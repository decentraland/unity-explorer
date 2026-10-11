using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class WritePlayerVoiceStateSystem : BaseUnityLoopSystem
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;

        internal WritePlayerVoiceStateSystem(World world, IECSToCRDTWriter ecsToCRDTWriter) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            UpdateVoiceStateQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateVoiceState(in PlayerSceneCRDTEntity crdtEntity, in PlayerVoiceStateComponent voiceState)
        {
            if (!voiceState.IsDirty) return;

            ecsToCRDTWriter.PutMessage<PBPlayerVoiceState, bool>(static (pbComponent, isSpeaking) => pbComponent.IsSpeaking = isSpeaking, crdtEntity.CRDTEntity, voiceState.IsSpeaking);
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PlayerVoiceStateComponent))]
        private void HandleComponentRemoval(in PlayerSceneCRDTEntity crdtEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBPlayerVoiceState>(crdtEntity.CRDTEntity);
        }
    }
}
