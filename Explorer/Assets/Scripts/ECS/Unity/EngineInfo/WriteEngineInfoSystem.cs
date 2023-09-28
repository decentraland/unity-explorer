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
using System;
using Utility.Multithreading;

namespace ECS.Unity.EngineInfo
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WriteEngineInfoSystem : BaseUnityLoopSystem
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
            c.FrameNumber = (uint)(MultithreadingUtility.FrameCount - sceneStateProvider.EngineStartInfo.FrameNumber);
            c.TotalRuntime = (float)(DateTime.Now - sceneStateProvider.EngineStartInfo.Timestamp).TotalSeconds;

            ecsToCRDTWriter.PutMessage(sdkEntity, c);
        }
    }
}
