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
            var debugState = globalWorld.Get<LightSourceDebugState>(debugStateEntity);

            ApplyDebugStateQuery(World, debugState);
        }

        [Query]
        private void ApplyDebugState([Data] in LightSourceDebugState debugState, in PBLightSource pbLightSource,  in LightSourceComponent lightSourceComponent)
        {
            var light = lightSourceComponent.LightSourceInstance;

            bool shadowsEnabled = debugState.ShadowsEnabled && (light.type != LightType.Point || debugState.PointLightShadowsEnabled);

            var maxShadowQuality = shadowsEnabled ? LightShadows.Soft : LightShadows.None;
            light.shadows = LightSourceHelper.GetCappedUnityLightShadows(pbLightSource, maxShadowQuality);
        }
    }
}
