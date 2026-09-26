using DCL.Translation.Processors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Text;

namespace DCL.Translation
{
    public static class TranslationDebug
    {
        private const int MAX_LOG_CHARS = 8000;

        public static string FormatRequest(string url, string contentType, object payload)
        {
            return $"[MT] → {url}\nContent-Type: {contentType}\nBody:\n{Trunc(PrettyJson(payload))}";
        }

        public static string FormatResponse(string url, object payload)
        {
            return $"[MT] ← {url}\nResponse:\n{Trunc(PrettyJson(payload))}";
        }

        public static string FormatRetry(int attempt, int max, int delayMs, long httpCode)
        {
            return $"[MT] retry {attempt}/{max} after {delayMs} ms (HTTP {httpCode})";
        }

        public static string FormatTokens(string label, List<Tok> toks)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[Seg] {label} (count={toks.Count})");
            for (int i = 0; i < toks.Count; i++)
            {
                var t = toks[i];
                string v = Escape(t.Value);
                if (v.Length > 120) v = v.Substring(0, 120) + "…";
                sb.AppendLine($"{i,3}: {t.Type,-7} \"{v}\"");
            }

            return sb.ToString();
        }

        public static string FormatPieces(string label, string[] pieces)
        {
            return $"[Seg] {label} pieces:\n{Trunc(PrettyJson(pieces))}";
        }

        private static string PrettyJson(object o)
        {
            try
            {
                if (o is string s)
                {
                    try { return JToken.Parse(s).ToString(Formatting.Indented); }
                    catch { return s; }
                }

                return JsonConvert.SerializeObject(o, Formatting.Indented);
            }
            catch { return o?.ToString() ?? "null"; }
        }

        private static string Trunc(string s)
        {
            if (s == null) return "null";
            return s.Length <= MAX_LOG_CHARS ? s : s.Substring(0, MAX_LOG_CHARS) + "… [truncated]";
        }

        private static string Escape(string s)
        {
            return s?.Replace("\r", "\\r").Replace("\n", "\\n") ?? "null";
        }
    }
}
