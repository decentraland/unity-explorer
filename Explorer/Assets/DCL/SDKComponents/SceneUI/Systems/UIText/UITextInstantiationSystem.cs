using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Fonts;
using SceneRunner.Scene;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Entity = Arch.Core.Entity;

namespace DCL.SDKComponents.SceneUI.Systems.UIText
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UITextInstantiationSystem : BaseUnityLoopSystem
    {
        private const string COMPONENT_NAME = "UIText";

        private readonly IComponentPool<Label> labelsPool;
        private readonly StyleFontDefinition[] styleFontDefinitions;
        private readonly ISceneData sceneData;
        private readonly IPartitionComponent scenePartition;

        public UITextInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry, in StyleFontDefinition[] styleFontDefinitions, ISceneData sceneData, IPartitionComponent scenePartition) : base(world)
        {
            labelsPool = poolsRegistry.GetReferenceTypePool<Label>();
            this.styleFontDefinitions = styleFontDefinitions;
            this.sceneData = sceneData;
            this.scenePartition = scenePartition;
        }

        protected override void Update(float t)
        {
            InstantiateUITextQuery(World);
            UpdateUITextQuery(World);
            ApplyLoadedFontQuery(World);
        }

        [Query]
        [All(typeof(PBUiText), typeof(UITransformComponent))]
        [None(typeof(UITextComponent), typeof(DeleteEntityIntention))]
        private void InstantiateUIText(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            var label = labelsPool.Get();
            label.name = UiElementUtils.BuildElementName(COMPONENT_NAME, entity);
            label.pickingMode = PickingMode.Ignore;
            UiElementUtils.SetElementDefaultStyle(label.style);
            uiTransformComponent.ContentContainer.Add(label);
            var uiTextComponent = new UITextComponent();
            uiTextComponent.Label = label;
            World.Add(entity, uiTextComponent);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateUIText(ref UITextComponent uiTextComponent, ref PBUiText sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            if (uiTextComponent.FontRequest.Update(World, sceneData, sdkModel.FontSrc, scenePartition))
                uiTextComponent.CustomFont = null;

            UiElementUtils.SetupLabel(ref uiTextComponent.Label, ref sdkModel, ref uiTransformComponent, in styleFontDefinitions, uiTextComponent.CustomFont);
            sdkModel.IsDirty = false;
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyLoadedFont(ref UITextComponent uiTextComponent, ref PBUiText sdkModel)
        {
            if (!uiTextComponent.FontRequest.TryConsume(World, out FontFamilyAssets? assets))
                return;

            uiTextComponent.CustomFont = assets?.UIToolkitFont;

            if (uiTextComponent.CustomFont != null)
                UiElementUtils.SetFont(uiTextComponent.Label, sdkModel.GetFont(), in styleFontDefinitions, uiTextComponent.CustomFont);
        }
    }
}
