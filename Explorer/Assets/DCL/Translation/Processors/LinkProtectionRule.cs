using DCL.Translation.Processors.DCL.Translation.Service.Processing;
using System;
using System.Collections.Generic;

namespace DCL.Translation.Processors
{
    public class LinkProtectionRule : ITokenizationRule
    {
        public List<Tok> Process(List<Tok> tokens)
        {
            return ProtectLinkInners(tokens);
        }

        private List<Tok> ProtectLinkInners(List<Tok> toks)
        {
            for (int i = 0; i < toks.Count; i++)
            {
                if (toks[i].Type == TokType.Tag &&
                    toks[i].Value.StartsWith("<link", StringComparison.OrdinalIgnoreCase))
                {
                    int close = FindClosingLinkTag(toks, i + 1);
                    if (close > i)
                    {
                        for (int j = i + 1; j < close; j++)
                            if (toks[j].Type == TokType.Text)
                            {
                                // mark as protected
                                toks[j] = new Tok(toks[j].Id, TokType.Protected, toks[j].Value);
                            }

                        i = close;
                    }
                }
            }

            return toks;

            static int FindClosingLinkTag(List<Tok> xs, int start)
            {
                for (int k = start; k < xs.Count; k++)
                    if (xs[k].Type == TokType.Tag &&
                        xs[k].Value.StartsWith("</link", StringComparison.OrdinalIgnoreCase))
                        return k;
                return -1;
            }
        }
    }
}