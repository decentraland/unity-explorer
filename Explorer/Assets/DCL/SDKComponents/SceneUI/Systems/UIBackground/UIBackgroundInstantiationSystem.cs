using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Defaults;
using DCL.SDKComponents.SceneUI.Groups;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;

namespace DCL.SDKComponents.SceneUI.Systems.UIBackground
{
    [UpdateInGroup(typeof(SceneUIComponentInstantiationGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UIBackgroundInstantiationSystem : BaseUnityLoopSystem
    {
        //private readonly IComponentPool<DCLImage> imagesPool;

        private UIBackgroundInstantiationSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            //imagesPool = poolsRegistry.GetReferenceTypePool<DCLImage>();
        }

        protected override void Update(float t)
        {
            InstantiateUIBackgroundQuery(World);
            UpdateUIBackgroundQuery(World);
        }

        [Query]
        [All(typeof(PBUiBackground), typeof(PBUiTransform), typeof(UITransformComponent))]
        [None(typeof(UIBackgroundComponent))]
        private void InstantiateUIBackground(in Entity entity, ref UITransformComponent uiTransformComponent)
        {
            //var image = imagesPool.Get();
            var image = new DCLImage();
            image.Initialize(uiTransformComponent.Transform);
            var uiBackgroundComponent = new UIBackgroundComponent();
            uiBackgroundComponent.Image = image;
            World.Add(entity, uiBackgroundComponent);
        }

        [Query]
        [All(typeof(PBUiBackground), typeof(UITransformComponent))]
        private void UpdateUIBackground(ref PBUiBackground sdkModel, ref UIBackgroundComponent uiBackgroundComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            // TODO: Set image!
            uiBackgroundComponent.Image.Color = sdkModel.GetColor();
            uiBackgroundComponent.Image.Slices = sdkModel.GetBorder();
            uiBackgroundComponent.Image.UVs = sdkModel.Uvs.ToDCLUVs();
            uiBackgroundComponent.Image.ScaleMode = sdkModel.TextureMode.ToDCLImageScaleMode();

            sdkModel.IsDirty = false;
        }
    }
}
