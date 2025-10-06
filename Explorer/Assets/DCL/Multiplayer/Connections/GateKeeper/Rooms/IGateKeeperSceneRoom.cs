using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms.Connective;
using System;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoom : IActivatableConnectiveRoom
    {
        /// <summary>
        /// This event is triggered when the current scene room is successfully connected.
        /// </summary>
        public event Action CurrentSceneRoomConnected;

        /// <summary>
        /// This event is triggered when the current scene room is disconnected.
        /// </summary>
        public event Action CurrentSceneRoomDisconnected;

        /// <summary>
        /// This event is triggered when we receive a forbidden access response from the server after trying to connect to the current scene room.
        /// </summary>
        public event Action CurrentSceneRoomForbiddenAccess;

        public MetaData? ConnectedScene { get; }

        /// <summary>
        ///     Tells if no communication channel is attached to the given scene.
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns>Returns false if the LiveKit room is connected but the scene itself is not loaded yet</returns>
        bool IsSceneConnected(string? sceneId);

        class Fake : Null, IGateKeeperSceneRoom
        {
            public event Action? CurrentSceneRoomConnected;
            public event Action? CurrentSceneRoomDisconnected;
            public event Action? CurrentSceneRoomForbiddenAccess;
            public MetaData? ConnectedScene { get; } = new MetaData("Fake", Vector2Int.zero, new MetaData.Input());

            public bool Activated => true;

            public bool IsSceneConnected(string? sceneId) =>
                false;

            public UniTask ActivateAsync() =>
                UniTask.CompletedTask;

            public UniTask DeactivateAsync() =>
                UniTask.CompletedTask;
        }
    }
}
