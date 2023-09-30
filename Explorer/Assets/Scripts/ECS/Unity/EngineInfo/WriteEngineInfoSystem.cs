using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Special;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using SceneRunner.Scene;
using System;
using Utility.Multithreading;

namespace ECS.Unity.EngineInfo
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WriteEngineInfoSystem : BaseUnityLoopSystem
    {
        private static readonly PBEngineInfo ENGINE_INFO_SHARED = new ();

        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        internal WriteEngineInfoSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            PropagateToSceneQuery(World);
        }

        [Query]
        [All(typeof(SceneRootComponent))]
        private void PropagateToScene(ref CRDTEntity sdkEntity)
        {
            ENGINE_INFO_SHARED.TickNumber = sceneStateProvider.TickNumber;
            ENGINE_INFO_SHARED.FrameNumber = (uint)(MultithreadingUtility.FrameCount - sceneStateProvider.EngineStartInfo.FrameNumber);
            ENGINE_INFO_SHARED.TotalRuntime = (float)(DateTime.Now - sceneStateProvider.EngineStartInfo.Timestamp).TotalSeconds;

            ecsToCRDTWriter.PutMessage(sdkEntity, ENGINE_INFO_SHARED);
        }
    }
}
