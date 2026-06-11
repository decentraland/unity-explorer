using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.PrivateWorlds
{
    internal static class WorldPermissionTypeNames
    {
        public const string Unrestricted = "unrestricted";
        public const string AllowList = "allow-list";
        public const string SharedSecret = "shared-secret";
    }

    /// <summary>
    /// Represents the type of access control for a world.
    /// </summary>
    public enum WorldAccessType
    {
        Unknown,
        Unrestricted,
        AllowList,
        SharedSecret
    }

    /// <summary>
    /// Response from the world permissions API endpoint.
    /// GET /world/{world_name}/permissions
    /// </summary>
    [Serializable]
    public class WorldPermissionsResponse
    {
        [JsonProperty("permissions")]
        public WorldPermissions Permissions { get; set; } = new ();

        [JsonProperty("owner")]
        public string Owner { get; set; } = string.Empty;
    }

    /// <summary>
    /// Contains all permission configurations for a world.
    /// </summary>
    [Serializable]
    public class WorldPermissions
    {
        [JsonProperty("deployment")]
        public PermissionConfig? Deployment { get; set; }

        [JsonProperty("access")]
        public AccessPermissionConfig? Access { get; set; }

        [JsonProperty("streaming")]
        public PermissionConfig? Streaming { get; set; }
    }

    /// <summary>
    /// Base permission configuration for allow-list type permissions.
    /// </summary>
    [Serializable]
    public class PermissionConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; } = WorldPermissionTypeNames.Unrestricted;

        [JsonProperty("wallets")]
        public List<string>? Wallets { get; set; }

        [JsonProperty("communities")]
        public List<string>? Communities { get; set; }
    }

    /// <summary>
    /// Access permission configuration that can include shared-secret type.
    /// </summary>
    [Serializable]
    public class AccessPermissionConfig
    {
        [JsonProperty("type")]
        public string Type { get; set; } = WorldPermissionTypeNames.Unrestricted;

        [JsonProperty("wallets")]
        public List<string>? Wallets { get; set; }

        [JsonProperty("communities")]
        public List<string>? Communities { get; set; }

        // Note: secret is write-only in the API - it's not returned in GET responses
        // We use this when POSTing a password to validate access
    }

    /// <summary>
    /// Parsed and processed world access information for easier consumption.
    /// </summary>
    public class WorldAccessInfo
    {
        private List<string> allowedWallets = new ();
        private HashSet<string> allowedWalletsSet = new (StringComparer.OrdinalIgnoreCase);

        public WorldAccessType AccessType { get; set; }
        public string OwnerAddress { get; set; } = string.Empty;
        public List<string> AllowedWallets
        {
            get => allowedWallets;
            set
            {
                allowedWallets = value ?? new List<string>();
                allowedWalletsSet = new HashSet<string>(allowedWallets, StringComparer.OrdinalIgnoreCase);
            }
        }

        public IReadOnlyCollection<string> AllowedWalletsSet => allowedWalletsSet;
        public List<string> AllowedCommunities { get; set; } = new ();

        /// <summary>
        /// Creates a WorldAccessInfo from the raw API response.
        /// </summary>
        public static WorldAccessInfo FromResponse(WorldPermissionsResponse response)
        {
            var info = new WorldAccessInfo
            {
                OwnerAddress = response.Owner
            };

            var access = response.Permissions.Access;

            if (access == null || string.IsNullOrEmpty(access.Type) ||
                access.Type.Equals(WorldPermissionTypeNames.Unrestricted, StringComparison.OrdinalIgnoreCase))
            {
                info.AccessType = WorldAccessType.Unrestricted;
            }
            else if (access.Type.Equals(WorldPermissionTypeNames.AllowList, StringComparison.OrdinalIgnoreCase))
            {
                info.AccessType = WorldAccessType.AllowList;
                info.AllowedWallets = access.Wallets ?? new List<string>();
                info.AllowedCommunities = access.Communities ?? new List<string>();
            }
            else if (access.Type.Equals(WorldPermissionTypeNames.SharedSecret, StringComparison.OrdinalIgnoreCase))
            {
                info.AccessType = WorldAccessType.SharedSecret;
            }
            else
            {
                info.AccessType = WorldAccessType.Unknown;
            }

            return info;
        }

        /// <summary>
        /// Checks if a wallet address is in the allow list (case-insensitive).
        /// </summary>
        public bool IsWalletAllowed(string walletAddress)
        {
            return !string.IsNullOrEmpty(walletAddress) &&
                   allowedWalletsSet.Contains(walletAddress);
        }
    }
}
