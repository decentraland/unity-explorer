using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera.Components;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;
using Utility.UIToolkit;
using ProcessPointerEventsSystem = DCL.Interaction.Systems.ProcessPointerEventsSystem;

namespace DCL.Interaction.HoverCanvas.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProcessPointerEventsSystem))]
    public partial class ShowHoverFeedbackSystem : BaseUnityLoopSystem
    {
        private readonly UI.HoverCanvas hoverCanvasInstance;
        private readonly Dictionary<InputAction, HoverCanvasSettings.InputButtonSettings> inputButtonSettingsMap;
        private Vector2 cursorPositionPercent;

        internal ShowHoverFeedbackSystem(World world, UI.HoverCanvas hoverCanvasInstance,
            IReadOnlyList<HoverCanvasSettings.InputButtonSettings> settings) : base(world)
        {
            this.hoverCanvasInstance = hoverCanvasInstance;

            inputButtonSettingsMap = new Dictionary<InputAction, HoverCanvasSettings.InputButtonSettings>(settings.Count);

            foreach (HoverCanvasSettings.InputButtonSettings inputButtonSettings in settings)
                inputButtonSettingsMap.Add(inputButtonSettings.InputAction, inputButtonSettings);
        }

        protected override void Update(float t)
        {
            GetCursorStateQuery(World);
            SetTooltipsQuery(World);
        }

        [Query]
        private void GetCursorState(in CursorComponent cursorComponent)
        {
            if (!cursorComponent.CursorIsLocked)
            {
                // 0,0 is the center of the screen
                cursorPositionPercent.x = (cursorComponent.Position.x - (Screen.width * 0.5f)) / Screen.width * 100f;
                cursorPositionPercent.y = (cursorComponent.Position.y - (Screen.height * 0.5f)) / Screen.height * 100f;
            }
            else
                cursorPositionPercent = Vector2Int.zero;
        }

        [Query]
        private void SetTooltips(ref HoverFeedbackComponent hoverFeedbackComponent)
        {
            hoverCanvasInstance.SetDisplayed(hoverFeedbackComponent.Enabled);

            if (hoverFeedbackComponent.Enabled)
            {
                for (var i = 0; i < hoverFeedbackComponent.Tooltips.Count; i++)
                {
                    HoverFeedbackComponent.Tooltip tooltipInfo = hoverFeedbackComponent.Tooltips[i];

                    string actionKeyText = null;
                    Sprite icon = null;

                    if (inputButtonSettingsMap.TryGetValue(tooltipInfo.Action, out HoverCanvasSettings.InputButtonSettings settings))
                    {
                        actionKeyText = settings.Key;
                        icon = settings.Icon;
                    }

                    hoverCanvasInstance.SetTooltip(tooltipInfo.Text, actionKeyText, icon, i);
                }

                hoverCanvasInstance.SetTooltipsCount(hoverFeedbackComponent.Tooltips.Count);
                hoverCanvasInstance.SetPosition(cursorPositionPercent);
            }
        }
    }
}
