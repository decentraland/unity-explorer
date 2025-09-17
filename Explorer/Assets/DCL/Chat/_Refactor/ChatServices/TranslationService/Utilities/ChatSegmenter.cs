using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
    
namespace DCL.Chat.ChatServices.TranslationService.Utilities
{
    public enum TokType
    {
        Text,
        Tag,
        Handle,

        Emoji
        /* add: Url, Command, Number */
    }

    public readonly struct Tok
    {
        public readonly int Id;        // token order
        public readonly TokType Type;
        public readonly string Value;

        public Tok(int id, TokType t, string v)
        {
            Id = id;
            Type = t;
            Value = v;
        }

        public Tok With(string newV) => new Tok(Id, Type, newV);
    }

    public static class ChatSegmenter
    {
        static readonly Regex TagRx = new(@"</?[^>]+?>", RegexOptions.Compiled);
        static readonly Regex LinkOpenRx = new(@"<link=[^>]*>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex LinkCloseRx = new(@"</link>", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        static readonly Regex HandleRx = new(@"@[\p{L}\p{N}_.-]+#[0-9a-fA-F]{2,}", RegexOptions.Compiled);

        struct Span
        {
            public int Start, Len;
            public TokType Type;
        }

        static Span S(int s, int l, TokType t) => new Span
        {
            Start = s, Len = l, Type = t
        };

        public static List<Tok> Segment(string s)
        {
            var spans = new List<Span>();

            // 1) All tags (highest priority)
            var tagMatches = TagRx.Matches(s);
            foreach (Match m in tagMatches)
                spans.Add(S(m.Index, m.Length, TokType.Tag));

            // 2) <link> inner ranges, then handles ONLY inside
            var linkStarts = new Stack<int>();
            var linkRanges = new List<(int start, int end)>();
            foreach (Match m in tagMatches)
            {
                if (LinkOpenRx.IsMatch(m.Value)) linkStarts.Push(m.Index + m.Length);
                else if (LinkCloseRx.IsMatch(m.Value) && linkStarts.Count > 0)
                {
                    int innerStart = linkStarts.Pop();
                    linkRanges.Add((innerStart, m.Index));
                }
            }

            foreach (var (start, end) in linkRanges)
            {
                var mh = HandleRx.Match(s, start, end - start);
                if (mh.Success) spans.Add(S(mh.Index, mh.Length, TokType.Handle));
            }

            // 3) Sort with priority so Tag > Handle > Text
            int Priority(TokType t) => t switch
            {
                TokType.Tag    => 0,
                TokType.Handle => 1,
                _              => 9,
            };

            spans.Sort((a, b) =>
            {
                int c = a.Start.CompareTo(b.Start);
                if (c != 0) return c;
                c = Priority(a.Type).CompareTo(Priority(b.Type));
                if (c != 0) return c;
                return b.Len.CompareTo(a.Len); // longer first if same start+priority
            });

            // 4) Filter overlaps: keep earlier/higher-priority span, drop overlapping later spans
            var filtered = new List<Span>();
            int takenEnd = -1;
            foreach (var sp in spans)
            {
                if (filtered.Count == 0)
                {
                    filtered.Add(sp);
                    takenEnd = sp.Start + sp.Len;
                    continue;
                }

                bool overlaps = sp.Start < takenEnd;
                if (!overlaps)
                {
                    filtered.Add(sp);
                    takenEnd = sp.Start + sp.Len;
                }
                // else: ignore lower-priority overlapping span
            }

            // 5) Emit tokens (everything else becomes TEXT)
            var toks = new List<Tok>(filtered.Count * 2);
            int pos = 0, id = 0;
            foreach (var sp in filtered)
            {
                if (sp.Start > pos)
                    toks.Add(new Tok(id++, TokType.Text, s.Substring(pos, sp.Start - pos)));

                toks.Add(new Tok(id++, sp.Type, s.Substring(sp.Start, sp.Len)));
                pos = sp.Start + sp.Len;
            }

            if (pos < s.Length) toks.Add(new Tok(id++, TokType.Text, s.Substring(pos)));
            return toks;
        }


        public static List<Tok> ProtectLinkInners(List<Tok> toks)
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
                                toks[j] = new Tok(toks[j].Id, TokType.Handle, toks[j].Value); // mark as protected
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

        public static (string[] cores, int[] idxs, string[] leading, string[] trailing)
            ExtractTranslatablesPreserveSpaces(List<Tok> toks)
        {
            var cores = new List<string>();
            var idxs  = new List<int>();
            var lead  = new List<string>();
            var trail = new List<string>();

            for (int i = 0; i < toks.Count; i++)
            {
                if (toks[i].Type != TokType.Text) continue;
                var v = toks[i].Value;
                if (string.IsNullOrEmpty(v)) continue;

                int L = 0; while (L < v.Length && char.IsWhiteSpace(v[L])) L++;
                int R = 0; while (R < v.Length - L && char.IsWhiteSpace(v[v.Length - 1 - R])) R++;

                if (L + R >= v.Length) continue; // pure whitespace: don’t translate, keep as-is

                lead.Add(v.Substring(0, L));
                trail.Add(v.Substring(v.Length - R, R));
                cores.Add(v.Substring(L, v.Length - L - R));
                idxs.Add(i);
            }
            return (cores.ToArray(), idxs.ToArray(), lead.ToArray(), trail.ToArray());
        }

        public static List<Tok> ApplyTranslationsWithSpaces(
            List<Tok> toks, int[] idxs, string[] leading, string[] trailing, string[] translated)
        {
            for (int k = 0; k < idxs.Length; k++)
            {
                int i = idxs[k];
                toks[i] = toks[i].With(leading[k] + translated[k] + trailing[k]);
            }

            return toks;
        }

        public static List<Tok> SplitTextTokensOnEmoji(List<Tok> toks)
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
        
        public static List<Tok> SegmentByAngleBrackets(string s)
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


        public static (string[] pieces, int[] tokenIndexes) ExtractTranslatables(List<Tok> toks)
        {
            var pieces = new List<string>();
            var idxs   = new List<int>();
            for (int i = 0; i < toks.Count; i++)
            {
                if (toks[i].Type == TokType.Text && toks[i].Value.Length > 0)
                {
                    pieces.Add(toks[i].Value);
                    idxs.Add(i);
                }
            }

            return (pieces.ToArray(), idxs.ToArray());
        }

        public static List<Tok> ApplyTranslations(List<Tok> toks, int[] tokenIndexes, string[] translated)
        {
            for (int k = 0; k < tokenIndexes.Length; k++)
            {
                int i = tokenIndexes[k];
                toks[i] = toks[i].With(translated[k]);
            }

            return toks;
        }

        public static string Stitch(List<Tok> toks)
        {
            var sb = new StringBuilder();
            foreach (var t in toks) sb.Append(t.Value);
            return sb.ToString();
        }
        
        private static string EnforceOriginalTags(List<Tok> toks)
        {
            // Re-stitch only from tokens; Tag tokens are exactly from the source.
            var sb = new StringBuilder();
            foreach (var t in toks) sb.Append(t.Value);
            return sb.ToString();
        }
    }
}