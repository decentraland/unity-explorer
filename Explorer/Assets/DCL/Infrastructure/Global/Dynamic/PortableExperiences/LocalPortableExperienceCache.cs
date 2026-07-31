using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using SceneRuntime.ScenePermissions;
using System;
using System.Collections.Generic;
using System.Threading;

namespace PortableExperiences.Controller
{
    /// <summary>
    ///     Per-session authorization state for local (scene-spawned) Portable Experiences, the counterpart of
    ///     SmartWearableCache for Smart Wearables.
    /// </summary>
    public class LocalPortableExperienceCache
    {
        private readonly IWebRequestController webRequestController;

        private readonly Dictionary<string, List<string>> permissionsCache = new (StringComparer.OrdinalIgnoreCase);

        public LocalPortableExperienceCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        public HashSet<string> AuthorizedPortableExperiences { get; } = new (StringComparer.OrdinalIgnoreCase);

        public HashSet<string> DeniedPortableExperiences { get; } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Fetches the Portable Experience's scene definitions to compute the permissions that need explicit
        ///     user authorization; cached for the whole session, so the definitions are fetched only once.
        /// </summary>
        public async UniTask<IReadOnlyList<string>> GetPermissionsRequiringAuthorizationAsync(string portableExperienceId, IIpfsRealm ipfsRealm, CancellationToken ct)
        {
            if (permissionsCache.TryGetValue(portableExperienceId, out List<string>? cachedPermissions))
                return cachedPermissions;

            var permissions = new List<string>();

            foreach (string urn in ipfsRealm.SceneUrns)
            {
                IpfsPath ipfsPath = IpfsHelper.ParseUrn(urn);

                SceneEntityDefinition sceneDefinition = await webRequestController
                                                             .GetAsync(new CommonArguments(ipfsPath.GetUrl(ipfsRealm.ContentBaseUrl)), ct, ReportCategory.PORTABLE_EXPERIENCE)
                                                             .CreateFromJson<SceneEntityDefinition>(WRJsonParser.Newtonsoft);

                List<string>? requiredPermissions = sceneDefinition.metadata.requiredPermissions;

                if (requiredPermissions == null) continue;

                foreach (string permission in requiredPermissions)
                    if (PermissionRequiresUserAuthorization(permission) && !permissions.Contains(permission))
                        permissions.Add(permission);
            }

            permissionsCache[portableExperienceId] = permissions;
            return permissions;
        }

        public void Clear()
        {
            permissionsCache.Clear();
            AuthorizedPortableExperiences.Clear();
            DeniedPortableExperiences.Clear();
        }

        // Mirrors the permission set gated by SmartWearableCache.CacheWearableInternalAsync.
        internal static bool PermissionRequiresUserAuthorization(string permission) =>
            permission is ScenePermissionNames.USE_WEB3_API
                or ScenePermissionNames.OPEN_EXTERNAL_LINK
                or ScenePermissionNames.USE_WEBSOCKET
                or ScenePermissionNames.SPAWN_PORTABLE_EXPERIENCE
                or ScenePermissionNames.USE_FETCH;
    }
}
