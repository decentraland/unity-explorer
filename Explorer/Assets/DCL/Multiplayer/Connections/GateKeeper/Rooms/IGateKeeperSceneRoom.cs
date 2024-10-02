using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Connections.Rooms.Connective;
using LiveKit.Rooms;
using SceneRunner.Scene;
using System;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoom : IConnectiveRoom
    {
        /// <summary>
        ///     Tells if no communication channel is attached to the given scene
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns></returns>
        bool IsSceneConnected(string? sceneId);

        public SceneShortInfo? ConnectedScene { get; }

        class Fake : IGateKeeperSceneRoom
        {
            public UniTask<bool> StartAsync() =>
                UniTask.FromResult(false);

            public UniTask StopAsync() =>
                UniTask.CompletedTask;

            public State CurrentState() =>
                State.Stopped;

            public IRoom Room() =>
                NullRoom.INSTANCE;

            public bool IsSceneConnected(string sceneId) =>
                false;

            public SceneShortInfo? ConnectedScene => new SceneShortInfo();
        }
    }
}
