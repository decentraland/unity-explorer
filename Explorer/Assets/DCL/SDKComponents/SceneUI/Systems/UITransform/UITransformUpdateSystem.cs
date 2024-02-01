using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.SceneUI.Components;
using DCL.SDKComponents.SceneUI.Utils;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UITransform
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(UITransformSortingSystem))]
    [LogCategory(ReportCategory.SCENE_UI)]
    [ThrottlingEnabled]
    public partial class UITransformUpdateSystem : BaseUnityLoopSystem
    {
        private readonly UIDocument canvas;
        private readonly ISceneStateProvider sceneStateProvider;
        private bool? lastIsCurrentScene;

        public UITransformUpdateSystem(World world, UIDocument canvas, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.canvas = canvas;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float _)
        {
            UpdateUITransformQuery(World);

            if (lastIsCurrentScene != sceneStateProvider.IsCurrent)
            {
                lastIsCurrentScene = sceneStateProvider.IsCurrent;
                CheckUITransformOutOfSceneQuery(World);
            }
        }

        [Query]
        private void UpdateUITransform(ref PBUiTransform sdkModel, ref UITransformComponent uiTransformComponent)
        {
            if (!sdkModel.IsDirty)
                return;

            UiElementUtils.SetupVisualElement(ref uiTransformComponent.Transform.VisualElement, ref sdkModel);
            sdkModel.IsDirty = false;
        }

        [Query]
        [All(typeof(PBUiTransform), typeof(UITransformComponent))]
        private void CheckUITransformOutOfScene(ref UITransformComponent uiTransformComponent)
        {
            // Ignore all the child transforms
            if (uiTransformComponent.Transform.Parent != EntityReference.Null)
                return;

            // Depending on the scene state, we add or remove the root transform from the canvas
            switch (sceneStateProvider.IsCurrent)
            {
                case false when !uiTransformComponent.Transform.IsHidden:
                    canvas.rootVisualElement.Remove(uiTransformComponent.Transform.VisualElement);
                    uiTransformComponent.Transform.IsHidden = true;
                    break;
                case true when uiTransformComponent.Transform.IsHidden:
                    canvas.rootVisualElement.Add(uiTransformComponent.Transform.VisualElement);
                    uiTransformComponent.Transform.IsHidden = false;
                    break;
            }
        }
    }
}
