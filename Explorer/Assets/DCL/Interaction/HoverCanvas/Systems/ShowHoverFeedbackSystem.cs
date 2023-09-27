using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.ECSComponents;
using DCL.Interaction.PlayerOriginated.Components;
using DCL.Interaction.PlayerOriginated.Systems;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;
using Utility.UIToolkit;

namespace DCL.Interaction.HoverCanvas.Systems
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(ProcessPointerEventsSystem))]
    public partial class ShowHoverFeedbackSystem : BaseUnityLoopSystem
    {
        private readonly UI.HoverCanvas hoverCanvasInstance;

        private readonly Dictionary<InputAction, HoverCanvasSettings.InputButtonSettings> inputButtonSettingsMap;

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
            SetTooltipsQuery(World);
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
            }
        }
    }
}
