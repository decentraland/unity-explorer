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
        private static readonly Regex WEBSITE_REGEX = new (
            @"\b((https?:\/\/)?(www\.)[a-zA-Z0-9](?:[a-zA-Z0-9-]*[a-zA-Z0-9])?\.[a-zA-Z]{2,63}(\/[^\s]*)?)\b",
            RegexOptions.Compiled);
        private static readonly Regex SCENE_REGEX = new Regex(@"(?<!\S)-?\d{1,3},\s*-?\d{1,3}(?!\S)", RegexOptions.Compiled);
        private static readonly Regex WORLD_REGEX = new Regex(@"(?<!\S)[a-zA-Z0-9][a-zA-Z0-9-]*\.dcl\.eth(?!\S)", RegexOptions.Compiled);

        [SerializeField] private TMP_StyleSheet styleSheet;

        private TMP_Style style;

        public void InitializeStyles()
        {
            style = styleSheet.GetStyle("Link");
        }

        public void ValidateOnBackspace(ref string text, ref int pos)
        {
            text = text.Insert(pos, "TAG");

            text = ProcessWord(text);

            int tag = text.IndexOf("TAG", StringComparison.InvariantCulture);
            int tag2 = text.LastIndexOf("TAG", StringComparison.InvariantCulture);

            if (tag != tag2)
            {
                pos = tag2 - 3;
            }
            else
            {
                pos = tag;
            }

            text = text.Replace("TAG", "");
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
                return string.Concat(style.styleOpeningDefinition, "<link=url:", website, ">", website, "</link>", style.styleClosingDefinition);
            });

            text = SCENE_REGEX.Replace(text, match =>
            {
                string scene = match.Value;
                return string.Concat(style.styleOpeningDefinition, "<link=scene:", scene, ">", scene, "</link>", style.styleClosingDefinition);
            });

            text = WORLD_REGEX.Replace(text, match =>
            {
                string world = match.Value;
                return string.Concat(style.styleOpeningDefinition, "<link=world:", world, ">", world, "</link>", style.styleClosingDefinition);
            });

            return text;
        }
    }
}
