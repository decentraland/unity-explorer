using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using ECS.Abstract;
using GPUInstancerPro;
using UnityEngine;

namespace DCL.Landscape.GPUI_Pro
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class GPUIDebugSystem : BaseUnityLoopSystem
    {
        private bool isDebugWindowInstantiated;
        private readonly GPUIDebuggerCanvas debuggerCanvas;

        public GPUIDebugSystem(World world, IDebugContainerBuilder debugBuilder, GPUIDebuggerCanvas debuggerCanvas) : base(world)
        {
            this.debuggerCanvas = debuggerCanvas;

            debugBuilder.TryAddWidget("Landscape - GPUI")
                ?.AddSingleButton("Debug", InstantiateDebugWindow);
        }

        private void InstantiateDebugWindow()
        {
            if (isDebugWindowInstantiated) return;
            Object.Instantiate(debuggerCanvas);
        }

        protected override void Update(float t)
        {
        }
    }
}