using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using Decentraland.Common;
using ECS.Abstract;
using ECS.Groups;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.SDKComponents.SceneUI.Systems.UICanvasInformation
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UICanvasInformationSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly UIDocument canvas;
        private BorderRect interactableArea;
        private int lastViewportResolutionWidth = -1;
        private int lastViewportResolutionHeight = -1;
        private float lastDevicePixelRatio = -1;

        public override void Initialize()
        {
            base.Initialize();

            interactableArea = new BorderRect { Bottom = 0, Left = Screen.width * 0.25f, Right = 0, Top = 0 };

            WriteToCRDT();
        }

        private UICanvasInformationSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, UIDocument canvas) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.canvas = canvas;
        }

        protected override void Update(float t)
        {
            PropagateToSceneQuery(World);
        }

        [Query]
        [All(typeof(SceneRootComponent))]
        private void PropagateToScene()
        {
            UpdateUICanvasInformationComponent();
        }

        private void UpdateUICanvasInformationComponent()
        {
            float devicePixelRatio = GetDevicePixelRatio();

            if (lastViewportResolutionWidth == Screen.width && lastViewportResolutionHeight == Screen.height && Mathf.Approximately(lastDevicePixelRatio, devicePixelRatio))
                return;

            lastViewportResolutionWidth = Screen.width;
            lastViewportResolutionHeight = Screen.height;
            lastDevicePixelRatio = devicePixelRatio;

            interactableArea.Left = Screen.width * 0.25f;

            WriteToCRDT();
        }

        private float GetDevicePixelRatio()
        {
            VisualElement root = canvas.rootVisualElement;
            return root?.panel != null ? root.scaledPixelsPerPoint : 1f;
        }

        private void WriteToCRDT()
        {
            ecsToCRDTWriter.PutMessage<PBUiCanvasInformation, UICanvasInformationSystem>(static (component, system) =>
            {
                component.InteractableArea = system.interactableArea;
                component.Width = Screen.width;
                component.Height = Screen.height;
                component.DevicePixelRatio = system.GetDevicePixelRatio();
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, this);
        }
    }
}
