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
using System.Runtime.InteropServices;
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
            int realWidth = GetScreenRealResolutionWidth();
            if (lastViewportResolutionWidth == Screen.width && lastScreenRealResolutionWidth == realWidth)
                return;

            lastScreenRealResolutionWidth = realWidth;
            lastViewportResolutionWidth = Screen.width;

            WriteToCRDT();
        }

        private static int GetScreenRealResolutionWidth()
        {
#if UNITY_WEBGL
            return Screen.width;
#else
            return Screen.mainWindowDisplayInfo.width;
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal", EntryPoint = "GetDevicePixelRatio")]
        private static extern double GetDevicePixelRatioNative();
#endif

        private static float GetDevicePixelRatio()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return (float)GetDevicePixelRatioNative();
#elif UNITY_WEBGL
            return 1f;
#else
            return Screen.mainWindowDisplayInfo.width / (float)Screen.width;
#endif
        }

        private void WriteToCRDT()
        {
            ecsToCRDTWriter.PutMessage<PBUiCanvasInformation, UICanvasInformationSystem>(static (component, system) =>
            {
                component.InteractableArea = system.interactableArea;
                component.Width = Screen.width;
                component.Height = Screen.height;
                component.DevicePixelRatio = GetDevicePixelRatio();
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, this);
        }
    }
}
