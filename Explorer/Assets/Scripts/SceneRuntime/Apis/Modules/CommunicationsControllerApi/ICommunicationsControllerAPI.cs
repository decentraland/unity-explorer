using System;
using System.Collections.Generic;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        object SendBinary(IReadOnlyList<byte[]> data);

        void OnSceneBecameCurrent();
    }
}
