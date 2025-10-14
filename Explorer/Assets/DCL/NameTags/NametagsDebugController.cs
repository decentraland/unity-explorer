using DCL.DebugUtilities;
using UnityEngine.UIElements;

namespace DCL.Nametags
{
    public class NametagsDebugController
    {
        private readonly NametagsData nametagsData;

        public NametagsDebugController(IDebugContainerBuilder debugBuilder, NametagsData nametagsData)
        {
            this.nametagsData = nametagsData;

            debugBuilder.TryAddWidget(IDebugContainerBuilder.Categories.NAMETAGS)
                       ?.AddToggleField("ShowNametags", OnShowNametagsToggled, nametagsData.showNameTags);
        }

        private void OnShowNametagsToggled(ChangeEvent<bool> evt)
        {
            nametagsData.showNameTags = evt.newValue;
        }
    }
}
