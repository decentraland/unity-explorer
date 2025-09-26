using DCL.Translation.Processors.DCL.Translation.Service.Processing;
using System.Collections.Generic;

namespace DCL.Translation.Processors
{
    public class AngleBracketSegmentationRule : ITokenizationRule
    {
        public List<Tok> Process(List<Tok> tokens)
        {
            if (tokens.Count != 1 || tokens[0].Type != TokType.Text) return tokens;
            return SegmentByAngleBrackets(tokens[0].Value);
        }

        private List<Tok> SegmentByAngleBrackets(string s)
        {
            var toks = new List<Tok>();
            if (string.IsNullOrEmpty(s))
            {
                toks.Add(new Tok(0, TokType.Text, string.Empty));
                return toks;
            }

            int id = 0, i = 0, n = s.Length;
            while (i < n)
            {
                int lt = s.IndexOf('<', i);
                if (lt < 0)
                {
                    if (i < n) toks.Add(new Tok(id++, TokType.Text, s.Substring(i)));
                    break;
                }

                if (lt > i) toks.Add(new Tok(id++, TokType.Text, s.Substring(i, lt - i)));

                int gt = s.IndexOf('>', lt + 1);
                if (gt < 0)
                {
                    toks.Add(new Tok(id++, TokType.Text, s.Substring(lt)));
                    break;
                }

                toks.Add(new Tok(id++, TokType.Tag, s.Substring(lt, gt - lt + 1)));
                i = gt + 1;
            }

            return toks;
        }
    }
}