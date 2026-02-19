using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Utility;
using ECS;
using Global.AppArgs;
using System;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms.Options
{
    public readonly struct GateKeeperSceneRoomOptions
    {
        public ISceneRoomMetaDataSource SceneRoomMetaDataSource { get; }
        public IRealmData RealmData { get; }

        private readonly string? overrideAdapterURL;
        private readonly ILaunchMode launchMode;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public GateKeeperSceneRoomOptions(
            ILaunchMode launchMode,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISceneRoomMetaDataSource play,
            ISceneRoomMetaDataSource localSceneDevelopment,
            IAppArgs appArgs,
            IRealmData realmData
        )
        {
            RealmData = realmData;
            SceneRoomMetaDataSource = launchMode.CurrentMode switch
                                      {
                                          LaunchMode.Play => play,
                                          LaunchMode.LocalSceneDevelopment => localSceneDevelopment,
                                          _ => throw new ArgumentOutOfRangeException()
                                      };

            this.launchMode = launchMode;
            this.decentralandUrlsSource = decentralandUrlsSource;

            if (appArgs.TryGetValue(AppArgsFlags.GATEKEEPER_URL, out string? overrideUrl)
                && !string.IsNullOrEmpty(overrideUrl))
                overrideAdapterURL = overrideUrl;
            else
                overrideAdapterURL = null;
        }

        public string GetAdapterURL(string sceneID)
        {
            if (!string.IsNullOrEmpty(overrideAdapterURL))
                return overrideAdapterURL;

            if (launchMode.CurrentMode == LaunchMode.LocalSceneDevelopment)
                return decentralandUrlsSource.Url(DecentralandUrl.LocalGateKeeperSceneAdapter);

            if (launchMode.CurrentMode == LaunchMode.Play)
            {
                if (RealmData.IsWorld() && !RealmData.SingleScene)
                    return string.Format(decentralandUrlsSource.Url(DecentralandUrl.WorldCommsAdapter), RealmData.RealmName, sceneID);
            }

            return decentralandUrlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter);
        }
    }
}
