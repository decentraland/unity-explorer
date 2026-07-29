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

        private SceneRestrictionsAction lastBusMessageAction = SceneRestrictionsAction.Removed;

        // Tracks whether this scene currently asserts any input modifier on the shared global player.
        // Kept separate from lastBusMessageAction because the movement-restriction bus intentionally
        // ignores gliding/double-jump, but the global reset must still fire for them.
        private bool sceneAssertedModifiers;

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

            ApplyModifiersQuery(World, false);
            HandleComponentRemovalQuery(World);
        }

        private void SendBusMessage(in InputModifierComponent inputModifier)
        {
            SceneRestrictionsAction currentAction = inputModifier is { DisableAll: false, DisableWalk: false, DisableJog: false, DisableRun: false, DisableJump: false, DisableEmote: false } ? SceneRestrictionsAction.Removed : SceneRestrictionsAction.Applied;

            if (currentAction == lastBusMessageAction) return;

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateAvatarMovementsBlocked(currentAction));
            lastBusMessageAction = currentAction;
        }

        private void ResetModifiers()
        {
            if (!globalWorld.Has<InputModifierComponent>(playerEntity)) return;

            ref InputModifierComponent inputModifier = ref globalWorld.Get<InputModifierComponent>(playerEntity);
            inputModifier.RemoveAllModifiers();
            sceneAssertedModifiers = false;

            SendBusMessage(inputModifier);
        }

        [Query]
        private void ApplyModifiers([Data] bool skipDirtyCheck, Entity entity, in PBInputModifier pbInputModifier, in CRDTEntity crdtEntity)
        {
            if (crdtEntity.Id != SpecialEntitiesID.PLAYER_ENTITY
                || pbInputModifier.ModeCase == PBInputModifier.ModeOneofCase.None
                || (!skipDirtyCheck && !pbInputModifier.IsDirty)) return;

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
                inputModifier.DisableDoubleJump = pb.DisableDoubleJump;
                inputModifier.DisableGliding = pb.DisableGliding;
            }

            sceneAssertedModifiers = !inputModifier.EverythingEnabled;

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
            if (value)
                ApplyModifiersQuery(World, true);

            // Only reset the shared global modifier if this scene was the one actively asserting it.
            else if (sceneAssertedModifiers)
                ResetModifiers();
        }

        public void FinalizeComponents(in Query query)
        {
            // Only reset the shared global modifier if this scene was the one actively asserting it.
            if (sceneAssertedModifiers)
                ResetModifiers();
        }
    }
}
