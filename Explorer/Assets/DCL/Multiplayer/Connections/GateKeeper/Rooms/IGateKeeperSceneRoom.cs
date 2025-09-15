using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.GateKeeper.Meta;
using DCL.Multiplayer.Connections.Rooms.Connective;
using UnityEngine;

namespace DCL.Multiplayer.Connections.GateKeeper.Rooms
{
    public interface IGateKeeperSceneRoom : IActivatableConnectiveRoom
    {
        public MetaData? ConnectedScene { get; }

        /// <summary>
        ///     Tells if no communication channel is attached to the given scene.
        /// </summary>
        /// <param name="sceneId"></param>
        /// <returns>Returns false if the LiveKit room is connected but the scene itself is not loaded yet</returns>
        bool IsSceneConnected(string? sceneId);

        class Fake : Null, IGateKeeperSceneRoom
        {
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
