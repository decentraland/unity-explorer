using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Arch.SystemGroups.Metadata;
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
        public bool isDebugWindowInstantiated;
        
        public GPUIDebugSystem(World world, IDebugContainerBuilder debugBuilder) : base(world)
        {
            debugBuilder.TryAddWidget("Landscape - GPUI")
                ?.AddSingleButton("Debug", InstantiateDebugWindow);
        }

        private void InstantiateDebugWindow()
        {
            if (isDebugWindowInstantiated) return;
            
            //ONLY VALID CASE FOR RESOURCES, DO NOT REUSE
            GPUIDebuggerCanvas debugWindow = Resources.Load<GPUIDebuggerCanvas>("GPUIDebuggerCanvas");
            GameObject.Instantiate(debugWindow);
        }

        protected override void Update(float t)
        {
        }
    }
}