using System;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace DCL.UI.InputFieldValidator
{
    [CreateAssetMenu(fileName = "InputFieldValidator", menuName = "DCL/UI/InputFieldValidator")]
    public class InputFieldsValidator : TMP_InputValidator
    {
        private const string TAG = "§";
        private const string SCENE = "scene";
        private const string WORLD = "world";
        private const string URL = "url";
        private const string USER = "user";

        private static readonly Regex RICH_TEXT_TAG_REGEX = new (@"<(?!\/?(b|i)(>|\s))[^>]+>", RegexOptions.Compiled);
        private static readonly Regex LINK_TAG_REGEX = new (@"<#[0-9A-Fa-f]{6}><link=(url|scene|world|user):.*?>(.*?)</link></color>", RegexOptions.Compiled);
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

            mainStringBuilder.Clear();
            mainStringBuilder.Append(text).Insert(pos, TAG);

            ProcessMainStringBuilder(ref pos);

            text = mainStringBuilder.ToString();

            //Not Ideal implementation, but other methods are much more convoluted,
            //will try to find something better before merging to dev
            int tag = text.IndexOf(TAG, StringComparison.InvariantCulture);
            int tag2 = text.LastIndexOf(TAG, StringComparison.InvariantCulture);

            if (tag != tag2) { pos = tag2 - 1; }
            else { pos = tag; }

            text = mainStringBuilder.Replace(TAG, "").ToString();
        }

        public override char Validate(ref string text, ref int pos, char ch)
        {
            mainStringBuilder.Clear();
            mainStringBuilder.Append(text).Insert(pos, ch);
            ProcessMainStringBuilder(ref pos);
            text = mainStringBuilder.ToString();
            return ch;
        }

        private void ProcessMainStringBuilder(ref int pos)
        {
            int originalLength = mainStringBuilder.Length;

            RemoveLinkTags(mainStringBuilder);
            ReplaceMatches(RICH_TEXT_TAG_REGEX, mainStringBuilder, ReplaceRichTextTags);
            ReplaceMatches(WEBSITE_REGEX, mainStringBuilder, WrapWithUrlLink);
            ReplaceMatches(SCENE_REGEX, mainStringBuilder, WrapWithSceneLink);
            ReplaceMatches(WORLD_REGEX, mainStringBuilder, WrapWithWorldLink);

            int lengthDifference = mainStringBuilder.Length - originalLength;
            pos += lengthDifference + 1;
        }

        private void RemoveLinkTags(StringBuilder sb)
        {
            MatchCollection matches = LINK_TAG_REGEX.Matches(sb.ToString());

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                Match match = matches[i];
                sb.Remove(match.Index, match.Length);
                sb.Insert(match.Index, match.Groups[2]);
            }
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
            MatchCollection matches = regex.Matches(stringBuilder.ToString());

            if (matches.Count == 0)
                return;

            for (int i = matches.Count - 1; i >= 0; i--)
            {
                Match match = matches[i];
                stringBuilder.Remove(match.Index, match.Length);
                stringBuilder.Insert(match.Index, evaluator(match));
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

            //Validate here if these are valid before creating the links
            switch (linkType)
            {
                case LinkType.SCENE:
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
                             .Append(':')
                             .Append(match.Value)
                             .Append(">")
                             .Append(match.Value)
                             .Append(linkClosingStyle);

            return tempStringBuilder;
        }

        private enum LinkType
        {
            SCENE,
            WORLD,
            URL,
            USER,
        }
    }
}
