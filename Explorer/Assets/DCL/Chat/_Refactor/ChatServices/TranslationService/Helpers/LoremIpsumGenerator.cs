using System.Collections.Generic;
using System.Text;
using DCL.Translation.Models; // Make sure you have a 'using' for your LanguageCode enum
using UnityEngine;

namespace DCL.Chat.ChatServices.TranslationService.Helpers
{
    public static class LoremIpsumGenerator
    {
        // A dictionary to hold word banks for each supported language.
        private static readonly Dictionary<LanguageCode, string[]> LanguageWords = new ()
        {
            // Default/English uses the classic Lorem Ipsum words.
            [LanguageCode.EN] = new[]
            {
                "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua"
            },

            // Spanish-sounding words
            [LanguageCode.ES] = new[]
            {
                "sol", "luna", "gato", "mesa", "cielo", "fuego", "agua", "tierra", "computadora", "programador", "virtual", "futuro", "explorar", "mundo", "nuevo", "crear", "juntos"
            },

            // French-sounding words
            [LanguageCode.FR] = new[]
            {
                "le", "la", "croissant", "baguette", "ordinateur", "virtuel", "monde", "nouveau", "explorer", "ensemble", "créer", "magnifique", "étoile", "ciel", "futur", "développeur"
            },

            // German-sounding words
            [LanguageCode.DE] = new[]
            {
                "der", "die", "das", "schnell", "entwickler", "welt", "neu", "entdecken", "zusammen", "schaffen", "zukunft", "virtuell", "wunderbar", "himmel", "stern", "haus"
            },

            // Portuguese-sounding words
            [LanguageCode.PT] = new[]
            {
                "o", "a", "sol", "lua", "gato", "mesa", "céu", "fogo", "água", "terra", "computador", "desenvolvedor", "virtual", "futuro", "explorar", "mundo", "novo", "criar", "juntos"
            },

            // Italian-sounding words
            [LanguageCode.IT] = new[]
            {
                "il", "la", "gatto", "tavolo", "cielo", "fuoco", "acqua", "terra", "computer", "sviluppatore", "virtuale", "futuro", "esplorare", "mondo", "nuovo", "creare", "insieme"
            },

            // Common Chinese characters (Hanzi)
            [LanguageCode.ZH] = new[]
            {
                "的", "一", "是", "在", "不", "了", "有", "我", "人", "大", "中", "国", "上", "小", "你", "好", "世", "界", "元", "宇", "宙"
            },

            // Common Japanese characters (Hiragana/Katakana)
            [LanguageCode.JA] = new[]
            {
                "こ", "ん", "に", "ち", "は", "世", "界", "私", "あ", "な", "た", "デ", "ザ", "イ", "ン", "プ", "ロ", "グ", "ラ", "ム", "メ", "タ", "バ", "ー", "ス"
            },

            // Common Korean syllables (Hangul)
            [LanguageCode.KO] = new[]
            {
                "하", "세", "요", "감", "사", "합", "니", "다", "세", "계", "메", "타", "버", "스", "사", "람", "안", "녕", "컴", "퓨", "터", "프", "로", "그", "래", "머"
            }
        };

        /// <summary>
        ///     Generates a random block of text using a word bank appropriate for the target language.
        /// </summary>
        /// <param name="language">The language for which to generate text.</param>
        /// <param name="minWords">The minimum number of words/characters to generate.</param>
        /// <param name="maxWords">The maximum number of words/characters to generate.</param>
        /// <returns>A string of pseudo-random text in the style of the target language.</returns>
        public static string Generate(LanguageCode language, int minWords = 5, int maxWords = 25)
        {
            // Try to get the specific word bank; if it doesn't exist, fall back to English.
            if (!LanguageWords.TryGetValue(language, out string[]? wordsToUse))
                wordsToUse = LanguageWords[LanguageCode.EN];

            // Some languages (like Chinese and Japanese) don't typically use spaces between characters.
            bool useSpaces = language != LanguageCode.ZH && language != LanguageCode.JA;

            int numWords = Random.Range(minWords, maxWords + 1);
            var result = new StringBuilder();
            bool firstWord = true;

            for (int i = 0; i < numWords; i++)
            {
                string word = wordsToUse[Random.Range(0, wordsToUse.Length)];

                if (firstWord)
                {
                    // Capitalize the first letter for Latin-based scripts.
                    result.Append(useSpaces ? char.ToUpper(word[0]) + word.Substring(1) : word);
                    firstWord = false;
                }
                else { result.Append(word); }

                if (i < numWords - 1 && useSpaces)
                    result.Append(" ");
            }

            result.Append(".");
            return result.ToString();
        }
    }
}