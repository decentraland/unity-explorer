using JetBrains.Annotations;
using System;

namespace SceneRuntime.Apis.Modules
{
    public class RestrictedActionsAPIWrapper : IDisposable
    {
        private readonly IRestrictedActionsAPI api;

        public RestrictedActionsAPIWrapper(IRestrictedActionsAPI api)
        {
            this.api = api;
        }

        public void Dispose()
        {
            api.Dispose();
        }

        [UsedImplicitly]
        public bool OpenExternalUrl(string url) =>
            api.OpenExternalUrl(url);
    }
}
