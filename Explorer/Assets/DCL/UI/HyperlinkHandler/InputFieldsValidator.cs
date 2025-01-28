using System;
using System.Collections.Generic;
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
        private static readonly Regex WEBSITE_REGEX = new (
            @"\b((https?:\/\/)?(www\.)?[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,63}(\/[^\s]*)?)\b",
            RegexOptions.Compiled);

        [SerializeField] private TMP_StyleSheet styleSheet;

        private TMP_Style style;

        private void Awake()
        {
            style = styleSheet.GetStyle("Link");
        }

        public void ValidateOnBackspace(ref string text, ref int pos)
        {
            int textLength = text.Length;

            text = ProcessWord(text);

            int lenghtDifference = textLength - text.Length;

            pos  = pos + lenghtDifference;
        }

        public override char Validate(ref string text, ref int pos, char ch)
        {
            text = text.Insert(pos, ch.ToString());

            int textLength = text.Length;

            text = ProcessWord(text);

            int lenghtDifference = text.Length - textLength;

            pos = pos + lenghtDifference + 1;

            return ch;
        }

        private string ProcessWord(string text)
        {
            text = LINK_TAG_REGEX.Replace(text, "$2");

            text = RICH_TEXT_TAG_REGEX.Replace(text, match =>
            {
                string tag = match.Value;
                return tag.Replace('<', '‹').Replace('>', '›');
            });

            text = WEBSITE_REGEX.Replace(text, match =>
            {
                string website = match.Value;
                return $"{styleSheet.GetStyle("Link").styleOpeningDefinition}<link=url:{website}>{website}</link>{styleSheet.GetStyle("Link").styleClosingDefinition}";
            });

            return text;
        }
    }
}
