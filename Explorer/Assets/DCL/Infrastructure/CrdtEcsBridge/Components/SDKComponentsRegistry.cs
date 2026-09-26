using DCL.Optimization.Pools;
using Google.Protobuf;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.Components
{
    public class SDKComponentsRegistry : ISDKComponentsRegistry
    {
        private readonly Dictionary<int, SDKComponentBridge> bridges = new (PoolConstants.SDK_COMPONENT_TYPES_COUNT);
        private readonly Dictionary<Type, SDKComponentBridge> bridgeByType = new (PoolConstants.SDK_COMPONENT_TYPES_COUNT);

        public IReadOnlyCollection<SDKComponentBridge> SdkComponents => bridges.Values;

        public SDKComponentsRegistry Add(SDKComponentBridge bridge)
        {
            bridges.Add(bridge.Id, bridge);
            bridgeByType.Add(bridge.ComponentType, bridge);
            return this;
        }

        public bool TryGet(int id, out SDKComponentBridge sdkComponentBridge) =>
            bridges.TryGetValue(id, out sdkComponentBridge);

        public bool TryGet<T>(out SDKComponentBridge sdkComponentBridge) where T: IMessage =>
            bridgeByType.TryGetValue(typeof(T), out sdkComponentBridge);
    }
}
