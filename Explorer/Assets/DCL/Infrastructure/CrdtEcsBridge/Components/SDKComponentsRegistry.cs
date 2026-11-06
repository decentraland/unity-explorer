using DCL.Diagnostics;
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
            if (!bridges.TryAdd(bridge.Id, bridge))
            {
                ReportHub.LogError(ReportCategory.CRDT_ECS_BRIDGE, $"SDK component bridge with id {bridge.Id} is already registered");
                return this;
            }

            if (!bridgeByType.TryAdd(bridge.ComponentType, bridge))
                ReportHub.LogError(ReportCategory.CRDT_ECS_BRIDGE, $"SDK component bridge for type {bridge.ComponentType.Name} is already registered");

            return this;
        }

        public bool TryGet(int id, out SDKComponentBridge sdkComponentBridge) =>
            bridges.TryGetValue(id, out sdkComponentBridge);

        public bool TryGet<T>(out SDKComponentBridge sdkComponentBridge) where T: IMessage =>
            bridgeByType.TryGetValue(typeof(T), out sdkComponentBridge);
    }
}
