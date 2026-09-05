using Google.Protobuf;
using System.Collections.Generic;

namespace CrdtEcsBridge.Components
{
    public interface ISDKComponentsRegistry
    {
        IReadOnlyCollection<SDKComponentBridge> SdkComponents { get; }

        bool TryGet(int id, out SDKComponentBridge sdkComponentBridge);

        bool TryGet<T>(out SDKComponentBridge sdkComponentBridge) where T: IMessage;
    }
}
