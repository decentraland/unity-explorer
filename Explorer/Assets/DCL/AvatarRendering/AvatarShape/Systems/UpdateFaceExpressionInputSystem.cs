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
using System.Collections.Generic;
using UnityEngine.InputSystem;
using InputAction = UnityEngine.InputSystem.InputAction;

namespace DCL.AvatarRendering.AvatarShape
{
    /// <summary>
    ///     Listens to the Y + 1-9 keyboard shortcuts and applies the corresponding face expression
    ///     to the local player's avatar. Mirrors the B + 1-9 emote shortcut pattern.
    ///     Y+1 → expression at index 0; Y+N → expression at index N-1.
    ///     If the requested index exceeds the configured expression list, expression 0 is used.
    /// </summary>
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateFaceExpressionInputSystem : BaseUnityLoopSystem
    {
        private const int SLOT_COUNT = 9;

        private readonly AvatarFaceExpressionDefinition[] expressions;
        private readonly Dictionary<string, int> slotByActionName = new (SLOT_COUNT);
        private readonly DCLInput.FaceExpressionsActions faceExpressionsActions;

        private int triggeredSlot = -1;

        internal UpdateFaceExpressionInputSystem(World world, AvatarFaceExpressionDefinition[] expressions) : base(world)
        {
            this.expressions = expressions;
            faceExpressionsActions = DCLInput.Instance.FaceExpressions;

            RegisterSlotCallbacks(faceExpressionsActions.Get());
        }

        protected override void OnDispose()
        {
            UnregisterSlotCallbacks(faceExpressionsActions.Get());
        }

        protected override void Update(float t)
        {
            // Ensure LocalPlayerFacialExpressionComponent exists before the expression query runs.
            SetupNetworkExpressionComponentQuery(World);

            // Sync any external writes to AvatarFaceExpressionComponent (e.g. debug menu) back to the
            // network bridge component so PlayerMovementNetSendSystem picks them up.
            SyncAvatarExpressionToNetworkQuery(World);

            if (triggeredSlot < 0)
                return;

            int expressionIndex = ResolveExpressionIndex(triggeredSlot);
            ApplyExpressionToPlayerQuery(World, expressionIndex);
            triggeredSlot = -1;
        }

        /// <summary>
        ///     Adds <see cref="LocalPlayerFacialExpressionComponent"/> to the player entity as soon as
        ///     <see cref="AvatarFaceExpressionComponent"/> is available (i.e. after avatar load).
        ///     Runs at most once per player entity thanks to the [None] filter.
        /// </summary>
        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceExpressionComponent))]
        [None(typeof(LocalPlayerFacialExpressionComponent), typeof(DeleteEntityIntention))]
        private void SetupNetworkExpressionComponent(in Entity entity)
        {
            World.Add(entity, new LocalPlayerFacialExpressionComponent());
        }

        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceExpressionComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyExpressionToPlayer([Data] int expressionIndex, ref AvatarFaceExpressionComponent expression, ref LocalPlayerFacialExpressionComponent networkExpression)
        {
            AvatarFaceExpressionDefinition def = expressions[expressionIndex];
            expression.EyebrowsExpressionIndex = def.EyebrowsIndex;
            expression.EyesExpressionIndex = def.EyesIndex;
            expression.MouthExpressionIndex = def.MouthIndex;
            expression.IsDirty = true;

            networkExpression.EyebrowsIndex = (byte)def.EyebrowsIndex;
            networkExpression.EyesIndex = (byte)def.EyesIndex;
            networkExpression.MouthIndex = (byte)def.MouthIndex;
        }

        /// <summary>
        ///     Every frame, mirrors the local player's <see cref="AvatarFaceExpressionComponent"/> into
        ///     <see cref="LocalPlayerFacialExpressionComponent"/> so that changes made outside the input
        ///     system (e.g. the debug menu) are also propagated over the network.
        ///     Negative sentinel values (NO_EYE_OVERRIDE / NO_MOUTH_POSE) are clamped to 0.
        /// </summary>
        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceExpressionComponent), typeof(LocalPlayerFacialExpressionComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void SyncAvatarExpressionToNetwork(in AvatarFaceExpressionComponent expression, ref LocalPlayerFacialExpressionComponent networkExpression)
        {
            byte eyebrows = (byte)Math.Max(0, expression.EyebrowsExpressionIndex);
            byte eyes = (byte)Math.Max(0, expression.EyesExpressionIndex);
            byte mouth = (byte)Math.Max(0, expression.MouthExpressionIndex);

            if (networkExpression.EyebrowsIndex == eyebrows &&
                networkExpression.EyesIndex == eyes &&
                networkExpression.MouthIndex == mouth)
                return;

            networkExpression.EyebrowsIndex = eyebrows;
            networkExpression.EyesIndex = eyes;
            networkExpression.MouthIndex = mouth;
        }

        private void OnSlotPerformed(InputAction.CallbackContext ctx)
        {
            triggeredSlot = slotByActionName[ctx.action.name];
        }

        /// <summary>
        ///     Converts a 1-based slot number to a 0-based expression index.
        ///     Falls back to 0 when the requested index is out of range.
        /// </summary>
        private int ResolveExpressionIndex(int slot)
        {
            if (expressions.Length == 0)
                return 0;

            int index = slot - 1;
            return index >= 0 && index < expressions.Length ? index : 0;
        }

        private void RegisterSlotCallbacks(InputActionMap actionMap)
        {
            for (var slot = 1; slot <= SLOT_COUNT; slot++)
            {
                string actionName = $"Slot {slot}";

                try
                {
                    InputAction action = actionMap.FindAction(actionName);
                    action.started += OnSlotPerformed;
                    slotByActionName[actionName] = slot;
                }
                catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
            }
        }

        private void UnregisterSlotCallbacks(InputActionMap actionMap)
        {
            for (var slot = 1; slot <= SLOT_COUNT; slot++)
            {
                try
                {
                    InputAction action = actionMap.FindAction($"Slot {slot}");
                    action.started -= OnSlotPerformed;
                }
                catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
            }
        }
    }
}
