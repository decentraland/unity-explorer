using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.FacialExpression;
using DCL.Character.Components;
using DCL.Diagnostics;
using DCL.FacialExpressionsWheel;
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
    ///     Listens to the FaceExpressions input map (Y+[0-9]) and writes a
    ///     <see cref="TriggerFacialExpressionIntent"/> on the local player. Notifies
    ///     <see cref="FacialExpressionsWheelShortcutHandler"/> so the upcoming Y release
    ///     doesn't toggle the wheel.
    /// </summary>
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(InputGroup))]
    public partial class UpdateFacialExpressionInputSystem : BaseUnityLoopSystem
    {
        private const int SLOT_COUNT = 10;

        private readonly AvatarFaceExpressionConfig config;
        private readonly FacialExpressionsWheelShortcutHandler shortcutHandler;
        private readonly DCLInput.FaceExpressionsActions actions;
        private readonly Dictionary<string, int> slotByActionName = new ();

        private int triggeredSlot = -1;

        internal UpdateFacialExpressionInputSystem(
            World world,
            AvatarFaceExpressionConfig config,
            FacialExpressionsWheelShortcutHandler shortcutHandler) : base(world)
        {
            this.config = config;
            this.shortcutHandler = shortcutHandler;
            actions = DCLInput.Instance.FaceExpressions;
            ListenToSlotsInput(actions.Get());
        }

        protected override void OnDispose()
        {
            UnregisterSlotsInput(actions.Get());
        }

        protected override void Update(float t)
        {
            if (triggeredSlot < 0) return;

            if (triggeredSlot < config.Expressions.Length)
            {
                ApplyFaceQuery(World, triggeredSlot);
                shortcutHandler.NotifyExpressionPlayed(FacialExpressionTriggerSource.SHORTCUT);
            }

            triggeredSlot = -1;
        }

        [Query]
        [All(typeof(PlayerComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void ApplyFace([Data] int slot, in Entity entity)
        {
            ref AvatarFaceExpressionDefinition def = ref config.Expressions[slot];

            World.AddOrGet(entity, new TriggerFacialExpressionIntent
            {
                EyebrowsIndex = (byte)def.EyebrowsIndex,
                EyesIndex = (byte)def.EyesIndex,
                MouthIndex = (byte)def.MouthIndex,
            });
        }

        private void OnSlotPerformed(InputAction.CallbackContext ctx) =>
            triggeredSlot = slotByActionName[ctx.action.name];

        private void ListenToSlotsInput(InputActionMap map)
        {
            for (var i = 0; i < SLOT_COUNT; i++)
            {
                string name = FacialExpressionWheelUtils.GetSlotActionName(i);

                try
                {
                    InputAction action = map.FindAction(name);
                    action.started += OnSlotPerformed;
                    slotByActionName[name] = FacialExpressionWheelUtils.SlotIndexFromActionName(name);
                }
                catch (Exception e) { ReportHub.LogException(e, GetReportData()); }
            }
        }

        private void UnregisterSlotsInput(InputActionMap map)
        {
            for (var i = 0; i < SLOT_COUNT; i++)
            {
                string name = FacialExpressionWheelUtils.GetSlotActionName(i);
                InputAction action = map.FindAction(name);
                action.started -= OnSlotPerformed;
            }
        }
    }
}
