namespace CrdtEcsBridge.Components
{
    public interface ISDKComponentsRegistry
    {
        bool TryGet(int id, out SDKComponentBridge sdkComponentBridge);
    }
}
