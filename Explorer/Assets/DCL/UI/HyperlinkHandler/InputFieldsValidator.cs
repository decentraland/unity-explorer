using System;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using Utility;

namespace DCL.UI.InputFieldValidator
{
    [CreateAssetMenu(fileName = "InputFieldValidator", menuName = "DCL/UI/InputFieldValidator")]
    public class InputFieldsValidator : TMP_InputValidator
    {
        private const string TAG_STRING = "§";
        private const char TAG_CHAR = '§';
        private const string SCENE = "scene";
        private const string WORLD = "world";
        private const string URL = "url";
        private const string USER = "user";

        private static readonly Regex RICH_TEXT_TAG_REGEX = new (@"<(?!\/?(b|i)(>|\s))[^>]+>", RegexOptions.Compiled);
        private static readonly Regex LINK_TAG_REGEX = new (@"<#[0-9A-Fa-f]{6}><link=(url|scene|world|user)=.*?>(.*?)</link></color>", RegexOptions.Compiled);
        private static readonly Regex WEBSITE_REGEX = new (
            @"(?:^|\s)§?((http§?s?:\/\/)?§?(www\.)§?[a-zA-Z0-9]§?(?:[a-zA-Z0-9-]*§?[a-zA-Z0-9]*)?§?\.§?[a-zA-Z]{2,30}§?[a-zA-Z]{0,33}§?(\/[^\s]*)?)§?(?=\s|$)",
            RegexOptions.Compiled);
        private static readonly Regex SCENE_REGEX = new (@"(?:^|\s)-?§?\d{0,1}§?\d{0,1}§?\d{1}§?,§?-?§?\d{1}§?\d{0,1}§?\d{0,1}§?(?=\s|$)", RegexOptions.Compiled);
        private static readonly Regex WORLD_REGEX = new (@"(?:^|\s)§?[a-zA-Z0-9]§?[a-zA-Z0-9]*§?[a-zA-Z0-9]*§?\.dcl\.eth§?(?=\s|$)",
            RegexOptions.Compiled);

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

        public void ValidateOnBackspace(ref string text, ref int pos)
        {
            if (pos <= 0 || text.Length == 0)
                return;

            PerformValidation(ref text, ref pos);
        }

        public override char Validate(ref string text, ref int pos, char ch)
            => PerformValidation(ref text, ref pos, ch);


        private char PerformValidation(ref string text, ref int pos, char ch = default)
        {
            mainStringBuilder.Clear();
            mainStringBuilder.Append(text.AsSpan(0, pos));

            if (ch != default)
                mainStringBuilder.Append(ch);

            mainStringBuilder.Append(TAG_STRING);

            mainStringBuilder.Append(text.AsSpan(pos));

            ProcessMainStringBuilder();
            pos = GetPositionFromTag(mainStringBuilder);
            text = mainStringBuilder.Remove(pos,1).ToString();
            return ch;
        }

        private int GetPositionFromTag(StringBuilder stringBuilder)
        {
            int length = stringBuilder.Length;

            for (int i = 0; i < length; i++)
            {
                if (stringBuilder[i] == TAG_CHAR)
                    return i;
            }
            return 0;
        }

        private void ProcessMainStringBuilder()
        {
            RemoveLinkTags(mainStringBuilder);
            ReplaceMatches(RICH_TEXT_TAG_REGEX, mainStringBuilder, ReplaceRichTextTags);
            ReplaceMatches(WEBSITE_REGEX, mainStringBuilder, WrapWithUrlLink);
            ReplaceMatches(SCENE_REGEX, mainStringBuilder, WrapWithSceneLink);
            ReplaceMatches(WORLD_REGEX, mainStringBuilder, WrapWithWorldLink);
        }

        private void RemoveLinkTags(StringBuilder stringBuilder)
        {
            var text = stringBuilder.ToString();
            MatchCollection matches = LINK_TAG_REGEX.Matches(text);

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                Match match = matches[i];
                stringBuilder.Clear()
                             .Append(text.AsSpan(0, match.Index))
                             .Append(match.Groups[2])
                             .Append(text.AsSpan(match.Index + match.Length));
            }
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

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                Match match = matches[i];
                stringBuilder.Clear()
                             .Append(text.AsSpan(0, match.Index))
                             .Append(evaluator(match))
                             .Append(text.AsSpan(match.Index + match.Length));
            }
        }

        private StringBuilder WrapWithUrlLink(Match match) =>
            WrapWithLink(match, LinkType.URL);

        private StringBuilder WrapWithSceneLink(Match match) =>
            WrapWithLink(match, LinkType.SCENE);

        private StringBuilder WrapWithWorldLink(Match match) =>
            WrapWithLink(match, LinkType.WORLD);

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
                    linkTypeString = USER;
                    break;
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
