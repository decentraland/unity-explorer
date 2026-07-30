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
    ///     Stores per-session authorization state for local (scene-spawned) Portable Experiences,
    ///     mirroring what SmartWearableCache does for Smart Wearables.
    ///
    ///     It also caches the permissions requiring user authorization computed from each Portable Experience's
    ///     scene definitions, so the definitions are fetched only once per session.
    /// </summary>
    public class LocalPortableExperienceCache
    {
        private readonly IWebRequestController webRequestController;

        private readonly Dictionary<string, List<string>> permissionsCache = new (StringComparer.OrdinalIgnoreCase);

        public LocalPortableExperienceCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        /// <summary>
        ///     Portable Experiences authorized during the current session.
        ///     The user won't be asked again for authorization of those Portable Experiences.
        /// </summary>
        public HashSet<string> AuthorizedPortableExperiences { get; } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Portable Experiences the user denied during the current session.
        ///     Spawn attempts for those fail without prompting again.
        /// </summary>
        public HashSet<string> DeniedPortableExperiences { get; } = new (StringComparer.OrdinalIgnoreCase);

        /// <summary>
        ///     Fetches the scene definitions of the Portable Experience and returns the subset of its
        ///     required permissions that need explicit user authorization. Results are cached per session.
        /// </summary>
        public async UniTask<IReadOnlyList<string>> GetPermissionsRequiringAuthorizationAsync(string portableExperienceId, IIpfsRealm ipfsRealm, CancellationToken ct)
        {
            if (permissionsCache.TryGetValue(portableExperienceId, out List<string> cachedPermissions))
                return cachedPermissions;

            var permissions = new List<string>();

            // The scene pointers pipeline re-fetches these definitions later through its own (non-cached) promise,
            // so the first spawn of a Portable Experience costs one extra GET per scene definition.
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
                or ScenePermissionNames.PORTABLE_EXPERIENCE
                or ScenePermissionNames.USE_FETCH;
    }
}
