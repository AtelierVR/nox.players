using System;
using System.IO;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nox.CCK.Events;
using Nox.CCK.Utils;
using Nox.Tables;

namespace Nox.Players.Runtime {
	/// <summary>
	/// Per-player local data persisted in
	/// <c>.nox/config/data/&lt;identifier&gt;.json</c>
	/// and synced with <c>nox.table</c> when connected.
	/// <para>
	/// Uses a chain pattern: the first instance (head) owns persistence.
	/// Children delegate all writes to the head. When the head is disposed,
	/// the next child takes over.
	/// </para>
	/// </summary>
	public sealed class PlayerData : IPlayerData, IDisposable {
		private readonly string _filePath;
		private readonly string _tableKey;
		private JObject _data;
		private DateTime _localMtime;

		private CancellationTokenSource _saveCts;
		private const float SaveDelaySeconds = 5f;
		private bool _isReady;

		/// <summary>Next instance in the chain (set when this is the head and a new child joins).</summary>
		private PlayerData _next;

		/// <summary>The head for this player (null if this instance IS the head).</summary>
		private PlayerData _head;

		private bool IsHead
			=> _head == null;

		private static string BaseDir
			=> Path.Combine(Main.CoreAPI.ConfigAPI.GetFolder(), "data");

		public PlayerData(Identifier id) {
			_filePath = Path.Combine(BaseDir, $"{id}.json");
			_tableKey = $"players.{id}";

			// Ask if a head already exists for this player
			PlayerData found = null;
			Main.OnHeadExist.Invoke(_tableKey, (head) => found = head);

			if (found != null) {
				// Join chain as child
				_head = found;
				_data = new JObject(found._data);
				_localMtime = found._localMtime;
				_isReady = true;
				found._next = this;
			} else {
				// Become head
				_head = null;
				Main.OnHeadExist.AddListener(OnHeadExistRequest);
				LoadLocal();
				LoadAsync().Forget();
			}
		}

		/// <summary>
		/// Called when a new instance fires <see cref="Main.OnHeadExist"/>.
		/// If the key matches, respond with this instance as the head.
		/// </summary>
		private void OnHeadExistRequest(string key, Action<PlayerData> callback) {
			if (key == _tableKey)
				callback(this);
		}

		/// <summary>
		/// Load data from local file and optionally merge with remote table.
		/// Only called on the head instance.
		/// </summary>
		private async UniTask LoadAsync() {
			if (Main.TableAPI == null) {
				MarkReady();
				return;
			}

			IEntry remote;
			try {
				remote = await Main.TableAPI.Get(_tableKey);
			} catch {
				MarkReady();
				return;
			}

			if (remote == null || remote.AsBytes == null) {
				MarkReady();
				return;
			}

			if (remote.UpdatedAt > _localMtime) {
				try {
					var json = Encoding.UTF8.GetString(remote.AsBytes);
					_data = JObject.Parse(json);
					_localMtime = remote.UpdatedAt;
					SaveLocal();
				} catch {
					// corrupt remote data, keep local
				}
			} else if (_data != null) {
				SyncToTable().Forget();
			}

			MarkReady();
		}

		private void MarkReady() {
			_isReady = true;
		}

		private void LoadLocal() {
			try {
				if (File.Exists(_filePath)) {
					var json = File.ReadAllText(_filePath);
					_data = JObject.Parse(json);
					_localMtime = File.GetLastWriteTimeUtc(_filePath);
				} else {
					_data = new JObject();
					_localMtime = DateTime.MinValue;
				}
			} catch {
				_data = new JObject();
				_localMtime = DateTime.MinValue;
			}
		}

		private void SaveLocal() {
			try {
				Directory.CreateDirectory(BaseDir);
				var json = _data.ToString(Formatting.Indented);
				File.WriteAllText(_filePath, json);
				_localMtime = File.GetLastWriteTimeUtc(_filePath);
			} catch (Exception e) {
				Logger.LogWarning($"Failed to save player data for '{_filePath}': {e.Message}");
			}
		}

