using System;

namespace SceneRuntime.Apis.Modules
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        byte[][] SendBinary(byte[][] data);
    }
}
