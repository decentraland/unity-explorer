using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using Global.Dynamic.LaunchModes;
using System;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms.Options
{
    public class GateKeeperSceneRoomOptions : IGateKeeperSceneRoomOptions
    {
        private readonly ILaunchMode launchMode;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly ISceneRoomMetaDataSource play;
        private readonly ISceneRoomMetaDataSource localSceneDevelopment;

        public GateKeeperSceneRoomOptions(
            ILaunchMode launchMode,
            IDecentralandUrlsSource decentralandUrlsSource,
            ISceneRoomMetaDataSource play,
            ISceneRoomMetaDataSource localSceneDevelopment
        )
        {
            this.launchMode = launchMode;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.play = play;
            this.localSceneDevelopment = localSceneDevelopment;
        }

        public ISceneRoomMetaDataSource SceneRoomMetaDataSource => launchMode.CurrentMode switch
                                                                   {
                                                                       LaunchMode.Play => play,
                                                                       LaunchMode.LocalSceneDevelopment => localSceneDevelopment,
                                                                       _ => throw new ArgumentOutOfRangeException()
                                                                   };

        public string AdapterUrl => launchMode.CurrentMode switch
                                    {
                                        LaunchMode.Play => decentralandUrlsSource.Url(DecentralandUrl.GateKeeperSceneAdapter),
                                        LaunchMode.LocalSceneDevelopment => decentralandUrlsSource.Url(DecentralandUrl.LocalGateKeeperSceneAdapter),
                                        _ => throw new ArgumentOutOfRangeException()
                                    };
    }
}
