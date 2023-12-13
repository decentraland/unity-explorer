using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.ColorComponent;
using ECS.Unity.Groups;
using ECS.Unity.SceneUI.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace ECS.Unity.SceneUI.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class InstantiateSceneUISystem : BaseUnityLoopSystem
    {
        private readonly UIDocument canvas;
        private readonly IComponentPool<Label> labelsPool;

        internal InstantiateSceneUISystem(World world, UIDocument canvas, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.canvas = canvas;
            labelsPool = poolsRegistry.GetReferenceTypePool<Label>();
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingUITextQuery(World);
        }

        [Query]
        [All(typeof(PBUiText), typeof(PBUiTransform))]
        [None(typeof(UITextComponent))]
        private void InstantiateNonExistingUIText(in Entity entity, ref PBUiText sdkComponent)
        {
            // Instantiate the UI Text
            var label = labelsPool.Get();
            label.text = sdkComponent.Value;
            label.style.color = sdkComponent.Color.ToUnityColor();
            label.style.fontSize = sdkComponent.HasFontSize ? sdkComponent.FontSize : 10;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            canvas.rootVisualElement.Add(label);

            // Add the UITextComponent to the entity
            var uiTextComponent = new UITextComponent();
            uiTextComponent.Label = label;
            World.Add(entity, uiTextComponent);
        }
    }
}
