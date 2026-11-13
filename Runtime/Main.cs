using System;
using System.Collections.Generic;
using Nox.CCK;
using Nox.CCK.Events;
using Nox.CCK.Mods.Cores;
using Nox.CCK.Mods.Initializers;
using Nox.CCK.Utils;
using Nox.Tables;

namespace Nox.Players.Runtime {
	public class Main : IMainModInitializer, IPlayerAPI {
		static internal IMainModCoreAPI CoreAPI;

		public void OnInitializeMain(IMainModCoreAPI api)
			=> CoreAPI = api;

		public void OnDisposeMain() {
			foreach (var data in Stores.ToArray())
				data.Dispose(true);
			Stores.Clear();
			CoreAPI = null;
		}

        public static NoxEvent<string, Action<PersistentData>> OnHeadExist = new();

		/// <summary>
		/// Live stores, shared by every consumer of the same player. Sharing them here is what makes
		/// the very same <see cref="IDataContainer"/> instance survive across sessions: a store is created
		/// on first access and kept for the application lifetime (released on <see cref="OnDisposeMain"/>).
		/// </summary>
		private static readonly List<PersistentData> Stores = new();

		/// <summary>
		/// Get the store of a player, creating it on first access, and retain it for the caller.
		/// Every call returns the very same instance for the whole application lifetime.
		/// </summary>
		public IDataContainer Get(Identifier id) {
			var key = id.ToShortString();

			foreach (var store in Stores) {
				if (store.Key != key)
					continue;

				store.Retain();
				return store;
			}

			var data = new PersistentData(id);
			Stores.Add(data);
			data.Retain();
			return data;
		}

		internal static ITableAPI TableAPI
            => CoreAPI.ModAPI
				.GetMod("tables")
				?.GetInstance<ITableAPI>();
	}
}
