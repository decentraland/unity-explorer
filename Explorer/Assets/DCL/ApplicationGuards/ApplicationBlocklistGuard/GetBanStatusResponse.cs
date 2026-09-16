using System;

namespace DCL.ApplicationGuards
{
    // Server schema: decentraland/comms-gatekeeper src/controllers/handlers/user-moderation/ban-status-handler.ts#/BanStatusResponse
    // (the 200 body is a { data } envelope; UNITY-EXPLORER-PW7 observed data missing, so it is declared optional)
    [Serializable]
    public class GetBanStatusResponse
    {
        public GetBanStatusData? data;
    }

    [Serializable]
    public class GetBanStatusData
    {
        public bool isBanned;
        public BannedUserData ban;
    }

    [Serializable]
    public class BannedUserData
    {
        public string id;
        public string bannedAddress;
        public string bannedBy;
        public string reason;
        public string customMessage;
        public string bannedAt;
        public string expiresAt;
        public string liftedAt;
        public string liftedBy;
        public string createdAt;
    }
}
