﻿using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CrdtEcsBridge.Components.Special;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using SceneRunner.Scene;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformSortingSystem))]
    [ThrottlingEnabled]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UITransformUpdateSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly UIDocument canvas;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly Entity sceneRoot;

        public UITransformUpdateSystem(World world, UIDocument canvas, ISceneStateProvider sceneStateProvider, Entity sceneRoot) : base(world)
        {
            this.canvas = canvas;
            this.sceneStateProvider = sceneStateProvider;
            this.sceneRoot = sceneRoot;
        }

        protected override void Update(float _)
        {
            UpdateUITransformQuery(World);

            // For newly created and modified entities
            CheckUITransformOutOfSceneQuery(World, sceneStateProvider.IsCurrent);
        }

        [Query]
        private void UpdateUITransform(ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            bool zIndexChanged = false;
            if (sdkModel.HasZIndex && uiTransformComponent.ZIndex != sdkModel.ZIndex)
            {
                zIndexChanged = true;
                uiTransformComponent.ZIndex = sdkModel.ZIndex;
            }

            UiElementUtils.SetupVisualElement(uiTransformComponent.Transform, ref sdkModel);

            // If zIndex changed, mark the parent layout as dirty.
            // This is needed to trigger UITransformSortingSystem.ApplySorting
            // Note: UITransformUpdateSystem executes after UITransformSortingSystem so there will be always one frame delay.
            if (zIndexChanged && uiTransformComponent.RelationData.parent != Entity.Null && World.IsAlive(uiTransformComponent.RelationData.parent))
            {
                ref var parentComponent = ref World.Get<UITransformComponent>(uiTransformComponent.RelationData.parent);
                parentComponent.RelationData.layoutIsDirty = true;
            }

            sdkModel.IsDirty = false;
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent))]
        [None(typeof(SceneRootComponent))]
        private void CheckUITransformOutOfScene([Data] bool isCurrent, ref UITransformComponent uiTransformComponent)
        {
            // Ignore all the child transforms
            if (uiTransformComponent.RelationData.parent != sceneRoot)
                return;

            // Depending on the scene state, we add or remove the root transform from the canvas
            switch (isCurrent)
            {
                case false when !uiTransformComponent.IsHidden:
                    canvas.rootVisualElement.Remove(uiTransformComponent.Transform);
                    uiTransformComponent.IsHidden = true;
                    break;
                case true when uiTransformComponent.IsHidden:
                    canvas.rootVisualElement.Add(uiTransformComponent.Transform);
                    uiTransformComponent.IsHidden = false;
                    break;
            }
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            if(sceneStateProvider.State.Value() == SceneState.Disposed) return;

            CheckUITransformOutOfSceneQuery(World, value);
        }
    }
}
