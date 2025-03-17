using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using NSubstitute;
using NUnit.Framework;
using System;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class WriteGltfContainerLoadingStateSystemShould : UnitySystemTestBase<WriteGltfContainerLoadingStateSystem>
    {
        private IECSToCRDTWriter writer;
        private IComponentPool<PBGltfContainerLoadingState> componentPool;
        private EntityEventBuffer<GltfContainerComponent> eventBuffer;

        [SetUp]
        public void SetUp()
        {
            writer = Substitute.For<IECSToCRDTWriter>();
            componentPool = Substitute.For<IComponentPool<PBGltfContainerLoadingState>>();

            componentPool.Get().Returns(new PBGltfContainerLoadingState());

            system = new WriteGltfContainerLoadingStateSystem(world, writer, eventBuffer = new EntityEventBuffer<GltfContainerComponent>(1));
        }

        [Test]
        public void WriteIfStateChanged()
        {
            var component = new GltfContainerComponent
                {
                    State = LoadingState.Finished,
                };

            var e = world.Create(component, new CRDTEntity(100), new PBGltfContainer());
            eventBuffer.Add(e, component);

            system.Update(0);

            writer.Received(1)
                  .PutMessage(
                       Arg.Any<Action<PBGltfContainerLoadingState, LoadingState>>(),
                       Arg.Is<CRDTEntity>(c => c.Id == 100),
                       Arg.Is<LoadingState>(c => c == LoadingState.Finished));
        }
    }
}
