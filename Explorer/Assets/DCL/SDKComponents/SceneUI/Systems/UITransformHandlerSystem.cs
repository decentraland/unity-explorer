using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Unity.Groups;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformHandlerSystem : BaseUnityLoopSystem
    {
        private readonly UIDocument canvas;
        private readonly IComponentPoolsRegistry poolsRegistry;
        private readonly IComponentPool<VisualElement> transformsPool;

        private UITransformHandlerSystem(World world, UIDocument canvas, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            this.canvas = canvas;
            this.poolsRegistry = poolsRegistry;
            transformsPool = poolsRegistry.GetReferenceTypePool<VisualElement>();
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingUITransformQuery(World);
            TrySetupExistingUITransformQuery(World);
            HandleEntityDestructionQuery(World);
            HandleUITransformRemovalQuery(World);
            World.Remove<UITransformComponent>(in HandleUITransformRemoval_QueryDescription);
        }

        [Query]
        [All(typeof(PBUiTransform))]
        [None(typeof(UITransformComponent))]
        private void InstantiateNonExistingUITransform(in Entity entity, ref PBUiTransform sdkComponent)
        {
            var uiTransformComponent = new UITransformComponent();
            InstantiateEmptyVisualElement(ref uiTransformComponent, ref sdkComponent);
            World.Add(entity, uiTransformComponent);
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent))]
        private void TrySetupExistingUITransform(ref UITransformComponent uiTransformComponent, ref PBUiTransform sdkComponent)
        {
            if (!sdkComponent.IsDirty)
                return;

            if (ReferenceEquals(uiTransformComponent.Transform, null))
                InstantiateEmptyVisualElement(ref uiTransformComponent, ref sdkComponent);
            else
                SetupVisualElement(uiTransformComponent.Transform, sdkComponent);

            sdkComponent.IsDirty = false;
        }

        [Query]
        [None(typeof(PBUiTransform), typeof(DeleteEntityIntention))]
        private void HandleUITransformRemoval(ref UITransformComponent uiTransformComponent) =>
            RemoveVisualElement(uiTransformComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UITransformComponent uiTransformComponent) =>
            RemoveVisualElement(uiTransformComponent);

        private void InstantiateEmptyVisualElement(ref UITransformComponent uiTransformComponent, ref PBUiTransform sdkComponent)
        {
            var transform = transformsPool.Get();
            canvas.rootVisualElement.Add(transform);
            uiTransformComponent.Transform = transform;

            SetupVisualElement(transform, sdkComponent);
        }

        private static void SetupVisualElement(VisualElement visualElementToSetup, PBUiTransform model)
        {
            visualElementToSetup.style.display = UiElementUtils.GetDisplay(model.Display);
            visualElementToSetup.style.overflow = UiElementUtils.GetOverflow(model.Overflow);

            // Pointer blocking
            visualElementToSetup.pickingMode = model.PointerFilter == PointerFilterMode.PfmBlock ? PickingMode.Position : PickingMode.Ignore;

            // Flex
            visualElementToSetup.style.flexDirection = UiElementUtils.GetFlexDirection(model.FlexDirection);
            if (model.FlexBasisUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.flexBasis = model.FlexBasisUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.FlexBasis, UiElementUtils.GetUnit(model.FlexBasisUnit));

            visualElementToSetup.style.flexGrow = model.FlexGrow;
            visualElementToSetup.style.flexShrink = model.GetFlexShrink();
            visualElementToSetup.style.flexWrap = UiElementUtils.GetWrap(model.GetFlexWrap());

            // Align
            visualElementToSetup.style.alignContent = UiElementUtils.GetAlign(model.GetAlignContent());
            visualElementToSetup.style.alignItems = UiElementUtils.GetAlign(model.GetAlignItems());
            visualElementToSetup.style.alignSelf = UiElementUtils.GetAlign(model.AlignSelf);
            visualElementToSetup.style.justifyContent = UiElementUtils.GetJustify(model.JustifyContent);

            // Layout size
            if (model.HeightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.height = model.HeightUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.Height, UiElementUtils.GetUnit(model.HeightUnit));

            if (model.WidthUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.width = model.WidthUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.Width, UiElementUtils.GetUnit(model.WidthUnit));

            if (model.MaxWidthUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.maxWidth = model.MaxWidthUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.MaxWidth, UiElementUtils.GetUnit(model.MaxWidthUnit));

            if (model.MaxHeightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.maxHeight = model.MaxHeightUnit == YGUnit.YguAuto ? new StyleLength(StyleKeyword.Auto) : new Length(model.MaxHeight, UiElementUtils.GetUnit(model.MaxHeightUnit));

            if (model.MinHeightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.minHeight = new Length(model.MinHeight, UiElementUtils.GetUnit(model.MinHeightUnit));

            if (model.MinWidthUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.minWidth = new Length(model.MinWidth, UiElementUtils.GetUnit(model.MinWidthUnit));

            // Paddings
            if (model.PaddingBottomUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingBottom = new Length(model.PaddingBottom, UiElementUtils.GetUnit(model.PaddingBottomUnit));

            if (model.PaddingLeftUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingLeft = new Length(model.PaddingLeft, UiElementUtils.GetUnit(model.PaddingLeftUnit));

            if (model.PaddingRightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingRight = new Length(model.PaddingRight, UiElementUtils.GetUnit(model.PaddingRightUnit));

            if (model.PaddingTopUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.paddingTop = new Length(model.PaddingTop, UiElementUtils.GetUnit(model.PaddingTopUnit));

            // Margins
            if (model.MarginLeftUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginLeft = new Length(model.MarginLeft, UiElementUtils.GetUnit(model.MarginLeftUnit));

            if (model.MarginRightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginRight = new Length(model.MarginRight, UiElementUtils.GetUnit(model.MarginRightUnit));

            if (model.MarginBottomUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginBottom = new Length(model.MarginBottom, UiElementUtils.GetUnit(model.MarginBottomUnit));

            if (model.MarginTopUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.marginTop = new Length(model.MarginTop, UiElementUtils.GetUnit(model.MarginTopUnit));

            // Position
            visualElementToSetup.style.position = UiElementUtils.GetPosition(model.PositionType);

            if (model.PositionTopUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.top = new Length(model.PositionTop, UiElementUtils.GetUnit(model.PositionTopUnit));

            if (model.PositionBottomUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.bottom = new Length(model.PositionBottom, UiElementUtils.GetUnit(model.PositionBottomUnit));

            if (model.PositionRightUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.right = new Length(model.PositionRight, UiElementUtils.GetUnit(model.PositionRightUnit));

            if (model.PositionLeftUnit != YGUnit.YguUndefined)
                visualElementToSetup.style.left = new Length(model.PositionLeft, UiElementUtils.GetUnit(model.PositionLeftUnit));
        }

        private void RemoveVisualElement(UITransformComponent uiTransformComponent)
        {
            if (!poolsRegistry.TryGetPool(typeof(VisualElement), out IComponentPool componentPool))
                return;

            componentPool.Release(uiTransformComponent.Transform);
        }
    }
}
