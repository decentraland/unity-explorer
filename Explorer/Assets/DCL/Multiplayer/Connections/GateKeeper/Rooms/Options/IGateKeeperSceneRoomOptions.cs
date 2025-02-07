using DCL.Multiplayer.Connections.GateKeeper.Meta;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms.Options
{
    public interface IGateKeeperSceneRoomOptions
    {
        ISceneRoomMetaDataSource SceneRoomMetaDataSource { get; }

        string AdapterUrl { get; }
    }
}
