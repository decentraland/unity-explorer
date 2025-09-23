using Cysharp.Threading.Tasks;
using DCL.Translation.Models;
using DCL.Utilities;
using System.Threading;
using System.Collections.Generic;
using DCL.Chat.ChatServices.TranslationService.Utilities;
using DCL.Translation.Service.Provider;
using System;
using System.Text;
using DCL.Chat.ChatServices.TranslationService.Processors.DCL.Translation.Service.Processing;
using DCL.Translation.Service.Debug;

namespace DCL.Chat.ChatServices.TranslationService.Processors
{
    public class ChatMessageProcessor : IMessageProcessor
    {
        private readonly ITranslationProvider provider;
        private readonly List<ITokenizationRule> rules;

        public ChatMessageProcessor(ITranslationProvider provider)
        {
            this.provider = provider;

            rules = new List<ITokenizationRule>
            {
                new AngleBracketSegmentationRule(), new LinkProtectionRule(), new SplitTextTokensOnEmojiRule(), new SplitNumericAndDateRule(),
                new SplitSlashCommandsRule()
            };
        }

        public async UniTask<TranslationResult> ProcessAndTranslateAsync(string rawText, LanguageCode targetLang, CancellationToken ct)
        {
            TranslationDebug.LogInfo($"[Seg] input: \"{rawText}\"");

            var tokens = new List<Tok>();
            if (!string.IsNullOrEmpty(rawText))
            {
                tokens.Add(new Tok(0, TokType.Text, rawText));
            }

            // 1. Execute all segmentation rules in order
            foreach (var rule in rules)
            {
                tokens = rule.Process(tokens);
            }

            TranslationDebug.LogTokens("after-all-rules", tokens);

            // 2. Extract parts that need translation
            (string[] cores, int[] idxs, string[] leading, string[] trailing) =
                ExtractTranslatablesPreserveSpaces(tokens);

            // 3. Translate
            string[] translated = Array.Empty<string>();
            if (cores.Length > 0)
            {
                if (provider is IBatchTranslationProvider batchProvider)
                {
                    var response = await batchProvider.TranslateBatchAsync(cores, targetLang, ct);
                    translated = response.translatedText;
                }
                else
                {
                    // Fallback for non-batch providers
                    translated = new string[cores.Length];
                    for (int i = 0; i < cores.Length; i++)
                    {
                        var result = await provider.TranslateAsync(cores[i], targetLang, ct);
                        translated[i] = result.TranslatedText;
                    }
                }
            }

            // 4. Apply translations back
            tokens = ApplyTranslationsWithSpaces(tokens, idxs, leading, trailing, translated);
            TranslationDebug.LogTokens("after-apply", tokens);

            // 5. Stitch the final string
            string stitched = Stitch(tokens);
            TranslationDebug.LogInfo($"[Seg] output: \"{stitched}\"");

            // Assuming source language detection is handled by provider, returning a placeholder.
            // A more advanced system could aggregate this from the provider's response.
            return new TranslationResult(stitched, LanguageCode.EN, false);
        }

        private (string[] cores, int[] idxs, string[] leading, string[] trailing)
            ExtractTranslatablesPreserveSpaces(List<Tok> toks)
        {
            var cores = new List<string>();
            var idxs  = new List<int>();
            var lead  = new List<string>();
            var trail = new List<string>();

            for (int i = 0; i < toks.Count; i++)
            {
                if (toks[i].Type != TokType.Text) continue;
                string v = toks[i].Value;
                if (string.IsNullOrEmpty(v)) continue;

                int L = 0;
                while (L < v.Length && char.IsWhiteSpace(v[L])) L++;
                int R = 0;
                while (R < v.Length - L && char.IsWhiteSpace(v[v.Length - 1 - R])) R++;

                if (L + R >= v.Length) continue; // pure whitespace: don’t translate, keep as-is

                lead.Add(v.Substring(0, L));
                trail.Add(v.Substring(v.Length - R, R));
                cores.Add(v.Substring(L, v.Length - L - R));
                idxs.Add(i);
            }

            return (cores.ToArray(), idxs.ToArray(), lead.ToArray(), trail.ToArray());
        }

        private List<Tok> ApplyTranslationsWithSpaces(
            List<Tok> toks, int[] idxs, string[] leading, string[] trailing, string[] translated)
        {
            for (int k = 0; k < idxs.Length; k++)
            {
                int i = idxs[k];
                toks[i] = toks[i].With(leading[k] + translated[k] + trailing[k]);
            }

            return toks;
        }

        private string Stitch(List<Tok> toks)
        {
            var sb = new StringBuilder();
            foreach (var t in toks) sb.Append(t.Value);
            return sb.ToString();
        }
    }
}