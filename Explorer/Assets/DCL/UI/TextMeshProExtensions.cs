using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using MVC;
using System;
using System.Text.RegularExpressions;
using System.Threading;
using TMPro;
using Utility;

namespace DCL.UI
{
    public static class TextMeshProExtensions
    {
        private const string DEFAULT_LINK_STYLE = "<color=#0000FF><u>{0}</u></color>";

        private static readonly Regex URL_REGEX = new (@"\bhttps://[^\s/$.?#].[^\s]*\b");

        /// <summary>
        ///     Puts copy written by another user — an event or place description — on a rich-text label. The value is
        ///     escaped before it is assigned, so a <c>&lt;link&gt;</c> the author smuggled in renders as literal text
        ///     instead of a clickable link to a destination of their choosing, and a <c>&lt;size&gt;</c> or
        ///     <c>&lt;color&gt;</c> cannot restyle the panel around it. Bare <c>https://</c> URLs are then linkified as
        ///     usual, and clicking one opens the external-URL consent prompt rather than the OS handler directly.
        /// </summary>
        /// <remarks>
        ///     This is the entry point for any user-written value — never <see cref="ConvertUrlsToClickeableLinks" /> on
        ///     a value already assigned to the label. Escaping and assigning in one step is what stops the two from
        ///     drifting apart as callers are added (SEC-084).
        /// </remarks>
        public static void SetAuthorTextWithClickeableLinks(this TMP_Text tmp, string? authorText)
        {
            tmp.text = RichTextSanitizer.Escape(authorText);

            // The token is read when a link is activated rather than here: by then the label is known to be alive, and
            // its destruction is what should close a prompt still waiting for an answer.
            tmp.ConvertUrlsToClickeableLinks(url => ShowExternalUrlPromptAsync(url, tmp.GetCancellationTokenOnDestroy()).Forget());
        }

        /// <summary>
        ///     Wraps every bare <c>https://</c> URL already on the label in styled, clickable <c>&lt;link&gt;</c> markup
        ///     and routes activations to <paramref name="onLinkClicked" />. The text is taken as-is, so markup in it
        ///     stays live — which is what trusted copy that authors its own <c>&lt;link=ID&gt;</c> targets relies on.
        ///     For a value a user wrote, call <see cref="SetAuthorTextWithClickeableLinks" /> instead.
        /// </summary>
        public static void ConvertUrlsToClickeableLinks(this TMP_Text tmp, Action<string> onLinkClicked,
            string style = DEFAULT_LINK_STYLE,
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

        private static async UniTaskVoid ShowExternalUrlPromptAsync(string url, CancellationToken ct)
        {
            try
            {
                await ViewDependencies.GlobalUIViews.ShowExternalUrlPromptAsync(URLAddress.FromString(url), ct);
            }
            catch (OperationCanceledException) { }
            catch (Exception exception)
            {
                ReportHub.LogException(exception, ReportCategory.UI);
            }
        }
    }
}
