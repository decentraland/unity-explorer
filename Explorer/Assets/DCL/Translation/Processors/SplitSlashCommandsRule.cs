using DCL.Translation.Processors.DCL.Translation.Service.Processing;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DCL.Translation.Processors
{
    public class SplitSlashCommandsRule : ITokenizationRule
    {
        public List<Tok> Process(List<Tok> tokens)
        {
            return SplitTextTokensOnSlashCommands(tokens);
        }

        private List<Tok> SplitTextTokensOnSlashCommands(List<Tok> toks)
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
                var ms = ProtectedPatterns.InlineCommandRx.Matches(v);
                if (ms.Count == 0)
                {
                    outList.Add(new Tok(nextId++, TokType.Text, v));
                    continue;
                }

                int last = 0;
                foreach (Match m in ms)
                {
                    if (m.Index > last)
                        outList.Add(new Tok(nextId++, TokType.Text, v.Substring(last, m.Index - last)));

                    outList.Add(new Tok(nextId++, TokType.Command, m.Value));
                    last = m.Index + m.Length;
                }

                if (last < v.Length)
                    outList.Add(new Tok(nextId++, TokType.Text, v.Substring(last)));
            }

            return outList;
        }
    }
}