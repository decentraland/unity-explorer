using System.Collections.Generic;

namespace CrdtEcsBridge.Components
{
    public class SDKComponentsRegistry : ISDKComponentsRegistry
    {
        private readonly Dictionary<int, SDKComponentBridge> bridges = new (30);

        public ISDKComponentsRegistry Add(SDKComponentBridge bridge)
        {
            bridges.Add(bridge.Id, bridge);
            return this;
        }

        public bool TryGet(int id, out SDKComponentBridge sdkComponentBridge) =>
            bridges.TryGetValue(id, out sdkComponentBridge);
    }
}
