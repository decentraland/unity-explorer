using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PrivateWorlds
{
    /// <summary>
    /// Result of checking world access permissions.
    /// </summary>
    public enum WorldAccessCheckResult
    {
        /// <summary>
        /// User is allowed to access the world.
        /// </summary>
        Allowed,

        /// <summary>
        /// World requires a password to enter.
        /// </summary>
        PasswordRequired,

        /// <summary>
        /// User is not on the allow-list and not in any allowed community.
        /// </summary>
        AccessDenied,

        /// <summary>
        /// Failed to fetch permissions, should fallback to server-side check.
        /// </summary>
        CheckFailed
    }

    /// <summary>
    /// Contains the result of an access check along with additional context.
    /// </summary>
    public class WorldAccessCheckContext
    {
        public WorldAccessCheckResult Result { get; set; }
        public WorldAccessInfo? AccessInfo { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Interface for the world permissions service.
    /// </summary>
    public interface IWorldPermissionsService
    {
        /// <summary>
        /// Fetches and checks if the current user has access to a world.
        /// </summary>
        /// <param name="worldName">The world name (e.g., "my-world.dcl.eth")</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Access check result with context</returns>
        UniTask<WorldAccessCheckContext> CheckWorldAccessAsync(string worldName, CancellationToken ct);

        /// <summary>
        /// Fetches the raw permissions data for a world.
        /// </summary>
        /// <param name="worldName">The world name</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Parsed world access info</returns>
        UniTask<WorldAccessInfo?> GetWorldPermissionsAsync(string worldName, CancellationToken ct);

        /// <summary>
        /// Validates a password for a world. Returns true if the password is correct.
        /// </summary>
        UniTask<bool> ValidatePasswordAsync(string worldName, string password, CancellationToken ct);
    }

    /// <summary>
    /// Service for fetching and checking world access permissions.
    /// </summary>
    public class WorldPermissionsService : IWorldPermissionsService
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public WorldPermissionsService(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask<WorldAccessCheckContext> CheckWorldAccessAsync(string worldName, CancellationToken ct)
        {
            var context = new WorldAccessCheckContext();

            try
            {
                // Always use random mock for mirko.dcl.eth so we can test both flows regardless of backend
                if (string.Equals(worldName, "mirko.dcl.eth", StringComparison.OrdinalIgnoreCase))
                {
                    context = GetMockWorldAccessContext(worldName);
                    return context;
                }

                WorldAccessInfo? accessInfo = await GetWorldPermissionsAsync(worldName, ct);

                if (accessInfo == null)
                {
                    context = GetMockWorldAccessContext(worldName);
                    if (context.AccessInfo != null)
                    {
                        ReportHub.Log(ReportCategory.REALM, $"[WorldPermissionsService] Backend returned no data for '{worldName}'. Using mock access: {context.Result} (name-based).");
                        return context;
                    }
                    context.Result = WorldAccessCheckResult.CheckFailed;
                    context.ErrorMessage = "Failed to fetch world permissions";
                    return context;
                }

                context.AccessInfo = accessInfo;

                switch (accessInfo.AccessType)
                {
                    case WorldAccessType.Unrestricted:
                        context.Result = WorldAccessCheckResult.Allowed;
                        break;

                    case WorldAccessType.SharedSecret:
                        context.Result = WorldAccessCheckResult.PasswordRequired;
                        break;

                    case WorldAccessType.AllowList:
                        bool hasAccess = await CheckAllowListAccessAsync(accessInfo, ct);
                        context.Result = hasAccess ? WorldAccessCheckResult.Allowed : WorldAccessCheckResult.AccessDenied;
                        break;

                    default:
                        context.Result = WorldAccessCheckResult.Allowed;
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"Failed to check world permissions for '{worldName}': {e.Message}");
                context.Result = WorldAccessCheckResult.CheckFailed;
                context.ErrorMessage = e.Message;
            }

            return context;
        }

        public async UniTask<WorldAccessInfo?> GetWorldPermissionsAsync(string worldName, CancellationToken ct)
        {
            try
            {
                string baseUrl = urlsSource.Url(DecentralandUrl.WorldPermissions);
                string url = string.Format(baseUrl, worldName);

                var response = await webRequestController
                    .GetAsync(new CommonArguments(URLAddress.FromString(url)), ct, ReportCategory.REALM)
                    .CreateFromJson<WorldPermissionsResponse>(WRJsonParser.Newtonsoft);

                return WorldAccessInfo.FromResponse(response);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"Failed to fetch world permissions for '{worldName}': {e.Message}");
                return null;
            }
        }

        public async UniTask<bool> ValidatePasswordAsync(string worldName, string password, CancellationToken ct)
        {
            try
            {
                string baseUrl = urlsSource.Url(DecentralandUrl.WorldComms);
                string url = string.Format(baseUrl, worldName);

                string metadata = $"{{\"intent\":\"dcl:explorer:comms-handshake\",\"signer\":\"dcl:explorer\",\"isGuest\":false,\"secret\":\"{EscapeJsonString(password)}\"}}";

                long statusCode = await webRequestController
                    .SignedFetchPostAsync(url, metadata, ct)
                    .StatusCodeAsync();

                ReportHub.Log(ReportCategory.REALM, $"[WorldPermissionsService] ValidatePassword for '{worldName}': status {statusCode}");
                return statusCode != 401;
            }
            catch (UnityWebRequestException e)
            {
                ReportHub.Log(ReportCategory.REALM, $"[WorldPermissionsService] ValidatePassword for '{worldName}': caught status {e.ResponseCode}");
                return e.ResponseCode != 401;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception e)
            {
                ReportHub.LogWarning(ReportCategory.REALM, $"[WorldPermissionsService] ValidatePassword for '{worldName}' failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Escapes special characters in a string for safe JSON embedding.
        /// </summary>
        private static string EscapeJsonString(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
        }

        /// <summary>
        /// When backend has no data, mock access by world name so you can test flows.
        /// mirko.dcl.eth: randomly password protected or invitation only (for testing both flows).
        /// Names containing "password" or "secret" -> password protected; "invite" or "private" or "whitelist" -> invitation only; else -> unrestricted.
        /// </summary>
        private static WorldAccessCheckContext GetMockWorldAccessContext(string worldName)
        {
            var context = new WorldAccessCheckContext();
            var nameLower = worldName.ToLowerInvariant();

            if (string.Equals(nameLower, "mirko.dcl.eth", StringComparison.Ordinal))
            {
                bool passwordMode = UnityEngine.Random.Range(0,2) == 0;
                context.Result = passwordMode ? WorldAccessCheckResult.PasswordRequired : WorldAccessCheckResult.AccessDenied;
                context.AccessInfo = new WorldAccessInfo
                {
                    AccessType = passwordMode ? WorldAccessType.SharedSecret : WorldAccessType.AllowList,
                    OwnerAddress = "0xMockOwner0000000000000000000000000000",
                    AllowedWallets = passwordMode ? new List<string>() : new List<string>()
                };
                ReportHub.Log(ReportCategory.REALM, $"[WorldPermissionsService] Mock for mirko.dcl.eth: {(passwordMode ? "Password protected" : "Invitation only")} (random).");
                return context;
            }

            if (nameLower.Contains("password") || nameLower.Contains("secret"))
            {
                context.Result = WorldAccessCheckResult.PasswordRequired;
                context.AccessInfo = new WorldAccessInfo
                {
                    AccessType = WorldAccessType.SharedSecret,
                    OwnerAddress = "0xMockOwner0000000000000000000000000000"
                };
                return context;
            }

            if (nameLower.Contains("invite") || nameLower.Contains("private") || nameLower.Contains("whitelist"))
            {
                context.Result = WorldAccessCheckResult.AccessDenied;
                context.AccessInfo = new WorldAccessInfo
                {
                    AccessType = WorldAccessType.AllowList,
                    OwnerAddress = "0xMockOwner0000000000000000000000000000",
                    AllowedWallets = new List<string>()
                };
                return context;
            }

            context.Result = WorldAccessCheckResult.Allowed;
            context.AccessInfo = new WorldAccessInfo
            {
                AccessType = WorldAccessType.Unrestricted,
                OwnerAddress = string.Empty
            };
            return context;
        }

        private async UniTask<bool> CheckAllowListAccessAsync(WorldAccessInfo accessInfo, CancellationToken ct)
        {
            // Check if current user's wallet is in the allow list
            string? currentWallet = web3IdentityCache.Identity?.Address;

            if (!string.IsNullOrEmpty(currentWallet))
            {
                if (accessInfo.IsWalletAllowed(currentWallet))
                    return true;

                // Owner always has access
                if (currentWallet.Equals(accessInfo.OwnerAddress, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Check community membership
            // if (accessInfo.AllowedCommunities.Count > 0)
            // {
            //     foreach (string communityId in accessInfo.AllowedCommunities)
            //     {
            //         if (ct.IsCancellationRequested)
            //             return false;
            //
            //         try
            //         {
            //             bool isMember = await checkCommunityMembership(communityId, ct);
            //             if (isMember)
            //                 return true;
            //         }
            //         catch (Exception e)
            //         {
            //             ReportHub.LogWarning(ReportCategory.REALM, $"Failed to check community membership for '{communityId}': {e.Message}");
            //         }
            //     }
            // }

            return false;
        }
    }
}
