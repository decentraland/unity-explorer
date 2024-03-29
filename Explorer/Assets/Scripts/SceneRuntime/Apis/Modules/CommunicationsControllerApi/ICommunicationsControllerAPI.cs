using System;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        object SendBinary(byte[][] data);
    }
}
