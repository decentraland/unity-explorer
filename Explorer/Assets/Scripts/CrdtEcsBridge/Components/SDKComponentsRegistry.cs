﻿using Google.Protobuf;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.Components
{
    public class SDKComponentsRegistry : ISDKComponentsRegistry
    {
        private const int INITIAL_CAPACITY = 30;

        private readonly Dictionary<int, SDKComponentBridge> bridges = new (INITIAL_CAPACITY);
        private readonly Dictionary<Type, SDKComponentBridge> bridgeByType = new (INITIAL_CAPACITY);

        public SDKComponentsRegistry Add(SDKComponentBridge bridge)
        {
            bridges.Add(bridge.Id, bridge);
            bridgeByType.Add(bridge.ComponentType, bridge);
            return this;
        }

        public IReadOnlyCollection<SDKComponentBridge> SdkComponents => bridges.Values;

        public bool TryGet(int id, out SDKComponentBridge sdkComponentBridge) =>
            bridges.TryGetValue(id, out sdkComponentBridge);

        public bool TryGet<T>(out SDKComponentBridge sdkComponentBridge) where T: IMessage<T> =>
            bridgeByType.TryGetValue(typeof(T), out sdkComponentBridge);
    }
}
