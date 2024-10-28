using CommunicationData.URLHelpers;
using DCL.Character;
using Utility;

namespace DCL.Multiplayer.Profiles.Poses
{
    public static class RemoteMetadataExtensions
    {
        public static void BroadcastSelfParcel(this IRemoteMetadata remoteMetadata, ICharacterObject characterObject) =>
            remoteMetadata.BroadcastSelfParcel(characterObject.Position.ToParcel());

        /// <summary>
        ///     Without metadata we don't know in which catalyst profile was updated
        ///     so we fall back to the empty one that will be replaced with the lambdas of the local user (previous behaviour)
        /// </summary>
        public static URLDomain? GetLambdaDomainOrNull(this IRemoteMetadata remoteMetadata, string walletId) =>
            remoteMetadata.Metadata.TryGetValue(walletId, out IRemoteMetadata.ParticipantMetadata metadata) ? metadata.LambdasEndpoint : null;
    }
}
