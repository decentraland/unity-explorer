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

        public string AdapterUrl { get; }
        public string WorldCommsUrl { get; }
        public IRealmData RealmData { get; }

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

            WorldCommsUrl = decentralandUrlsSource.Url(DecentralandUrl.WorldComms);

            if (appArgs.TryGetValue(AppArgsFlags.GATEKEEPER_URL, out string? overrideUrl)
                && !string.IsNullOrEmpty(overrideUrl))
            {
                AdapterUrl = overrideUrl;
            }
            else
            {
                AdapterUrl = launchMode.CurrentMode switch
                             {
                                 LaunchMode.Play => decentralandUrlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter),
                                 LaunchMode.LocalSceneDevelopment => decentralandUrlsSource.Url(DecentralandUrl.LocalGateKeeperSceneAdapter),
                                 _ => throw new ArgumentOutOfRangeException()
                             };
            }
        }
    }
}
