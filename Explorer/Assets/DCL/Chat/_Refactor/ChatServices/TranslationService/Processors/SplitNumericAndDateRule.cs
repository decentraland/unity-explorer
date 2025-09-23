using System.Collections.Generic;
using System.Text.RegularExpressions;
using DCL.Chat.ChatServices.TranslationService.Processors.DCL.Translation.Service.Processing;
using DCL.Chat.ChatServices.TranslationService.Processors.RegEx;
using DCL.Chat.ChatServices.TranslationService.Utilities;

namespace DCL.Chat.ChatServices.TranslationService.Processors
{
    public class SplitNumericAndDateRule : ITokenizationRule
    {
        public List<Tok> Process(List<Tok> tokens)
        {
            return SplitTextTokensOnNumbersAndDates(tokens);
        }

        private List<Tok> SplitTextTokensOnNumbersAndDates(List<Tok> toks)
        {
            var outList = new List<Tok>(toks.Count + 8);
            int nextId = 0;

            foreach (var t in toks)
            {
                if (t.Type != TokType.Text || string.IsNullOrEmpty(t.Value))
                {
                    outList.Add(new Tok(nextId++, t.Type, t.Value));
                    continue;
                }

                string v = t.Value;
                var spans = new List<(int start, int len)>();

                void AddMatches(Regex rx)
                {
                    var m = rx.Matches(v);
                    for (int i = 0; i < m.Count; i++) spans.Add((m[i].Index, m[i].Length));
                }

                AddMatches(ProtectedPatterns.CurrencyAmountRx);
                AddMatches(ProtectedPatterns.AmountCurrencyRx);
                AddMatches(ProtectedPatterns.IsoDateRx);
                AddMatches(ProtectedPatterns.SlashDateRx);
                AddMatches(ProtectedPatterns.DotDateRx);
                AddMatches(ProtectedPatterns.Time24Rx);
                AddMatches(ProtectedPatterns.Time12Rx);

                if (spans.Count == 0)
                {
                    outList.Add(new Tok(nextId++, TokType.Text, v));
                    continue;
                }

                spans.Sort((a, b) => a.start != b.start ? a.start.CompareTo(b.start) : b.len.CompareTo(a.len));

                var final = new List<(int s, int l)>();
                int cursor = -1;
                foreach (var s in spans)
                {
                    if (s.start >= cursor)
                    {
                        final.Add(s);
                        cursor = s.start + s.len;
                    }
                }

                int last = 0;
                foreach (var s in final)
                {
                    if (s.s > last)
                        outList.Add(new Tok(nextId++, TokType.Text, v.Substring(last, s.s - last)));

                    outList.Add(new Tok(nextId++, TokType.Number, v.Substring(s.s, s.l)));
                    last = s.s + s.l;
                }

                if (last < v.Length)
                    outList.Add(new Tok(nextId++, TokType.Text, v.Substring(last)));
            }

            return outList;
        }
    }
}