using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.SignedFetch.Messages
{
    public class FlatFetchInit
    {
        public string? body;
        public Dictionary<string, string> headers;
        public string? method;
    }
}
