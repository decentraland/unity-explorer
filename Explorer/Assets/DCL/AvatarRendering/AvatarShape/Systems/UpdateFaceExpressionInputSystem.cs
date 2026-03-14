using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.Input;
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
            if (triggeredSlot < 0)
                return;

            int expressionIndex = ResolveExpressionIndex(triggeredSlot);
            ApplyExpressionToPlayerQuery(World, expressionIndex);
            triggeredSlot = -1;
        }

        [Query]
        [All(typeof(PlayerComponent), typeof(AvatarFaceExpressionComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyExpressionToPlayer([Data] int expressionIndex, ref AvatarFaceExpressionComponent expression)
        {
            AvatarFaceExpressionDefinition def = expressions[expressionIndex];
            expression.EyebrowsExpressionIndex = def.EyebrowsIndex;
            expression.EyesExpressionIndex = def.EyesIndex;
            expression.MouthExpressionIndex = def.MouthIndex;
            expression.IsDirty = true;
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
