using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Groups;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.Unity.EngineInfo
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public class WriteEngineInfoSystem : BaseUnityLoopSystem
    {
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IComponentPool<PBEngineInfo> pool;

        internal WriteEngineInfoSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter, IComponentPool<PBEngineInfo> pool) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.pool = pool;
        }

        public override void Initialize()
        {
            PropagateToSceneQuery(World);
        }

        protected override void Update(float t)
        {
            PropagateToSceneQuery(World);
        }

        [Query]
        [All(typeof(SceneRootComponent))]
        private void PropagateToScene(ref CRDTEntity sdkEntity)
        {
            PBEngineInfo c = pool.Get();
            c.TickNumber = sceneStateProvider.TickNumber;
            c.FrameNumber = (uint)(Time.frameCount - sceneStateProvider.EngineStartInfo.FrameNumber);
            c.TotalRuntime = Time.realtimeSinceStartup - sceneStateProvider.EngineStartInfo.Time;

            ecsToCRDTWriter.PutMessage(sdkEntity, c);
        }
    }
}
