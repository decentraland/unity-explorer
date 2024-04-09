using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ECS.TestSuite
{
    public abstract class UnitySystemTestBase<TSystem> where TSystem: BaseUnityLoopSystem
    {
        protected TSystem system;
        private World cachedWorld;

        protected World world => cachedWorld ??= World.Create();


        public void DestroyWorld()
        {
            system?.Dispose();
            cachedWorld?.Dispose();
            cachedWorld = null;
        }

        protected TransformComponent AddTransformToEntity(in Entity entity, bool isDirty = false) =>
            EcsTestsUtils.AddTransformToEntity(world, entity, isDirty);

        protected UITransformComponent AddUITransformToEntity(in Entity entity, bool isDirty = false)
        {
            var uiTransformComponent = new UITransformComponent();
            uiTransformComponent.Transform = new VisualElement { name = $"{entity.Id}",};

            world.Add(entity, uiTransformComponent, new PBUiTransform { IsDirty = isDirty });
            return uiTransformComponent;
        }
    }
}
