using System;
using Nox.CCK.Events;
using Nox.CCK.Utils;

namespace Nox.Players {
	/// <summary>
	/// Public API for managing per-player local data.
	/// Persisted in <c>.nox/</c> config and synced with <c>nox.table</c> when connected.
	/// </summary>
	public interface IPlayerAPI {
		/// <summary>
		/// Get or create the local data store for a player.
		/// Loads from local file immediately, then fetches from <c>nox.table</c>
		/// in the background (keeping the most recent version based on <c>UpdatedAt</c>).
		/// </summary>
		IPlayerData Get(Identifier id);
	}

	/// <summary>
	/// Per-player local key-value store persisted in <c>.nox/</c> config.
	/// Same API shape as <c>Config</c>, scoped to a single player.
	/// </summary>
	public interface IPlayerData : IDisposable {
		/// <summary>Read a typed value, or <paramref name="default"/> if not set.</summary>
		T Get<T>(string[] key, T @default = default);

		/// <summary>Read a typed value from a single-segment key.</summary>
		T Get<T>(string key, T @default = default);

		/// <summary>Check whether a key exists.</summary>
		bool Has(string[] key);

		/// <summary>Check whether a single-segment key exists.</summary>
		bool Has(string key);

		/// <summary>Write a typed value and persist immediately.</summary>
		void Set<T>(string[] key, T value);

		/// <summary>Write a typed value for a single-segment key.</summary>
		void Set<T>(string key, T value);

		/// <summary>Remove a key (resets to default).</summary>
		void Delete(string[] key);

		/// <summary>Remove a single-segment key (resets to default).</summary>
		void Delete(string key);

		NoxEvent<string[], object, object> OnChanged { get; }
	}
}