using System;
using System.Text.RegularExpressions;
using TMPro;
using Utility;

namespace DCL.UI
{
    public static class TextMeshProExtensions
    {
        private static readonly Regex URL_REGEX = new (@"\bhttps://[^\s/$.?#].[^\s]*\b");

        public static void ConvertUrlsToClickeableLinks(this TMP_Text tmp, Action<string> onLinkClicked,
            string style = "<color=#0000FF><u>{0}</u></color>",
            bool clearHookedEvents = true)
        {
            TMP_Text_ClickeableLink clickeableLink = tmp.gameObject.TryAddComponent<TMP_Text_ClickeableLink>();

            if (clearHookedEvents)
                clickeableLink.ClearHookedEvents();

            tmp.text = URL_REGEX.Replace(tmp.text, match =>
            {
                string url = match.Value;
                return string.Format(style, $"<link={url}>{url}</link>");
            });

            clickeableLink.OnLinkClicked += onLinkClicked;
        }
    }
}
