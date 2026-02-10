using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Web3.Identities;
using DCL.WebRequests;
using Newtonsoft.Json;
using System;
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
                WorldAccessInfo? accessInfo = await GetWorldPermissionsAsync(worldName, ct);

                if (accessInfo == null)
                {
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

        private const int VALIDATE_PASSWORD_TIMEOUT_SECONDS = 30;

        public async UniTask<bool> ValidatePasswordAsync(string worldName, string password, CancellationToken ct)
        {
            try
            {
                string baseUrl = urlsSource.Url(DecentralandUrl.WorldComms);
                string url = string.Format(baseUrl, worldName);

                string metadata = $"{{\"intent\":\"dcl:explorer:comms-handshake\",\"signer\":\"dcl:explorer\",\"isGuest\":false,\"secret\":\"{EscapeJsonString(password)}\"}}";

                var commonArguments = new CommonArguments(
                    URLAddress.FromString(url),
                    RetryPolicy.NONE,
                    timeout: VALIDATE_PASSWORD_TIMEOUT_SECONDS);

                long statusCode = await webRequestController
                    .SignedFetchPostAsync(commonArguments, metadata, ct)
                    .StatusCodeAsync();

                ReportHub.Log(ReportCategory.REALM, $"[WorldPermissionsService] ValidatePassword for '{worldName}': status {statusCode}");
                // Only allow access on explicit success (2xx). 403 = wrong password; any other code or no response = invalid.
                return statusCode >= 200 && statusCode < 300;
            }
            catch (UnityWebRequestException e)
            {
                ReportHub.Log(ReportCategory.REALM,
                    $"[WorldPermissionsService] ValidatePassword for '{worldName}': status {e.ResponseCode}, response: {e.Text}");
                // No successful response (network error, timeout, 4xx, 5xx) => do not allow access
                return false;
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
        /// Escapes a string for safe embedding in a JSON value using Newtonsoft.
        /// </summary>
        private static string EscapeJsonString(string value)
        {
            // JsonConvert.ToString returns a quoted string like "the\"value", so trim the outer quotes
            string quoted = JsonConvert.ToString(value);
            return quoted.Substring(1, quoted.Length - 2);
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

            // TODO: Implement community membership check for AllowedCommunities.
            // Use CommunitiesDataProvider.GetCommunityAsync(communityId) and check role != CommunityMemberRole.none.

            return false;
        }
    }
}
