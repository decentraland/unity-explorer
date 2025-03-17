using CommunicationData.URLHelpers;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public interface IRemoteMetadata
    {
        IReadOnlyDictionary<string, ParticipantMetadata> Metadata { get; }

        void BroadcastSelfParcel(Vector2Int pose);

        /// <summary>
        ///     Work-around for the bug
        ///     OnConnected will be called while the room is not assigned, so the callback is missed so we need to asign the room right now
        /// </summary>
        void BroadcastSelfMetadata();

        readonly struct ParticipantMetadata
        {
            public readonly Vector2Int Parcel;
            public readonly URLDomain LambdasEndpoint;

            public ParticipantMetadata(Vector2Int parcel, URLDomain lambdasEndpoint)
            {
                Parcel = parcel;
                LambdasEndpoint = lambdasEndpoint;
            }

            public override string ToString() =>
                $"(Parcel: {Parcel}, Lambdas: {LambdasEndpoint})";
        }
    }
}
