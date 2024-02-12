using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Multiplayer.Connections.Credentials.Hub
{
    public interface ICredentialsHub
    {
        UniTask<ICredentials> SceneRoomCredentials(Vector2Int parcelPosition, CancellationToken token);

        UniTask<ICredentials> IslandRoomCredentials(CancellationToken token);

        class Null : ICredentialsHub
        {
            public static readonly Null INSTANCE = new ();

            public UniTask<ICredentials> SceneRoomCredentials(Vector2Int parcelPosition, CancellationToken token) =>
                throw new Exception("I'm a null implementation! Use an actual object!");

            public UniTask<ICredentials> IslandRoomCredentials(CancellationToken token) =>
                throw new Exception("I'm a null implementation! Use an actual object!");
        }
    }
}
