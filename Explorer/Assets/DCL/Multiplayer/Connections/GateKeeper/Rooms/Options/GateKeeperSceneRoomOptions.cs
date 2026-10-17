using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.HardwareFingerprint;
using DCL.Utility;
using DCL.Utility.Types;
using ECS;
using System;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms.Options
{
    public readonly struct GateKeeperSceneRoomOptions
    {
        public ISceneRoomMetaDataSource SceneRoomMetaDataSource { get; }
        public IRealmData RealmData { get; }
        public string HardwareFingerprint { get; }

        public bool IsCommsOffline => RealmData.CommsAdapter.Contains("offline:offline");

        private readonly ILaunchMode launchMode;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;

        public GateKeeperSceneRoomOptions(
            ILaunchMode launchMode,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISceneRoomMetaDataSource play,
            ISceneRoomMetaDataSource localSceneDevelopment,
            IRealmData realmData,
            Option<HardwareFingerprintProvider> hardwareFingerprintProvider
        )
        {
            RealmData = realmData;
            HardwareFingerprint = hardwareFingerprintProvider.Has ? hardwareFingerprintProvider.Value.Fingerprint : string.Empty;
            SceneRoomMetaDataSource = launchMode.CurrentMode switch
                                      {
                                          LaunchMode.Play => play,
                                          LaunchMode.LocalSceneDevelopment => localSceneDevelopment,
                                          _ => throw new ArgumentOutOfRangeException()
                                      };

            this.launchMode = launchMode;
            this.decentralandUrlsSource = decentralandUrlsSource;
        }

        public string GetAdapterURL(string sceneID)
        {
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
