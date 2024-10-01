using CommunicationData.URLHelpers;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Multiplayer.Profiles.Poses
{
    public interface IRemoteMetadata
    {
        IReadOnlyDictionary<string, ParticipantMetadata> Metadata { get; }

        void BroadcastSelfPose(Vector2Int pose);

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
