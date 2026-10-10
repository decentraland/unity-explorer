using DCL.Translation.Processors.DCL.Translation.Service.Processing;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.Translation.Processors
{
    /// <summary>
    ///     Identifies and protects Unicode emoji sequences (e.g., \U0001F631)
    ///     by tokenizing them as Emoji type. This prevents them from being
    ///     misinterpreted as other token types (like commands) or sent for translation.
    /// </summary>
    public class SplitUnicodeEmojiRule : ITokenizationRule
    {
        // Regex to match the \UXXXXXXXX format for Unicode characters.
        private static readonly Regex UnicodeEmojiRx = new (@"\\U[0-9A-Fa-f]{8}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public List<Tok> Process(List<Tok> tokens)
        {
            var outList = new List<Tok>(tokens.Count + 4);
            int nextId = 0;

            foreach (var t in tokens)
            {
                // This rule only processes text tokens.
                if (t.Type != TokType.Text || string.IsNullOrEmpty(t.Value))
                {
                    outList.Add(new Tok(nextId++, t.Type, t.Value));
                    continue;
                }

                string v = t.Value;
                var matches = UnicodeEmojiRx.Matches(v);

                // If no matches, add the token as is and continue.
                if (matches.Count == 0)
                {
                    outList.Add(new Tok(nextId++, TokType.Text, v));
                    continue;
                }

                int lastIndex = 0;
                foreach (Match match in matches)
                {
                    // Add the text preceding the emoji match.
                    if (match.Index > lastIndex)
                        outList.Add(new Tok(nextId++, TokType.Text, v.Substring(lastIndex, match.Index - lastIndex)));

                    // Add the emoji match as an Emoji token.
                    outList.Add(new Tok(nextId++, TokType.Emoji, match.Value));
                    lastIndex = match.Index + match.Length;
                }

                // Add any remaining text after the last emoji match.
                if (lastIndex < v.Length)
                    outList.Add(new Tok(nextId++, TokType.Text, v.Substring(lastIndex)));
            }

            return outList;
        }
    }
}