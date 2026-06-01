using Nox.CCK.Utils;
using Nox.Entities;
using UnityEngine;

namespace Nox.Players {
	public interface IPlayer : IPlayer<IPart> { }

	public interface IPlayer<TPart> : ILivingEntity, IMovingEntity, IMultiPartEntity<TPart>
		where TPart : IPart {
		/// <summary>
		/// Get the display name of the player.
		/// </summary>
		/// <returns></returns>
		public string Display { get; set; }

		/// <summary>
		/// Get the player Identifier (ID).
		/// </summary>
		/// <returns></returns>
		public Identifier Identifier { get; }

		/// <summary>
		/// Check if the player is the master player.
		/// </summary>
		/// <returns></returns>
		public bool IsMaster { get; }

		/// <summary>
		/// Check if the player is a local player.
		/// </summary>
		/// <returns></returns>
		public bool IsLocal { get; }

		/// <summary>
		/// Teleport the player to a specific position and rotation (immediately).
		/// </summary>
		/// <param name="position">Target position</param>
		/// <param name="rotation">Target rotation</param>
		public void Teleport(Vector3 position, Quaternion rotation);

		/// <summary>
		/// Teleport the player to a spawn point.
		/// </summary>
		public void Respawn();
	}
}