using Nox.CCK;
using Nox.CCK.Utils;

namespace Nox.Players {
	/// <summary>
	/// Public API for managing per-player local data.
	/// Persisted in <c>.nox/</c> config and synced with <c>nox.table</c> when connected.
	/// </summary>
	public interface IPlayerAPI {
		/// <summary>
		/// Get or create the local data store for a player.
		/// The very same instance is returned for the whole application lifetime (shared across
		/// sessions). Loads from local file immediately, then fetches from <c>nox.table</c>
		/// in the background (keeping the most recent version based on <c>UpdatedAt</c>).
		/// </summary>
		IDataContainer Get(Identifier id);
	}
}
