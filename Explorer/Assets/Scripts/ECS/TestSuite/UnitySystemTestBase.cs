using Arch.Core;
using CrdtEcsBridge.Components.Transform;
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
        protected TSystem system;
        private World cachedWorld;

        protected World world => cachedWorld ??= World.Create();

        [TearDown]
        public void DestroyWorld()
        {
            system?.Dispose();
            cachedWorld?.Dispose();
            cachedWorld = null;
        }

        /// <summary>
        ///     Adds SDKTransform and creates a new GO with the entity Id as name
        /// </summary>
        protected TransformComponent AddTransformToEntity(in Entity entity, bool isDirty = false)
        {
            var go = new GameObject($"{entity.Id}");
            Transform t = go.transform;

            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            var transformComponent = new TransformComponent(t);

            world.Add(entity, transformComponent, new SDKTransform { IsDirty = isDirty, Position = Vector3.zero, Rotation = Quaternion.identity, Scale = Vector3.one });
            return transformComponent;
        }

        protected UITransformComponent AddUITransformToEntity(in Entity entity, bool isDirty = false)
        {
            var uiTransformComponent = new UITransformComponent();
            uiTransformComponent.Transform = new VisualElement();
            uiTransformComponent.Transform.name = $"{entity.Id}";

            world.Add(entity, uiTransformComponent, new PBUiTransform { IsDirty = isDirty });
            return uiTransformComponent;
        }
    }
}
