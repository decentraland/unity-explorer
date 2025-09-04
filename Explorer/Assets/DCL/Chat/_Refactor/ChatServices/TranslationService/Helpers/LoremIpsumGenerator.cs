using System.Text;
using UnityEngine;

namespace DCL.Chat.ChatServices.TranslationService.Helpers
{
    public static class LoremIpsumGenerator
    {
        private static readonly string[] Words =
        {
            "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit", "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore", "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud", "exercitation", "ullamco", "laboris", "nisi", "ut", "aliquip", "ex", "ea", "commodo", "consequat"
        };

        /// <summary>
        ///     Generates a random block of "Lorem Ipsum" text.
        /// </summary>
        /// <param name="minWords">The minimum number of words to generate.</param>
        /// <param name="maxWords">The maximum number of words to generate.</param>
        /// <returns>A string of pseudo-Latin text.</returns>
        public static string Generate(int minWords = 5, int maxWords = 25)
        {
            int numWords = Random.Range(minWords, maxWords + 1);
            var result = new StringBuilder();
            bool firstWord = true;

            for (int i = 0; i < numWords; i++)
            {
                string word = Words[Random.Range(0, Words.Length)];

                if (firstWord)
                {
                    // Capitalize the first letter of the first word.
                    result.Append(char.ToUpper(word[0]) + word.Substring(1));
                    firstWord = false;
                }
                else
                {
                    result.Append(word);
                }

                if (i < numWords - 1)
                {
                    result.Append(" ");
                }
            }

            result.Append(".");
            return result.ToString();
        }
    }
}