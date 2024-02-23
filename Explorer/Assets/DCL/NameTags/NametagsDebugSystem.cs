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
    [LogCategory(ReportCategory.AVATAR)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class NametagsDebugSystem : BaseUnityLoopSystem
    {
        private readonly NametagsData nametagsData;

        public NametagsDebugSystem(World world, IDebugContainerBuilder debugBuilder,NametagsData nametagsData) : base(world)
        {
            this.nametagsData = nametagsData;

            debugBuilder.AddWidget("Nametags")
                        .AddToggleField("ShowNametags", OnShowNametagsToggled, nametagsData.showNameTags);
        }

        private void OnShowNametagsToggled(ChangeEvent<bool> evt)
        {
            nametagsData.showNameTags = evt.newValue;
        }

        protected override void Update(float t)
        {

        }
    }
}
