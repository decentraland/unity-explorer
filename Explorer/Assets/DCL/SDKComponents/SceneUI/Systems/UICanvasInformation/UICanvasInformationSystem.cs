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
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.SceneUI.Systems.UICanvasInformation
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.SCENE_UI)]
    public partial class UICanvasInformationSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private BorderRect interactableArea;
        private int lastViewportResolutionWidth = -1;
        private int lastScreenRealResolutionWidth = -1;
        private const int TOTAL_DELAY = 3;
        private int delay = 0;

        public override void Initialize()
        {
            base.Initialize();

            interactableArea = new BorderRect { Bottom = 0, Left = 0, Right = 0, Top = 0 };
            UpdateUICanvasInformationComponent();
        }

        private UICanvasInformationSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            delay = 0;
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
            //We add this logic because in the first frames the message gets lost and we wont send the size of the canvas
            //to the scene, causing breaking UIs, so we delay this.
            if (delay < TOTAL_DELAY)
            {
                delay++;
                return;
            }
            
            if (lastViewportResolutionWidth == Screen.width && lastScreenRealResolutionWidth == Screen.mainWindowDisplayInfo.width) return;

            lastScreenRealResolutionWidth = Screen.mainWindowDisplayInfo.width;
            lastViewportResolutionWidth = Screen.width;

            ecsToCRDTWriter.PutMessage<PBUiCanvasInformation, UICanvasInformationSystem>((component, system) =>
            {
                component.InteractableArea = system.interactableArea;
                component.Width = Screen.width;
                component.Height = Screen.height;
                component.DevicePixelRatio = system.lastScreenRealResolutionWidth / (float)system.lastViewportResolutionWidth;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, this);
        }
    }
}
