﻿using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.ComponentsPooling;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using NSubstitute;
using NUnit.Framework;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class WriteGltfContainerLoadingStateSystemShould : UnitySystemTestBase<WriteGltfContainerLoadingStateSystem>
    {
        private IECSToCRDTWriter writer;
        private IComponentPool<PBGltfContainerLoadingState> componentPool;

        [SetUp]
        public void SetUp()
        {
            writer = Substitute.For<IECSToCRDTWriter>();
            componentPool = Substitute.For<IComponentPool<PBGltfContainerLoadingState>>();

            componentPool.Get().Returns(new PBGltfContainerLoadingState());

            system = new WriteGltfContainerLoadingStateSystem(world, writer, componentPool);
        }

        [Test]
        public void WriteIfStateChanged()
        {
            var component = new GltfContainerComponent();
            component.State.Set(LoadingState.Finished);

            world.Create(component, new CRDTEntity(100), new PBGltfContainer());

            system.Update(0);

            writer.Received(1).PutMessage(Arg.Is<CRDTEntity>(c => c.Id == 100), Arg.Is<PBGltfContainerLoadingState>(t => t.CurrentState == LoadingState.Finished));
        }

        [Test]
        public void WriteRemove()
        {
            var rc = RemovedComponents.CreateDefault();
            rc.Set.Add(typeof(PBGltfContainer));

            world.Create(new CRDTEntity(100), rc);

            system.Update(0);

            writer.Received(1).DeleteMessage<PBGltfContainerLoadingState>(new CRDTEntity(100));
        }
    }
}
