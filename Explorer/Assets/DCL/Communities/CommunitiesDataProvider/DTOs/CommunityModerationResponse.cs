using System;

namespace DCL.Communities.CommunitiesDataProvider.DTOs
{
    [Serializable]
    public class CommunityModerationResponse
    {
        public string error;
        public string message;
        public bool communityContentValidationUnavailable;
        public CommunityModerationData data;
    }

    [Serializable]
    public class CommunityModerationData
    {
        public CommunityModerationIssues issues;
    }

    [Serializable]
    public class CommunityModerationIssues
    {
        public string[] name;
        public string[] description;
        public string[] image;
    }
}
