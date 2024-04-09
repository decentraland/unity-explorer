using CrdtEcsBridge.Serialization;
using DCL.ECS7;
using DCL.ECSComponents;
using NSubstitute;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.Components.Tests
{
    public class SDKComponentsRegistryTests
    {
        private SDKComponentsRegistry sdkComponentsRegistry;


        public void Setup()
        {
            sdkComponentsRegistry = new SDKComponentsRegistry();

            sdkComponentsRegistry.Add(
                                      SDKComponentBuilder<FooComponent>.Create(1)
                                                                       .WithPool()
                                                                       .WithCustomSerializer(Substitute.For<IComponentSerializer<FooComponent>>())
                                                                       .Build())
                                 .Add(
                                      SDKComponentBuilder<BarComponent>.Create(2)
                                                                       .WithPool()
                                                                       .WithCustomSerializer(Substitute.For<IComponentSerializer<BarComponent>>())
                                                                       .Build()
                                  )
                                 .Add(
                                      SDKComponentBuilder<PBMeshCollider>.Create(ComponentID.MESH_COLLIDER)
                                                                         .AsProtobufComponent());
        }



        public void ProvideRegisteredComponents(Type componentType, int id)
        {
            Assert.IsTrue(sdkComponentsRegistry.TryGet(id, out SDKComponentBridge bridge));
            Assert.AreEqual(componentType, bridge.ComponentType);
        }



        public void BuildUpAllRequiredComponents(Type componentType, int id)
        {
            sdkComponentsRegistry.TryGet(id, out SDKComponentBridge bridge);
            Assert.IsNotNull(bridge.Serializer);
            Assert.IsNotNull(bridge.Pool);
            Assert.IsNotNull(bridge.CommandBufferSynchronizer);
        }

        public static object[][] AllTypes() =>
            new[]
            {
                new object[] { typeof(FooComponent), 1 },
                new object[] { typeof(BarComponent), 2 },
                new object[] { typeof(PBMeshCollider), ComponentID.MESH_COLLIDER },
            };

        public class FooComponent { }

        public class BarComponent { }
    }
}
