using System;

namespace SceneRuntime.Apis.Modules
{
    public interface IRestrictedActionsAPI : IDisposable
    {
        public bool OpenExternalUrl(string url);
    }
}
