using DCL.Diagnostics;
using System;
using System.Collections.Generic;

namespace SceneRuntime.ScenePermissions
{
    public class RestrictedJsApiPermissionsProvider : IJsApiPermissionsProvider
    {
        protected readonly HashSet<string> cachedPermissions = new (StringComparer.OrdinalIgnoreCase);

        public RestrictedJsApiPermissionsProvider(IEnumerable<string> permissions)
        {
            foreach (string permission in permissions) cachedPermissions.Add(permission);
        }

        public bool CanOpenExternalUrl() =>
            CanInvokeAPI(ScenePermissionNames.OPEN_EXTERNAL_LINK, "Open External URL");

        public bool CanInvokeWebSocketsAPI() =>
            CanInvokeAPI(ScenePermissionNames.USE_WEBSOCKET, "Web Sockets API");

        public bool CanInvokeFetchAPI() =>
            CanInvokeAPI(ScenePermissionNames.USE_FETCH, "Fetch API");

        public bool CanInvokeWeb3API() =>
            CanInvokeAPI(ScenePermissionNames.USE_WEB3_API, "Web3 API");

        private bool CanInvokeAPI(string permission, string action)
        {
            if (!cachedPermissions.Contains(permission))
            {
                ReportHub.LogError(ReportCategory.SCENE_PERMISSIONS, $"{action}: permission denied");
                return false;
            }
            return true;
        }
    }
}
