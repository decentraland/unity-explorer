using System.Collections.Generic;
using DCL.Chat.ChatServices.TranslationService.Processors.DCL.Translation.Service.Processing;
using DCL.Chat.ChatServices.TranslationService.Utilities;

namespace DCL.Chat.ChatServices.TranslationService.Processors
{
    public class SplitTextTokensOnEmojiRule : ITokenizationRule
    {
        public List<Tok> Process(List<Tok> tokens)
        {
            return SplitTextTokensOnEmoji(tokens);
        }

        private List<Tok> SplitTextTokensOnEmoji(List<Tok> toks)
        {
            var outList = new List<Tok>(toks.Count + 4);
            int nextId = 0;

            foreach (var t in toks)
            {
                if (t.Type != TokType.Text || string.IsNullOrEmpty(t.Value))
                {
                    outList.Add(new Tok(nextId++, t.Type, t.Value));
                    continue;
                }

                string v = t.Value;
                var emojiRuns = EmojiDetector.FindEmoji(v);

                if (emojiRuns == null || emojiRuns.Count == 0)
                {
                    outList.Add(new Tok(nextId++, TokType.Text, v));
                    continue;
                }

                int last = 0;
                for (int r = 0; r < emojiRuns.Count; r++)
                {
                    var run = emojiRuns[r];
                    int start = run.Index;
                    int len   = run.Value.Length;

                    if (start > last)
                        outList.Add(new Tok(nextId++, TokType.Text, v.Substring(last, start - last)));

                    outList.Add(new Tok(nextId++, TokType.Emoji, run.Value));

                    last = start + len;
                }

                if (last < v.Length)
                    outList.Add(new Tok(nextId++, TokType.Text, v.Substring(last)));
            }

            return outList;
        }
    }
}