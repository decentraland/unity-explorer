using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.ECSComponents;
using DCL.SDKComponents.TextShape.Component;
using DCL.SDKComponents.TextShape.Renderer.Factory;
using DCL.SDKComponents.TextShape.System;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace DCL.SDKComponents.TextShape.Demo
{
    public class TextShapePlaygroundBoot : MonoBehaviour
    {
        [SerializeField]
        private TextShapeProperties textShapeProperties = new ();
        [SerializeField]
        private bool visible = true;

        private void Start()
        {
            StartAsync().Forget();
        }

        private async UniTask StartAsync()
        {
            var world = World.Create();
            var instantiateSystem = new InstantiateTextShapeSystem(world, new TextShapeRendererFactory());
            var updateSystem = new UpdateTextShapeSystem(world);
            var visibilitySystem = new VisibilityTextShapeSystem(world);

            var textShape = new PBTextShape();
            textShapeProperties.ApplyOn(textShape);

            var visibilityComponent = new PBVisibilityComponent();
            ApplySettings(visibilityComponent);

            world.Create(visibilityComponent, textShape, NewTransform());
            instantiateSystem.Update(Time.deltaTime);

            while (this)
            {
                textShapeProperties.ApplyOn(textShape);
                ApplySettings(visibilityComponent);
                updateSystem.Update(Time.deltaTime);
                visibilitySystem.Update(Time.deltaTime);
                await UniTask.Yield();
            }
        }

        private void ApplySettings(PBVisibilityComponent visibilityComponent)
        {
            visibilityComponent.Visible = visible;
            visibilityComponent.IsDirty = true;
        }

        private static TransformComponent NewTransform() =>
            new (new GameObject("text test"));
    }
}
