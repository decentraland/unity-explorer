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

namespace DCL.SDKComponents.SceneUI.Systems.UICanvasInformation
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UICanvasInformationSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private BorderRect interactableArea;
        private int lastViewportResolutionWidth = -1;
        private int lastScreenRealResolutionWidth = -1;

        public override void Initialize()
        {
            base.Initialize();

            interactableArea = new BorderRect { Bottom = 0, Left = 0, Right = 0, Top = 0 };

            WriteToCRDT();
        }

        private UICanvasInformationSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
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
            if (lastViewportResolutionWidth == Screen.width && lastScreenRealResolutionWidth == Screen.mainWindowDisplayInfo.width)
                return;

            lastScreenRealResolutionWidth = Screen.mainWindowDisplayInfo.width;
            lastViewportResolutionWidth = Screen.width;

            WriteToCRDT();
        }

        private void WriteToCRDT()
        {
            ecsToCRDTWriter.PutMessage<PBUiCanvasInformation, UICanvasInformationSystem>(static (component, system) =>
            {
                component.InteractableArea = system.interactableArea;
                component.Width = Screen.width;
                component.Height = Screen.height;
                component.DevicePixelRatio = Screen.mainWindowDisplayInfo.width / (float)Screen.width;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, this);
        }
    }
}