		private void ScheduleSave() {
			_saveCts?.Cancel();
			_saveCts = new CancellationTokenSource();
			DebouncedSave(_saveCts.Token).Forget();
		}

		private async UniTask DebouncedSave(CancellationToken ct) {
			try {
				await UniTask.Delay(TimeSpan.FromSeconds(SaveDelaySeconds), cancellationToken: ct);
			} catch (OperationCanceledException) {
				return;
			}

			SaveLocal();
			SyncToTable().Forget();
		}

		public void Dispose() {
			if (IsHead) {
				Main.OnHeadExist.RemoveListener(OnHeadExistRequest);

				// Flush pending save immediately
				_saveCts?.Cancel();
				_saveCts?.Dispose();
				_saveCts = null;
				SaveLocal();

				if (_next != null) {
					// Pass ownership to next child
					_next._data = _data;
					_next._localMtime = _localMtime;
					_next._isReady = true;
					_next._head = null;
					Main.OnHeadExist.AddListener(_next.OnHeadExistRequest);
				}
			} else {
				// Child: just cleanup local resources
				_saveCts?.Cancel();
				_saveCts?.Dispose();
				_saveCts = null;
			}
		}

		private async UniTask SyncToTable() {
			try {
				if (Main.TableAPI == null)
					return;

				var json = _data.ToString(Formatting.None);
				var entry = await Main.TableAPI.Set(_tableKey, json, "application/json+player");
				if (entry != null)
					_localMtime = entry.UpdatedAt;
			} catch {
				// table sync is best-effort
			}
		}

		private JObject Data {
			get {
				if (_data != null)
					return _data;
				LoadLocal();
				return _data;
			}
		}

		private static JToken Resolve(JObject root, string[] key) {
			JToken current = root;
			foreach (var segment in key) {
				if (current is JObject obj) {
					current = obj[segment];
					if (current == null)
						return null;
				} else {
					return null;
				}
			}
			return current;
		}

		public T Get<T>(string[] key, T @default = default) {
			var root = IsHead ? Data : _head.Data;
			var token = Resolve(root, key);
			if (token != null) {
				try { return token.ToObject<T>(); }
				catch { }
			}
			return @default;
		}

		public T Get<T>(string key, T @default = default)
			=> Get(key.Split('.'), @default);

		public bool Has(string[] key) {
			var root = IsHead ? Data : _head.Data;
			return Resolve(root, key) != null;
		}

		public bool Has(string key)
			=> Has(key.Split('.'));

		public void Set<T>(string[] key, T value) {
			if (IsHead) {
				var old = Get<object>(key);
				SetNested(Data, key, 0, value);
				ScheduleSave();
				OnChanged.Invoke(key, value, old);
			} else {
				_head.Set(key, value);
			}
		}

		public void Set<T>(string key, T value)
			=> Set(key.Split('.'), value);

		public void Delete(string[] key) {
			if (IsHead) {
				if (key.Length == 0) return;
				var old = Get<object>(key);
				DeleteNested(Data, key);
				ScheduleSave();
				OnChanged.Invoke(key, null, old);
			} else {
				_head.Delete(key);
			}
		}

		public void Delete(string key)
			=> Delete(key.Split('.'));

		public NoxEvent<string[], object, object> OnChanged { get; } = new();

		private static void DeleteNested(JObject root, string[] key) {
			JToken current = root;
			for (var i = 0; i < key.Length - 1; i++) {
				if (current is JObject obj)
					current = obj[key[i]];
				else return;
				if (current == null) return;
			}

			if (current is JObject parent)
				parent.Remove(key[^1]);
		}

		private static void SetNested(JObject root, string[] key, int index, object value) {
			while (true) {
				var propName = key[index];
				if (index == key.Length - 1) {
					root[propName] = value != null 
						? JToken.FromObject(value) 
						: JValue.CreateNull();
					return;
				}

				if (root[propName] is not JObject child) {
					child          = new JObject();
					root[propName] = child;
				}

				root  = child;
				index++;
			}
		}
	}
}
