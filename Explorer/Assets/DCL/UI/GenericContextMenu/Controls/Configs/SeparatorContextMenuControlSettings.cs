
using DCL.UI.GenericContextMenuParameter;

namespace DCL.UI.GenericContextMenu.Controls.Configs
{
    public class SeparatorContextMenuControlSettings : IContextMenuControlSettings
    {
        internal readonly int height;
        internal readonly int leftPadding;
        internal readonly int rightPadding;

        public SeparatorContextMenuControlSettings(int height = 8, int leftPadding = 0, int rightPadding = 0)
        {
            this.height = height;
            this.leftPadding = leftPadding;
            this.rightPadding = rightPadding;
        }
    }
}
