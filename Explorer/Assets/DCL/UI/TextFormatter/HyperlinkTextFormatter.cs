using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.Utilities;
using System;
using System.Text;
using System.Text.RegularExpressions;
using Utility;

namespace DCL.UI.InputFieldFormatting
{
    public class HyperlinkTextFormatter : ITextFormatter
    {
        private const string LINK_OPENING_STYLE = "<#00B2FF><link=";
        private const string LINK_CLOSING_STYLE = "</link></color>";
        private const string OWN_PROFILE_OPENING_STYLE = "<#00B2FF>";
        private const string OWN_PROFILE_CLOSING_STYLE = "</color>";

        private static readonly Regex RICH_TEXT_TAG_REGEX = new (@"<(?!\/?(b|i)(>|\s))[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WEBSITE_REGEX = new (@"(?<=^|\s)(https?:\/\/)([a-zA-Z0-9-]+\.)*[a-zA-Z0-9-]+\.[a-zA-Z]{2,}(\/[^\s]*)?(?=\s|$)",
            RegexOptions.Compiled);
        private static readonly Regex SCENE_REGEX = new (@"(?<=^|\s)(-?\d{1,3}),(-?\d{1,3})(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);
        private static readonly Regex WORLD_REGEX = new (@"(?<=^|\s)*[a-zA-Z0-9]*\.dcl\.eth(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);
        // This Regex will detect any pattern of format @username#1234 being the part with the "#" optional. This requires the username to start and/or end with an empty space or start/end of line.
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|!|\?|\.|,|$)", RegexOptions.Compiled);

        private readonly StringBuilder mainStringBuilder = new ();
        private readonly StringBuilder tempStringBuilder = new ();
        private readonly IProfileCache profileCache;
        private readonly SelfProfile selfProfile;
        private readonly Func<Match, StringBuilder> replaceRichTextTags;
        private readonly Func<Match, StringBuilder> wrapWithUrlLink;
        private readonly Func<Match, StringBuilder> wrapWithSceneLink;
        private readonly Func<Match, StringBuilder> wrapWithWorldLink;
        private readonly Func<Match, StringBuilder> wrapWithUsernameLink;

        public HyperlinkTextFormatter(IProfileCache profileCache, SelfProfile selfProfile)
        {
            this.profileCache = profileCache;
            this.selfProfile = selfProfile;
            this.wrapWithUrlLink = WrapWithUrlLink;
            this.replaceRichTextTags = ReplaceRichTextTags;
            this.wrapWithUsernameLink = WrapWithUsernameLink;
            this.wrapWithSceneLink = WrapWithSceneLink;
            this.wrapWithWorldLink = WrapWithWorldLink;
        }

        public string FormatText(string text)
        {
            if (text.Length == 0)
                return text;

            if (text.StartsWith("/"))
                return text;

            mainStringBuilder.Clear();
            mainStringBuilder.Append(text);

            ProcessMainStringBuilder();

            return mainStringBuilder.ToString();
        }

        private void ProcessMainStringBuilder()
        {
            ReplaceMatches(RICH_TEXT_TAG_REGEX, mainStringBuilder, replaceRichTextTags);
            ReplaceMatches(WEBSITE_REGEX, mainStringBuilder, wrapWithUrlLink);
            ReplaceMatches(SCENE_REGEX, mainStringBuilder, wrapWithSceneLink);
            ReplaceMatches(WORLD_REGEX, mainStringBuilder, wrapWithWorldLink);
            ReplaceMatches(USERNAME_REGEX, mainStringBuilder, wrapWithUsernameLink);
        }

        private StringBuilder ReplaceRichTextTags(Match match)
        {
            tempStringBuilder.Clear();

            for (var i = 0; i < match.Value.Length; i++)
            {
                char c = match.Value[i];

                tempStringBuilder.Append(c == '<' ? '‹' : c == '>' ? '›' : c);
            }

            return tempStringBuilder;
        }

        private void ReplaceMatches(Regex regex, StringBuilder stringBuilder, Func<Match, StringBuilder> evaluator)
        {
            var text = stringBuilder.ToString();
            MatchCollection matches = regex.Matches(text);

            if (matches.Count == 0)
                return;

            stringBuilder.Clear();
            stringBuilder.Append(text.AsSpan(0, matches[0].Index));

            for (var i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                stringBuilder.Append(evaluator(match));

                if (i < matches.Count - 1)
                {
                    int nextStart = matches[i + 1].Index;
                    stringBuilder.Append(text.AsSpan(match.Index + match.Length, nextStart - (match.Index + match.Length)));
                }
            }

            stringBuilder.Append(text.AsSpan(matches[^1].Index + matches[^1].Length));
        }

        private StringBuilder WrapWithUrlLink(Match match) =>
            WrapWithLink(match, LinkType.URL);

        private StringBuilder WrapWithSceneLink(Match match) =>
            WrapWithLink(match, LinkType.SCENE);

        private StringBuilder WrapWithWorldLink(Match match) =>
            WrapWithLink(match, LinkType.WORLD);

        private StringBuilder WrapWithUsernameLink(Match match) =>
            WrapWithLink(match, LinkType.PROFILE);

        private StringBuilder WrapWithLink(Match match, LinkType linkType)
        {
            tempStringBuilder.Clear();

            string linkTypeString = null;

            switch (linkType)
            {
                case LinkType.SCENE:
                    if (!AreCoordsValid(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value)))
                        return tempStringBuilder.Append(match);

                    linkTypeString = HyperlinkConstants.SCENE;
                    break;
                case LinkType.WORLD:
                    linkTypeString = HyperlinkConstants.WORLD;
                    break;
                case LinkType.URL:
                    linkTypeString = HyperlinkConstants.URL;
                    break;
                case LinkType.PROFILE:
                    string username = match.Groups[1].Value;
                    if (IsOwnUsername(username))
                    {
                        tempStringBuilder.Append(OWN_PROFILE_OPENING_STYLE)
                                         .Append(match)
                                         .Append(OWN_PROFILE_CLOSING_STYLE);
                        return tempStringBuilder;
                    }

                    if (!IsUserNameValid(username))
                        return tempStringBuilder.Append(match);

                    linkTypeString = HyperlinkConstants.PROFILE;
                    break;
            }

            tempStringBuilder.Append(LINK_OPENING_STYLE)
                             .Append(linkTypeString)
                             .Append(">")
                             .Append(match)
                             .Append(LINK_CLOSING_STYLE);

            return tempStringBuilder;
        }

        private bool AreCoordsValid(int x, int y) =>
            GenesisCityData.IsInsideBounds(x, y);

        private bool IsOwnUsername(string username) =>
            selfProfile.OwnProfile?.DisplayName == username;

        private bool IsUserNameValid(string username) =>
            profileCache.GetByUserName(username) != null;

        private enum LinkType
        {
            SCENE,
            WORLD,
            URL,
            PROFILE,
        }
    }
}
