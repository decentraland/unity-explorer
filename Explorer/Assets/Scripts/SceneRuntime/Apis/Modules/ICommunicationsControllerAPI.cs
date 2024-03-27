using System;

namespace SceneRuntime.Apis.Modules
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        object SendBinary(byte[][] data);
    }
}
