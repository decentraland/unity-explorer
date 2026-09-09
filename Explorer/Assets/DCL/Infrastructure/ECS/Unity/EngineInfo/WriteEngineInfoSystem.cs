using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
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
        private readonly Func<bool> isLoadingScreenOn;

        internal WriteEngineInfoSystem(World world, ISceneStateProvider sceneStateProvider, IECSToCRDTWriter ecsToCRDTWriter, Func<bool> isLoadingScreenOn) : base(world)
        {
            this.sceneStateProvider = sceneStateProvider;
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.isLoadingScreenOn = isLoadingScreenOn;
        }

        public override void Initialize()
        {
            PropagateToScene();
        }

        protected override void Update(float t)
        {
            PropagateToScene();
        }

        private void PropagateToScene()
        {
            ecsToCRDTWriter.PutMessage<PBEngineInfo, (ISceneStateProvider provider, bool sceneHidden)>(static (component, data) =>
            {
                component.TickNumber = data.provider.TickNumber;
                component.FrameNumber = (uint)(MultithreadingUtility.FrameCount - data.provider.EngineStartInfo.FrameNumber);
                component.TotalRuntime = (float)(DateTime.Now - data.provider.EngineStartInfo.Timestamp).TotalSeconds;
                component.SceneHidden = data.sceneHidden;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, (sceneStateProvider, isLoadingScreenOn()));
        }
    }
}
