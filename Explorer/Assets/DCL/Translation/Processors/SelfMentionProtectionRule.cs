using System;
using DCL.Translation.Processors.DCL.Translation.Service.Processing;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DCL.Translation.Processors
{
    /// <summary>
    ///     Identifies and protects self-mentions that are not wrapped in link tags.
    ///     The pattern is a hex color tag immediately followed by text starting with '@'
    ///     and a corresponding closing color tag.
    /// </summary>
    public class SelfMentionProtectionRule : ITokenizationRule
    {
        // This regex robustly matches a hex color tag, including optional alpha.
        private static readonly Regex ColorTagRegex = new (@"^<#([A-Fa-f0-9]{6}|[A-Fa-f0-9]{8})>$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const string CLOSE_COLOR_TAG = "</color>";

        public List<Tok> Process(List<Tok> tokens)
        {
            // Building a new list is safer than modifying the collection while iterating.
            var processedTokens = new List<Tok>(tokens.Count);

            for (int i = 0; i < tokens.Count; i++)
            {
                var currentToken = tokens[i];

                // Potential start of a self-mention: Is it a color tag?
                if (currentToken.Type == TokType.Tag && ColorTagRegex.IsMatch(currentToken.Value))
                {
                    // Is the very next token a text token that starts with '@'?
                    if (i + 1 < tokens.Count && tokens[i + 1].Type == TokType.Text && tokens[i + 1].Value.StartsWith("@"))
                    {
                        // We have a potential match. Now, find the closing </color> tag.
                        int closeIndex = FindClosingColorTag(tokens, i + 2);

                        if (closeIndex != -1)
                        {
                            // A complete self-mention block was found. Merge all parts into one protected token.
                            var mergedValue = new StringBuilder();
                            for (int j = i; j <= closeIndex; j++)
                                mergedValue.Append(tokens[j].Value);

                            processedTokens.Add(new Tok(currentToken.Id, TokType.Protected, mergedValue.ToString()));

                            // Advance the main loop index past the tokens we just processed.
                            i = closeIndex;
                            continue;
                        }
                    }
                }

                // If no match was found, just add the current token as is.
                processedTokens.Add(currentToken);
            }

            return processedTokens;
        }

        private static int FindClosingColorTag(List<Tok> tokens, int startIndex)
        {
            for (int k = startIndex; k < tokens.Count; k++)
            {
                if (tokens[k].Type == TokType.Tag &&
                    tokens[k].Value.Equals(CLOSE_COLOR_TAG, StringComparison.OrdinalIgnoreCase))
                    return k;
            }

            return -1; // Not found
        }
    }
}