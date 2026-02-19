using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Represents the type of access control for a world.
    /// </summary>
    public enum WorldAccessType
    {
        /// <summary>
        /// Anyone can access the world without restrictions.
        /// </summary>
        Unrestricted,

        /// <summary>
        /// Only wallets explicitly listed or members of specified communities can access.
        /// </summary>
        AllowList,

        /// <summary>
        /// Access requires a password/secret.
        /// </summary>
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
        public string Type { get; set; } = "unrestricted";

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
        public string Type { get; set; } = "unrestricted";

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
        public WorldAccessType AccessType { get; set; }
        public string OwnerAddress { get; set; } = string.Empty;
        public List<string> AllowedWallets { get; set; } = new ();
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
                access.Type.Equals("unrestricted", StringComparison.OrdinalIgnoreCase))
            {
                info.AccessType = WorldAccessType.Unrestricted;
            }
            else if (access.Type.Equals("allow-list", StringComparison.OrdinalIgnoreCase))
            {
                info.AccessType = WorldAccessType.AllowList;
                info.AllowedWallets = access.Wallets ?? new List<string>();
                info.AllowedCommunities = access.Communities ?? new List<string>();
            }
            else if (access.Type.Equals("shared-secret", StringComparison.OrdinalIgnoreCase))
            {
                info.AccessType = WorldAccessType.SharedSecret;
            }

            return info;
        }

        /// <summary>
        /// Checks if a wallet address is in the allow list (case-insensitive).
        /// </summary>
        public bool IsWalletAllowed(string walletAddress)
        {
            if (string.IsNullOrEmpty(walletAddress))
                return false;

            foreach (string allowed in AllowedWallets)
            {
                if (allowed.Equals(walletAddress, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
