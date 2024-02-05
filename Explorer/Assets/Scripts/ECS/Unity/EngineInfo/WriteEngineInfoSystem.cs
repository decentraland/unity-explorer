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
            ecsToCRDTWriter.PutMessage<PBEngineInfo, ISceneStateProvider>(static (component, provider) =>
            {
                component.TickNumber = provider.TickNumber;
                component.FrameNumber = (uint)(MultithreadingUtility.FrameCount - provider.EngineStartInfo.FrameNumber);
                component.TotalRuntime = (float)(DateTime.Now - provider.EngineStartInfo.Timestamp).TotalSeconds;
            }, sdkEntity, sceneStateProvider);
        }
    }
}
