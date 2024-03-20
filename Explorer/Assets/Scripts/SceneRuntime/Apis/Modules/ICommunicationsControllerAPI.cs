using Google.Protobuf;
using System;

namespace SceneRuntime.Apis.Modules
{
    public interface ICommunicationsControllerAPI : IDisposable
    {
        ByteString SendBinary(ByteString data);
    }
}
