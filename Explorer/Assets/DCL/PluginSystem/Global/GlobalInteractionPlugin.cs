using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using DCL.Interaction.Utility;
using System.Collections.Generic;

namespace DCL.PluginSystem.Global
{
    public class GlobalInteractionPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly DCLInput dclInput;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;

        public GlobalInteractionPlugin(DCLInput dclInput, IEntityCollidersGlobalCache entityCollidersGlobalCache)
        {
            this.dclInput = dclInput;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            var playerInteractionEntity = new PlayerInteractionEntity(
                builder.World.Create(new PlayerOriginRaycastResult(), new HoverStateComponent(), new HoverFeedbackComponent()),
                builder.World);

            PlayerOriginatedRaycastSystem.InjectToWorld(ref builder, dclInput.Camera.Point, entityCollidersGlobalCache,
                playerInteractionEntity, 100f);

            DCLInput.PlayerActions playerInput = dclInput.Player;

            // TODO How to add FORWARD/BACKWARD/LEFT/RIGHT properly?
            ProcessPointerEventsSystem.InjectToWorld(ref builder,
                new Dictionary<InputAction, UnityEngine.InputSystem.InputAction>
                {
                    { InputAction.IaPointer, playerInput.Pointer },
                    { InputAction.IaPrimary, playerInput.Primary },
                    { InputAction.IaSecondary, playerInput.Secondary },
                    { InputAction.IaJump, playerInput.Jump },
                    { InputAction.IaAction3, playerInput.ActionButton3 },
                    { InputAction.IaAction4, playerInput.ActionButton4 },
                    { InputAction.IaAction5, playerInput.ActionButton5 },
                    { InputAction.IaAction6, playerInput.ActionButton6 },
                },
                entityCollidersGlobalCache);
        }
    }
}
