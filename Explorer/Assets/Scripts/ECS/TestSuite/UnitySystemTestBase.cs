using Arch.Core;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace ECS.TestSuite
{
    public abstract class UnitySystemTestBase<TSystem> where TSystem: BaseUnityLoopSystem
    {
        protected TSystem? system;
        private World? cachedWorld;

        protected World world
        {
            get
            {
                if (cachedWorld != null)
                    return cachedWorld;

                cachedWorld = World.Create();
                cachedWorld.Create(new SceneShortInfo(Vector2Int.zero, "TEST"));

                return cachedWorld;
            }
        }

        [TearDown]
        public void DestroyWorld()
        {
            system?.Dispose();
            cachedWorld?.Dispose();
            cachedWorld = null;
        }

        protected TransformComponent AddTransformToEntity(in Entity entity, bool isDirty = false, World world = null) =>
            EcsTestsUtils.AddTransformToEntity(world ?? this.world, entity, isDirty);

        protected UITransformComponent AddUITransformToEntity(in Entity entity, bool isDirty = false)
        {
            var uiTransformComponent = new UITransformComponent();
            uiTransformComponent.Transform = new VisualElement { name = $"{entity.Id}",};

            world.Add(entity, uiTransformComponent, new PBUiTransform { IsDirty = isDirty });
            return uiTransformComponent;
        }
    }
}
