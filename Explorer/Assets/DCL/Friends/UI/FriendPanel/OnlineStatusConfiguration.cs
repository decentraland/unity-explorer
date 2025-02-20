using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Friends.UI.FriendPanel
{
    [CreateAssetMenu(fileName = "FriendPanelOnlineStatusSettings", menuName = "DCL/Friends/OnlineStatusConfiguration")]
    [Serializable]
    public class OnlineStatusConfiguration : ScriptableObject
    {
        [SerializeField] private List<StatusConfiguration> onlineStatusConfigurations;

        public OnlineStatusConfigurationData GetConfiguration(OnlineStatus status)
        {
            foreach (var config in onlineStatusConfigurations)
                if (config.Status == status)
                    return config.Configuration;

            throw new ArgumentOutOfRangeException($"Couldn't find configuration for status {status}");
        }
    }

    [Serializable]
    public class StatusConfiguration
    {
        public OnlineStatus Status;
        public OnlineStatusConfigurationData Configuration;
    }

    [Serializable]
    public class OnlineStatusConfigurationData
    {
        public Color StatusColor;
        public string StatusText;
    }

    public enum OnlineStatus
    {
        ONLINE,
        AWAY,
        OFFLINE
    }
}
