using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using System;
using UnityEngine.UIElements;

namespace DCL.Nametags
{
    public class NametagsDebugController
    {
        private readonly NametagsData nametagsData;

        public NametagsDebugController(IDebugContainerBuilder debugBuilder,NametagsData nametagsData)
        {
            this.nametagsData = nametagsData;

            debugBuilder.AddWidget("Nametags")
                        .AddToggleField("ShowNametags", OnShowNametagsToggled, nametagsData.showNameTags);
        }

        private void OnShowNametagsToggled(ChangeEvent<bool> evt)
        {
            nametagsData.showNameTags = evt.newValue;
        }
    }
}
