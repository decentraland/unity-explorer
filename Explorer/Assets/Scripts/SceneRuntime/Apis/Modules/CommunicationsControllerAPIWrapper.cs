using Google.Protobuf;
using JetBrains.Annotations;
using System;

namespace SceneRuntime.Apis.Modules
{
    public class CommunicationsControllerAPIWrapper : IDisposable
    {
        private readonly ICommunicationsControllerAPI api;

        public CommunicationsControllerAPIWrapper(ICommunicationsControllerAPI api)
        {
            this.api = api;
        }

        public void Dispose()
        {
            api.Dispose();
        }

        [UsedImplicitly]
        public ByteString SendBinary(ByteString data) =>
            api.SendBinary(data);
    }
}
