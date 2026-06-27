using System;
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
		
		public void OnDisposeMain() 
			=> CoreAPI = null;

        public static NoxEvent<string, Action<PlayerData>> OnHeadExist = new();

		public IPlayerData Get(Identifier id) 
            => new PlayerData(id);

		internal static ITableAPI TableAPI 
            => CoreAPI.ModAPI
				.GetMod("tables")
				?.GetInstance<ITableAPI>();
	}
}
