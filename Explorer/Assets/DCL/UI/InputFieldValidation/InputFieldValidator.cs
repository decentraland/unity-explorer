using System;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.UI.InputFieldValidator
{
    /// <summary>
    /// This is a validator that is called automatically by the TMP_InputField (if correctly configured) under certain circumstances.
    /// It needs to inherit from TMP_InputValidator and have a Validate method
    /// Validate will only be called by TMP_InputField when appending characters to the input, not when removing or setting the text
    /// That's where the backspace validation is used. Also, this adds the required rich text tags to the text when detecting hyperlinks of
    /// different types. And finally, this also filters invalid rich text tags that should not be admitted.
    /// </summary>
    [CreateAssetMenu(fileName = "InputFieldValidator", menuName = "DCL/UI/InputFieldValidator")]
    public class InputFieldValidator : TMP_InputValidator
    {
        private const string TAG_STRING = "§";
        private const char TAG_CHAR = '§';
        private const string SCENE = "scene";
        private const string WORLD = "world";
        private const string URL = "url";
        private const string USER = "user";

        private static readonly Regex RICH_TEXT_TAG_REGEX = new (@"<(?!\/?(b|i)(>|\s))[^>]+>", RegexOptions.Compiled);
        private static readonly Regex LINK_TAG_REGEX = new (
            @"[<‹]#[0-9A-Fa-f]{6}[>›][<‹]link=(url|scene|world|user)=.*?[>›](.*?)[<‹]/link[>›][<‹]/color[>›]",
            RegexOptions.Compiled);
        private static readonly Regex WEBSITE_REGEX = new (
            @"(?<=^|\s)§?((https§?:\/\/)?§?(www\.)?§?[a-zA-Z0-9]§?(?:[a-zA-Z0-9-]*§?[a-zA-Z0-9]*)?§?\.§?[a-zA-Z]{2,30}§?[a-zA-Z]{0,33}§?(\/[^\s]*)?)§?(?=\s|$)",
            RegexOptions.Compiled);
        private static readonly Regex SCENE_REGEX = new (@"(?<=^|\s)-?§?\d{0,1}§?\d{0,1}§?\d{1}§?,§?-?§?\d{1}§?\d{0,1}§?\d{0,1}§?(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex WORLD_REGEX = new (@"(?<=^|\s)§?[a-zA-Z0-9]§?[a-zA-Z0-9]*§?[a-zA-Z0-9]*§?\.dcl\.eth§?(?=\s|$)", RegexOptions.Compiled);

        private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)([A-Za-z0-9]*?)@([A-Za-z0-9]{3,15}§?(?:#[A-Za-z0-9]{4})?)§?(?=\s|$)", RegexOptions.Compiled);
        //TODO FRAN URGENT!: We need to remove the hash from the username! we will check it in the hyperlink handler comparing the username to the connected users (similar to the parsing done to get the suggestions)
        //private static readonly Regex USERNAME_REGEX = new (@"(?<=^|\s)§?@§?[A-Za-z0-9]{3,15}(?:#[A-Za-z0-9]{4})?§?(?=\s|$)", RegexOptions.Compiled);

        [SerializeField] private TMP_StyleSheet styleSheet;

        private readonly StringBuilder mainStringBuilder = new ();
        private readonly StringBuilder tempStringBuilder = new ();
        private string linkClosingStyle;

        private string linkOpeningStyle;

        public void InitializeStyles()
        {
            TMP_Style style = styleSheet.GetStyle("Link");
            linkOpeningStyle = style.styleOpeningDefinition + "<link=";
            linkClosingStyle = "</link>" + style.styleClosingDefinition;
        }

        public void Validate(ref string text, ref int pos)
        {
            if (text.Length == 0)
                return;

            PerformValidation(ref text, ref pos);
        }

        public override char Validate(ref string text, ref int pos, char ch) =>
            PerformValidation(ref text, ref pos, ch);

        private char PerformValidation(ref string text, ref int pos, char ch = default)
        {
            mainStringBuilder.Clear();
            mainStringBuilder.Append(text.AsSpan(0, pos));

            if (ch != default(int))
                mainStringBuilder.Append(ch);

            mainStringBuilder.Append(TAG_STRING);

            mainStringBuilder.Append(text.AsSpan(pos));

            if (!text.StartsWith("/"))
                ProcessMainStringBuilder();

            pos = GetPositionFromTag(mainStringBuilder);
            text = mainStringBuilder.Remove(pos, 1).ToString();
            return ch;
        }

        private int GetPositionFromTag(StringBuilder stringBuilder)
        {
            int length = stringBuilder.Length;

            for (var i = 0; i < length; i++)
                if (stringBuilder[i] == TAG_CHAR)
                    return i;

            return 0;
        }

        private void ProcessMainStringBuilder()
        {
            RemoveLinkTags(mainStringBuilder);
            ReplaceMatches(RICH_TEXT_TAG_REGEX, mainStringBuilder, ReplaceRichTextTags);
            ReplaceMatches(WEBSITE_REGEX, mainStringBuilder, WrapWithUrlLink);
            ReplaceMatches(SCENE_REGEX, mainStringBuilder, WrapWithSceneLink);
            ReplaceMatches(WORLD_REGEX, mainStringBuilder, WrapWithWorldLink);
            ReplaceMatches(USERNAME_REGEX, mainStringBuilder, WrapWithUsernameLink);
        }

        private void RemoveLinkTags(StringBuilder stringBuilder)
        {
            var text = stringBuilder.ToString();
            MatchCollection matches = LINK_TAG_REGEX.Matches(text);

            if (matches.Count <= 0) return;

            stringBuilder.Clear();
            stringBuilder.Append(text.AsSpan(0, matches[0].Index));

            for (var i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                stringBuilder.Append(match.Groups[2]);

                if (i < matches.Count - 1)
                {
                    int nextStart = matches[i + 1].Index;
                    stringBuilder.Append(text.AsSpan(match.Index + match.Length, nextStart - (match.Index + match.Length)));
                }
            }

            stringBuilder.Append(text.AsSpan(matches[^1].Index + matches[^1].Length));
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
            string matchWithoutTag = match.Value.Replace(TAG_STRING, "");

            //Validate here if these are valid before creating the links
            switch (linkType)
            {
                case LinkType.SCENE:
                    string[] splitCords = matchWithoutTag.Split(',');

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
                    tempStringBuilder.Append(linkOpeningStyle)
                                     .Append(USER)
                                     .Append('=')
                                     .Append(match.Groups[1].Value)
                                     .Append(">")
                                     .Append("@" + match.Groups[2].Value)
                                     .Append(linkClosingStyle);
                    return tempStringBuilder;
            }

            tempStringBuilder.Append(linkOpeningStyle)
                             .Append(linkTypeString)
                             .Append('=')
                             .Append(matchWithoutTag)
                             .Append(">")
                             .Append(match)
                             .Append(linkClosingStyle);

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
