using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Systems.UIPointerEvents;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Tests
{
    public class UIPointerEventsSystemShould : UnitySystemTestBase<UIPointerEventsSystem>
    {
        private Entity entity;
        private UITransformComponent uiTransformComponent;
        private ISceneStateProvider sceneStateProvider;
        private IECSToCRDTWriter ecsToCRDTWriter;


        public void SetUp()
        {
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            system = new UIPointerEventsSystem(world, sceneStateProvider, ecsToCRDTWriter);
            entity = world.Create();
            uiTransformComponent = AddUITransformToEntity(entity);
            world.Add(entity, new CRDTEntity(500));
        }




        public void TriggerPointerEvents(PointerEventType eventType)
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

            world.Add(entity, input);

            // Act
            uiTransformComponent.PointerEventTriggered = eventType;
            system.Update(0);

            // Assert
            Assert.AreEqual(PickingMode.Position, uiTransformComponent.Transform.pickingMode);
            ecsToCRDTWriter.Received(1).AppendMessage(
                Arg.Any<Action<PBPointerEventsResult, (RaycastHit sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>>(),
                Arg.Any<CRDTEntity>(),
                Arg.Any<int>(),
                Arg.Any<(RaycastHit sdkHit, InputAction button, PointerEventType eventType, ISceneStateProvider sceneStateProvider)>());
            Assert.IsNull(uiTransformComponent.PointerEventTriggered);
        }
    }
}
