using System;

// ReSharper disable InconsistentNaming
namespace DCL.ApplicationGuards
{
    // Server schema: decentraland/comms-gatekeeper src/controllers/handlers/user-moderation/ban-status-handler.ts#/BanStatusResponse
    // data is missing from some 200 bodies in practice (UNITY-EXPLORER-PW7), so it is optional.
    [Serializable]
    public class GetBanStatusResponse
    {
        public GetBanStatusData? data;
    }

    [Serializable]
    public class GetBanStatusData
    {
        public bool isBanned;
        public BannedUserData? ban;
    }

    // Server schema: decentraland/comms-gatekeeper src/logic/user-moderation/types.ts#/UserBan (bannedDeviceId is never sent)
    [Serializable]
    public class BannedUserData
    {
        public string id = null!;
        public string bannedAddress = null!;
        public string bannedBy = null!;
        public string reason = null!;
        public string? customMessage;
        public string bannedAt = null!;
        public string? expiresAt;
        public string? liftedAt;
        public string? liftedBy;
        public string createdAt = null!;
    }
}
