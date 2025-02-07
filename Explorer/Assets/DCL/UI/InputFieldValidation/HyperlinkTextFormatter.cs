using System;
using System.Text;
using System.Text.RegularExpressions;
using Utility;

namespace DCL.UI.InputFieldValidator
{
    public interface ITextFormatter
    {
        string FormatText(string text);
    }

    [Serializable]
    public class HyperlinkTextFormatter : ITextFormatter
    {
        private const string SCENE = "scene";
        private const string WORLD = "world";
        private const string URL = "url";
        private const string USER = "user";
        private const string LINK_CLOSING_STYLE = "<#00B2FF><link=";
        private const string LINK_OPENING_STYLE = "</link></color>";


        private static readonly Regex RICH_TEXT_TAG_REGEX = new (@"<(?!\/?(b|i)(>|\s))[^>]+>", RegexOptions.Compiled);
        private static readonly Regex WEBSITE_REGEX = new (@"(?<=^|\s)((https?:\/\/)?(www\.)?[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9]*)?\.[a-zA-Z]{2,30}[a-zA-Z]{0,33}(\/[^\s]*)?)(?=\s|$)",
            RegexOptions.Compiled);
        private static readonly Regex SCENE_REGEX = new (@"(?<=^|\s)-?\d{1,3},-?\d{1,3}(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex WORLD_REGEX = new (@"(?<=^|\s)*[a-zA-Z0-9]*\.dcl\.eth§?(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)@([A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?)(?=\s|$)", RegexOptions.Compiled);

        //TODO FRAN URGENT!: We need to remove the hash from the username! we will check it in the hyperlink handler comparing the username to the connected users (similar to the parsing done to get the suggestions)
        //private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)§?@§?[A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?§?(?=\s|$)", RegexOptions.Compiled);

        private readonly StringBuilder mainStringBuilder = new ();
        private readonly StringBuilder tempStringBuilder = new ();

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
            ReplaceMatches(RICH_TEXT_TAG_REGEX, mainStringBuilder, ReplaceRichTextTags);
            ReplaceMatches(WEBSITE_REGEX, mainStringBuilder, WrapWithUrlLink);
            ReplaceMatches(SCENE_REGEX, mainStringBuilder, WrapWithSceneLink);
            ReplaceMatches(WORLD_REGEX, mainStringBuilder, WrapWithWorldLink);
            ReplaceMatches(USERNAME_REGEX, mainStringBuilder, WrapWithUsernameLink);
        }

        private StringBuilder ReplaceRichTextTags(Match match)
        {
            tempStringBuilder.Clear();

            for (var i = 0; i < match.Value.Length; i++)
            {
                char c = match.Value[i];

                tempStringBuilder.Append(c == '<' ? '‹' :
                    c == '>' ? '›' : c);
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
            WrapWithLink(match, LinkType.USER);

        private StringBuilder WrapWithLink(Match match, LinkType linkType)
        {
            tempStringBuilder.Clear();
            string linkTypeString = string.Empty;

            //Validate here if these are valid before creating the links
            switch (linkType)
            {
                case LinkType.SCENE:
                    //TODO: Maybe we can make the match automatically split in 2 groups when detecting this? so we dont need additional string works?
                    string[] splitCords = match.Value.Split(',');

                    if (splitCords.Length != 2 ||
                        !AreCoordsValid(int.Parse(splitCords[0]), int.Parse(splitCords[1])))
                        return tempStringBuilder.Append(match);

                    linkTypeString = SCENE;
                    break;
                case LinkType.WORLD:
                    linkTypeString = WORLD;
                    break;
                case LinkType.URL:
                    linkTypeString = URL;
                    break;
                case LinkType.USER:
                    tempStringBuilder.Append(LINK_OPENING_STYLE)
                                     .Append(USER)
                                     .Append('=')
                                     .Append(match.Groups[1].Value)
                                     .Append(">")
                                     .Append("@" + match.Groups[2].Value)
                                     .Append(LINK_CLOSING_STYLE);

                    return tempStringBuilder;
            }

            tempStringBuilder.Append(LINK_OPENING_STYLE)
                             .Append(linkTypeString)
                             .Append('=')
                             .Append(match)
                             .Append(">")
                             .Append(match)
                             .Append(LINK_CLOSING_STYLE);

            return tempStringBuilder;
        }

        private bool AreCoordsValid(int x, int y) =>
            GenesisCityData.IsInsideBounds(x, y);

        private enum LinkType
        {
            SCENE,
            WORLD,
            URL,
            USER,
        }
    }
}
