using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.Rooms.Connective;
using SceneRunner.Scene;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoom : IActivatableConnectiveRoom
    {
        public ISceneData? ConnectedScene { get; }

        /// <summary>
        ///     Tells if no communication channel is attached to the given scene
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        bool IsSceneConnected(string? sceneId);

        class Fake : Null, IGateKeeperSceneRoom
        {
            public ISceneData? ConnectedScene { get; } = new ISceneData.Fake();

            public bool Activated => true;

            public bool IsSceneConnected(string? sceneId) =>
                false;

            public UniTask Activate() =>
                UniTask.CompletedTask;

            public UniTask Deactivate() =>
                UniTask.CompletedTask;
        }
    }
}
