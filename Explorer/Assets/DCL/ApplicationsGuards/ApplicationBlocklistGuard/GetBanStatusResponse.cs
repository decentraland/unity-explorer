using System;

namespace DCL.ApplicationBlocklistGuard
{
    [Serializable]
    public class GetBanStatusResponse
    {
        public GetBanStatusData data;
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
