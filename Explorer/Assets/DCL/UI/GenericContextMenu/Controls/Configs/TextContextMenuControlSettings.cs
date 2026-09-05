namespace DCL.UI.Controls.Configs
{
    public class TextContextMenuControlSettings : IContextMenuControlSettings
    {
        public string Text { get; }

        public TextContextMenuControlSettings(string text)
        {
            Text = text;
        }
    }
}
