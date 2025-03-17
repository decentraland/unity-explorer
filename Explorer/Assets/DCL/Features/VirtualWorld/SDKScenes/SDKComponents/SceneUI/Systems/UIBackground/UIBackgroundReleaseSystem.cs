﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.SceneUI.Classes;
using DCL.SDKComponents.SceneUI.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using UnityEngine;

namespace DCL.SDKComponents.SceneUI.Systems.UIBackground
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UIBackgroundReleaseSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool componentPool;

        private UIBackgroundReleaseSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            poolsRegistry.TryGetPool(typeof(DCLImage), out componentPool);
        }

        protected override void Update(float t)
        {
            HandleEntityDestructionQuery(World);
            HandleUIBackgroundRemovalQuery(World);

            World.Remove<UIBackgroundComponent>(in HandleUIBackgroundRemoval_QueryDescription);
        }

        [Query]
        [None(typeof(PBUiBackground), typeof(DeleteEntityIntention))]
        private void HandleUIBackgroundRemoval(ref UIBackgroundComponent uiBackgroundComponent) =>
            RemoveDCLImage(ref uiBackgroundComponent);

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(ref UIBackgroundComponent uiBackgroundComponent) =>
            RemoveDCLImage(ref uiBackgroundComponent);

        private void RemoveDCLImage(ref UIBackgroundComponent uiBackgroundComponent)
        {
            if (uiBackgroundComponent.TexturePromise != null)
            {
                AssetPromise<Texture2DData, GetTextureIntention> texturePromiseValue = uiBackgroundComponent.TexturePromise.Value;
                texturePromiseValue.ForgetLoading(World);
                texturePromiseValue.TryDereference(World);
                uiBackgroundComponent.TexturePromise = null;
            }

            if (!uiBackgroundComponent.IsDisposed)
            {
                componentPool.Release(uiBackgroundComponent.Image);
                uiBackgroundComponent.Dispose();
            }
        }

        [Query]
        private void Release(ref UIBackgroundComponent uiBackgroundComponent) =>
            RemoveDCLImage(ref uiBackgroundComponent);

        public void FinalizeComponents(in Query query)
        {
            ReleaseQuery(World);
        }
    }
}
