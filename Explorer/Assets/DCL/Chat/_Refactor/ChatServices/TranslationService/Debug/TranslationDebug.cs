using System.Text;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DCL.Diagnostics;
using DCL.Chat.ChatServices.TranslationService.Utilities;

namespace DCL.Translation.Service.Debug
{
    public static class TranslationDebug
    {
        public static bool Enabled = true;

        public static int MaxLogChars = 8000;

        public static void LogInfo(string msg)
        {
            if (!Enabled) return;
            ReportHub.Log(ReportCategory.CHAT_TRANSLATE, msg);
        }

        public static void LogRequest(string url, string contentType, object payload)
        {
            if (!Enabled) return;
            ReportHub.Log(ReportCategory.CHAT_TRANSLATE,
                $"[MT] → {url}\nContent-Type: {contentType}\nBody:\n{Trunc(PrettyJson(payload))}");
        }

        public static void LogResponse(string url, object payload)
        {
            if (!Enabled) return;
            ReportHub.Log(ReportCategory.CHAT_TRANSLATE,
                $"[MT] ← {url}\nResponse:\n{Trunc(PrettyJson(payload))}");
        }

        public static void LogRetry(int attempt, int max, int delayMs, long httpCode)
        {
            if (!Enabled) return;
            ReportHub.Log(ReportCategory.CHAT_TRANSLATE,
                $"[MT] retry {attempt}/{max} after {delayMs} ms (HTTP {httpCode})");
        }

        public static void LogTokens(string label, List<Tok> toks)
        {
            if (!Enabled) return;
            var sb = new StringBuilder();
            sb.AppendLine($"[Seg] {label} (count={toks.Count})");
            for (int i = 0; i < toks.Count; i++)
            {
                var t = toks[i];
                string v = Escape(t.Value);
                if (v.Length > 120) v = v.Substring(0, 120) + "…";
                sb.AppendLine($"{i,3}: {t.Type,-7} \"{v}\"");
            }

            ReportHub.Log(ReportCategory.CHAT_TRANSLATE, sb.ToString());
        }

        public static void LogPieces(string label, string[] pieces)
        {
            if (!Enabled) return;
            ReportHub.Log(ReportCategory.CHAT_TRANSLATE,
                $"[Seg] {label} pieces:\n{Trunc(PrettyJson(pieces))}");
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
            return s.Length <= MaxLogChars ? s : s.Substring(0, MaxLogChars) + "… [truncated]";
        }

        private static string Escape(string s)
        {
            return s?.Replace("\r", "\\r").Replace("\n", "\\n") ?? "null";
        }
    }
}