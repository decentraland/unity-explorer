using System;

namespace SceneRuntime.Apis.Modules
{
    [Serializable]
    public struct SendEthereumMessageResponse
    {
        public string jsonAnyResponse;

        [Serializable]
        public struct Payload
        {
            public long id;
            public string jsonrpc;
            public object result;
        }
    }
}
