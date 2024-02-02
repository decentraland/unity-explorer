using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIPointerEvents;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIPointerEventsSystemShould : UnitySystemTestBase<UIPointerEventsSystem>
    {
        private Entity entity;
        private UITransformComponent uiTransformComponent;
        private ISceneStateProvider sceneStateProvider;
        private IECSToCRDTWriter ecsToCRDTWriter;

        [SetUp]
        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            system = new UIPointerEventsSystem(world, sceneStateProvider, ecsToCRDTWriter);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));
        }

        [Test]
        public void UpdatePointerEvents()
        {
            // Arrange
            var input = new PBPointerEvents { IsDirty = true };
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetDown,
                EventInfo = new PBPointerEvents.Types.Info(),
            });
            input.PointerEvents.Add(new PBPointerEvents.Types.Entry
            {
                EventType = PointerEventType.PetUp,
                EventInfo = new PBPointerEvents.Types.Info(),
            });

            // Act
            world.Add(entity, input);
            system.Update(0);

            // Assert
            Assert.IsTrue(uiTransformComponent.Transform.HasAnyPointerDownCallback);
            Assert.IsTrue(uiTransformComponent.Transform.HasAnyPointerUpCallback);
        }
    }
}
