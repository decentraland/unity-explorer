using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.SDKComponents.RealmInfo
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class WriteRealmInfoSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        internal WriteRealmInfoSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        public override void Initialize()
        {
            // PropagateToScene();
        }

        protected override void Update(float t)
        {
            // PropagateToScene();
        }

        private void PropagateToScene()
        {
            /*ecsToCRDTWriter.PutMessage<PBEngineInfo, ISceneStateProvider>(static (component, provider) =>
            {
                component.TickNumber = provider.TickNumber;
                component.FrameNumber = (uint)(MultithreadingUtility.FrameCount - provider.EngineStartInfo.FrameNumber);
                component.TotalRuntime = (float)(DateTime.Now - provider.EngineStartInfo.Timestamp).TotalSeconds;
            }, SpecialEntitiesID.SCENE_ROOT_ENTITY, sceneStateProvider);*/
        }
    }
}
