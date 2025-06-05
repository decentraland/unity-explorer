using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using Global.Dynamic.LaunchModes;
using System;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms.Options
{
    public readonly struct GateKeeperSceneRoomOptions
    {
        public ISceneRoomMetaDataSource SceneRoomMetaDataSource { get; }

        public Uri AdapterUrl { get; }

        public GateKeeperSceneRoomOptions(
            ILaunchMode launchMode,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISceneRoomMetaDataSource play,
            ISceneRoomMetaDataSource localSceneDevelopment
        )
        {
            SceneRoomMetaDataSource = launchMode.CurrentMode switch
                                      {
                                          LaunchMode.Play => play,
                                          LaunchMode.LocalSceneDevelopment => localSceneDevelopment,
                                          _ => throw new ArgumentOutOfRangeException()
                                      };

            AdapterUrl = launchMode.CurrentMode switch
                         {
                             LaunchMode.Play => decentralandUrlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter),
                             LaunchMode.LocalSceneDevelopment => decentralandUrlsSource.Url(DecentralandUrl.LocalGateKeeperSceneAdapter),
                             _ => throw new ArgumentOutOfRangeException()
                         };
        }
    }
}
