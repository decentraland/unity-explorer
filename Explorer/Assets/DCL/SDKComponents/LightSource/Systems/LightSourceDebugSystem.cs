using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using UnityEngine;

namespace DCL.SDKComponents.LightSource.Systems
{
    /// <summary>
    /// Applies the debug state coming from the global debug state entity.
    /// The debug state is controlled with the in-game debug panel.
    /// </summary>
    [UpdateInGroup(typeof(LightSourcesLateGroup))]
    [LogCategory(ReportCategory.LIGHT_SOURCE)]
    public partial class LightSourceDebugSystem : BaseUnityLoopSystem
    {
        private static readonly QueryDescription DEBUG_STATE_QUERY = new QueryDescription().WithAll<LightSourceDebugState>();

        private readonly World globalWorld;

        private Entity debugStateEntity;

        public LightSourceDebugSystem(World world, World globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        public override void Initialize()
        {
            base.Initialize();

            debugStateEntity = globalWorld.GetSingleInstanceEntityOrNull(DEBUG_STATE_QUERY);
        }

        protected override void Update(float t)
        {
//TODO for some reason it throws NRE in WebGL, investigate later
//
//NullReferenceException: Object reference not set to an instance of an object.
//Rethrow as EcsSystemException: [LightSourceDebugSystem]
//  at ECS.Abstract.BaseUnityLoopSystem.Update (System.Single& t) [0x00000] in <00000000000000000000000000000000>:0 
//Rethrow as SceneExecutionException: <color=#FC9C26>[ECS] (88, -10)</color>: One or more errors occurred. ([LightSourceDebugSystem]) ([LightSourceDebugSystem]) ([LightSourceDebugSystem]) ([LightSourceDebugSystem])
#if !UNITY_WEBGL
            var debugState = globalWorld.Get<LightSourceDebugState>(debugStateEntity);

            ApplyDebugStateQuery(World, debugState);
#endif
        }

        [Query]
        private void ApplyDebugState([Data] in LightSourceDebugState debugState, in LightSourceComponent lightSourceComponent)
        {
            var light = lightSourceComponent.LightSourceInstance;

            light.enabled &= debugState.LightsEnabled;
            if (!debugState.LightsEnabled) return;

            bool shadowsEnabled = debugState.ShadowsEnabled && (light.type != LightType.Point || debugState.PointLightShadowsEnabled);

            var maxShadowQuality = shadowsEnabled ? LightShadows.Soft : LightShadows.None;
            light.shadows = LightSourceHelper.ClampShadowQuality(light.shadows, maxShadowQuality);
        }
    }
}
