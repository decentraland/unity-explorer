using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Input;
using DCL.Multiplayer.Movement;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using System;
using UnityEngine.InputSystem;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Listens to the Y + 1-9 shortcut and applies the corresponding face expression to the local
    ///     player's avatar (mirrors the emote slot pattern). Writes both
    ///     <see cref="AvatarFaceComponent"/> (drives rendering) and
    ///     <see cref="LocalPlayerFacialExpressionComponent"/> (drives the network send).
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    [LogCategory(ReportCategory.AVATAR)]
    public partial class UpdateFaceExpressionInputSystem : BaseUnityLoopSystem
    {
        private const int SLOT_COUNT = 9;

        private readonly AvatarFaceExpressionDefinition[] expressions;
        private readonly DCLInput.FaceExpressionsActions faceExpressionsActions;
        private readonly Action<InputAction.CallbackContext>[] slotCallbacks = new Action<InputAction.CallbackContext>[SLOT_COUNT];

        // One-frame buffer for the input event. Set on the input thread by the slot callback,
        // consumed in Update. Same shape as UpdateEmoteInputSystem.triggeredEmote.
        private int pendingExpressionIndex = -1;

        internal UpdateFaceExpressionInputSystem(World world, AvatarFaceExpressionDefinition[] expressions) : base(world)
        {
            this.expressions = expressions;
            faceExpressionsActions = DCLInput.Instance.FaceExpressions;

            InputActionMap actionMap = faceExpressionsActions.Get();

            for (var i = 0; i < SLOT_COUNT; i++)
            {
                int expressionIndex = i;
                slotCallbacks[i] = _ => pendingExpressionIndex = expressionIndex;

                try
                {
                    InputAction action = actionMap.FindAction($"Slot {i + 1}");
                    action.started += slotCallbacks[i];
                }
                catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
            }
        }

        protected override void OnDispose()
        {
            InputActionMap actionMap = faceExpressionsActions.Get();

            for (var i = 0; i < SLOT_COUNT; i++)
            {
                try
                {
                    InputAction action = actionMap.FindAction($"Slot {i + 1}");
                    action.started -= slotCallbacks[i];
                }
                catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
            }
        }

        protected override void Update(float t)
        {
            // Ensure LocalPlayerFacialExpressionComponent exists before the apply query runs.
            SetupNetworkExpressionComponentQuery(World);

            if (pendingExpressionIndex < 0)
                return;

            // Out of range (no expression configured for this slot) — drop the input.
            if (pendingExpressionIndex >= expressions.Length)
            {
                pendingExpressionIndex = -1;
                return;
            }

            ApplyExpressionToPlayerQuery(World, pendingExpressionIndex);
            pendingExpressionIndex = -1;
        }

        /// <summary>
        ///     Adds <see cref="LocalPlayerFacialExpressionComponent"/> to the player entity as soon as
        ///     <see cref="AvatarFaceComponent"/> is available (i.e. after avatar load). Runs at most
        ///     once per player entity thanks to the [None] filter.
        /// </summary>
        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceComponent))]
        [None(typeof(LocalPlayerFacialExpressionComponent), typeof(DeleteEntityIntention))]
        private void SetupNetworkExpressionComponent(in Entity entity)
        {
            World.Add(entity, new LocalPlayerFacialExpressionComponent());
        }

        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyExpressionToPlayer(
            [Data] int expressionIndex,
            ref AvatarFaceComponent face,
            ref LocalPlayerFacialExpressionComponent network)
        {
            AvatarFaceExpressionDefinition def = expressions[expressionIndex];

            face.EyebrowsExpressionIndex = def.EyebrowsIndex;
            face.EyesExpressionIndex = def.EyesIndex;
            face.MouthExpressionIndex = def.MouthIndex;
            face.IsDirty = true;

            network.EyebrowsIndex = (byte)def.EyebrowsIndex;
            network.EyesIndex = (byte)def.EyesIndex;
            network.MouthIndex = (byte)def.MouthIndex;
        }
    }
}