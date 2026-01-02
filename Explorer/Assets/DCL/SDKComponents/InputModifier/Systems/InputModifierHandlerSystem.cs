using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SDKComponents.InputModifier.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using SceneRunner.Scene;

namespace DCL.SDKComponents.PlayerInputMovement.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class InputModifierHandlerSystem : BaseUnityLoopSystem, ISceneIsCurrentListener, IFinalizeWorldSystem
    {
        private readonly Entity playerEntity;
        private readonly World globalWorld;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;

        private SceneRestrictionsAction lastBusMessageAction = SceneRestrictionsAction.REMOVED;

        public InputModifierHandlerSystem(World world, World globalWorld, Entity playerEntity, ISceneStateProvider sceneStateProvider, ISceneRestrictionBusController sceneRestrictionBusController) : base(world)
        {
            this.playerEntity = playerEntity;
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorld = globalWorld;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
        }

        protected override void Update(float t)
        {
            if (!sceneStateProvider.IsCurrent) return;

            ApplyModifiersQuery(World);
            HandleComponentRemovalQuery(World);
        }

        private void SendBusMessage(in InputModifierComponent inputModifier)
        {
            SceneRestrictionsAction currentAction = inputModifier is { DisableAll: false, DisableWalk: false, DisableJog: false, DisableRun: false, DisableJump: false, DisableEmote: false } ? SceneRestrictionsAction.REMOVED : SceneRestrictionsAction.APPLIED;

            if (currentAction == lastBusMessageAction) return;

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarMovementsBlocked(currentAction));
            lastBusMessageAction = currentAction;
        }

        private void ResetModifiers()
        {
            ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
            inputModifier.DisableAll = false;
            inputModifier.DisableWalk = false;
            inputModifier.DisableJog = false;
            inputModifier.DisableRun = false;
            inputModifier.DisableJump = false;
            inputModifier.DisableEmote = false;

            SendBusMessage(inputModifier);
        }

        [Query]
        private void ApplyModifiers(Entity entity, in PBInputModifier pbInputModifier, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY
                || pbInputModifier.ModeCase == PBInputModifier.ModeOneofCase.None
                || !pbInputModifier.IsDirty) return;

            ref var inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
            PBInputModifier.Types.StandardInput? pb = pbInputModifier.Standard;

            bool disableAll = pb.DisableAll;
            inputModifier.DisableAll = disableAll;

            if (!disableAll)
            {
                inputModifier.DisableWalk = pb.DisableWalk;
                inputModifier.DisableJog = pb.DisableJog;
                inputModifier.DisableRun = pb.DisableRun;
                inputModifier.DisableJump = pb.DisableJump;
                inputModifier.DisableEmote = pb.DisableEmote;
            }

            SendBusMessage(inputModifier);

            // Mark scene Entity with component as well to know later when the PB component gets removed
            World.AddOrGet<InputModifierComponent>(entity);
        }

        [Query]
        [None(typeof(PBInputModifier))]
        [All(typeof(InputModifierComponent))]
        private void HandleComponentRemoval(Entity entity, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY)
                return;

            ResetModifiers();

            World.Remove<InputModifierComponent>(entity);
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            if (!value)
                ResetModifiers();
        }

        public void FinalizeComponents(in Query query)
        {
            ResetModifiers();
        }
    }
}
