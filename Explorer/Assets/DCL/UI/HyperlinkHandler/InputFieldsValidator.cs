using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace DCL.UI.InputFieldValidator
{
    [CreateAssetMenu(fileName = "InputFieldValidator", menuName = "DCL/UI/InputFieldValidator")]
    public class InputFieldsValidator : TMP_InputValidator
    {
        private static readonly Regex RICH_TEXT_TAG_REGEX = new (@"<(?!\/?(b|i)(>|\s))[^>]+>", RegexOptions.Compiled);
        private static readonly Regex LINK_TAG_REGEX = new (@"<#[0-9A-Fa-f]{6}><link=(url|scene|world|user):.*?>(.*?)</link></color>", RegexOptions.Compiled);
        private static readonly Regex WEBSITE_REGEX = new (@"\b((https?:\/\/)?(www\.)[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,63}(\/[^\s]*)?)\b", RegexOptions.Compiled);
        private static readonly Regex SCENE_REGEX = new (@"(?<!\S)-?\d{1,3},\s*-?\d{1,3}(?!\S)", RegexOptions.Compiled);
        private static readonly Regex WORLD_REGEX = new (@"(?<!\S)[a-zA-Z0-9][a-zA-Z0-9-]*\.dcl\.eth(?!\S)", RegexOptions.Compiled);

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

        public void ValidateOnBackspace2(ref string text, ref int pos)
        {
            text = text.Insert(pos, "TAG");

            //text = ProcessWord(text);

            int tag = text.IndexOf("TAG", StringComparison.InvariantCulture);
            int tag2 = text.LastIndexOf("TAG", StringComparison.InvariantCulture);

            if (tag != tag2) { pos = tag2 - 3; }
            else { pos = tag; }

            text = text.Replace("TAG", "");
        }

        public void ValidateOnBackspace(ref string text, ref int pos)
        {
            if (pos <= 0 || text.Length == 0)
                return;

            mainStringBuilder.Clear();
            mainStringBuilder.Append(text);

            text = ProcessWord(ref pos);
        }

        public override char Validate(ref string text, ref int pos, char ch)
        {
            mainStringBuilder.Clear();
            mainStringBuilder.Append(text).Insert(pos, ch);

            pos++;

            text = ProcessWord(ref pos);
            return ch;
        }

        private string ProcessWord(ref int pos)
        {
            int originalLength = mainStringBuilder.Length;

            RemoveLinkTags(mainStringBuilder);
            ReplaceMatches(RICH_TEXT_TAG_REGEX, mainStringBuilder, ReplaceRichTextTags);
            ReplaceMatches(WEBSITE_REGEX, mainStringBuilder, WrapWithUrlLink);
            ReplaceMatches(SCENE_REGEX, mainStringBuilder, WrapWithSceneLink);
            ReplaceMatches(WORLD_REGEX, mainStringBuilder, WrapWithWorldLink);

            int lengthDifference = mainStringBuilder.Length - originalLength;
            pos += lengthDifference;

            return mainStringBuilder.ToString();
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
            WrapWithLink(match, "url");

        private StringBuilder WrapWithSceneLink(Match match) =>
            WrapWithLink(match, "scene");

        private StringBuilder WrapWithWorldLink(Match match) =>
            WrapWithLink(match, "world");

        private StringBuilder WrapWithLink(Match match, string linkType)
        {
            tempStringBuilder.Clear();

            tempStringBuilder.Append(linkOpeningStyle)
                             .Append(linkType)
                             .Append(':')
                             .Append(match.Value)
                             .Append(">")
                             .Append(match.Value)
                             .Append(linkClosingStyle);

            return tempStringBuilder;
        }
    }
}
