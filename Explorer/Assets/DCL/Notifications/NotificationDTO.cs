using System;
using System.Collections.Generic;

namespace DCL.Notification
{
    [Serializable]
    public struct NotificationDTO
    {
        public string id;
        public string type;
        public string address;
        public Dictionary<string, string> metadata;
        public string timestamp;
        public bool read;
    }

    [Serializable]
    public struct NotificationDTOList
    {
        public List<NotificationDTO> notifications;
    }

    [Serializable]
    public enum NotificationType
    {
        REWARD_ASSIGNMENT,
        EVENTS_STARTED,
        EVENTS_STARTS_SOON,
        GOVERNANCE_VOTING_ENDED_VOTER,
    }
}
