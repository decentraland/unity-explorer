using Cysharp.Threading.Tasks;
using DCL.Translation.Processors.DCL.Translation.Service.Processing;
using DCL.Translation.Service;
using DCL.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using DCL.Diagnostics;

namespace DCL.Translation.Processors
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
                new AngleBracketSegmentationRule(), new LinkProtectionRule(), new SplitTextTokensOnEmojiRule(), new SplitUnicodeEmojiRule(),
                new SplitNumericAndDateRule(), new SplitSlashCommandsRule(), new SelfMentionProtectionRule()
            };
        }

        public async UniTask<TranslationResult> ProcessAndTranslateAsync(string rawText, LanguageCode targetLang, CancellationToken ct)
        {
            ReportHub.Log(ReportCategory.TRANSLATE, $"[Seg] input: \"{rawText}\"");

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

            ReportHub.Log(ReportCategory.TRANSLATE, TranslationDebug.FormatTokens("after-all-rules", tokens));

            // 2. Extract parts that need translation
            (string[] cores, int[] idxs, string[] leading, string[] trailing) =
                ExtractTranslatablesPreserveSpaces(tokens);

            // 3. Translate
            string[] translated = Array.Empty<string>();
            var detectedSourceLanguage = LanguageCode.EN;
            
            if (cores.Length > 0)
            {
                var detectedLanguages = new List<LanguageCode>();
                
                if (provider is IBatchTranslationProvider batchProvider)
                {
                    var response = await batchProvider.TranslateBatchAsync(cores, targetLang, ct);
                    translated = response.translatedText;

                    if (response.detectedLanguage != null)
                    {
                        foreach (var langDto in response.detectedLanguage)
                            detectedLanguages.Add(LanguageCodeParser.Parse(langDto.language));
                    }
                }
                else
                {
                    // Fallback for non-batch providers
                    translated = new string[cores.Length];
                    for (int i = 0; i < cores.Length; i++)
                    {
                        var result = await provider.TranslateAsync(cores[i], targetLang, ct);
                        translated[i] = result.TranslatedText;
                        detectedLanguages.Add(result.DetectedSourceLanguage);
                    }
                }

                detectedSourceLanguage = GetMostFrequentLanguage(detectedLanguages);
            }

            // 4. Apply translations back
            tokens = ApplyTranslationsWithSpaces(tokens, idxs, leading, trailing, translated);
            ReportHub.Log(ReportCategory.TRANSLATE, TranslationDebug.FormatTokens("after-apply", tokens));

            // 5. Stitch the final string
            string stitched = Stitch(tokens);
            ReportHub.Log(ReportCategory.TRANSLATE, $"[Seg] output: \"{stitched}\"");

            // Assuming source language detection is handled by provider, returning a placeholder.
            // A more advanced system could aggregate this from the provider's response.
            return new TranslationResult(stitched, detectedSourceLanguage, false);
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

        /// <summary>
        ///     Determines the most frequently occurring language from a list of detected languages.
        /// </summary>
        /// <param name="languages">The list of detected languages from translated text segments.</param>
        /// <returns>The most common LanguageCode, or LanguageCode.EN as a default.</returns>
        private LanguageCode GetMostFrequentLanguage(List<LanguageCode> languages)
        {
            if (languages == null || languages.Count == 0)
                return LanguageCode.EN; // Return default if there's nothing to process

            // Use a dictionary to count occurrences of each language.
            var languageCounts = new Dictionary<LanguageCode, int>();

            foreach (var lang in languages)
            {
                if (languageCounts.ContainsKey(lang))
                    languageCounts[lang]++;
                else
                    languageCounts[lang] = 1;
            }

            // Find the language with the highest count.
            var mostFrequent = LanguageCode.EN;
            int maxCount = 0;

            foreach (var pair in languageCounts)
            {
                if (pair.Value > maxCount)
                {
                    maxCount = pair.Value;
                    mostFrequent = pair.Key;
                }
            }

            return mostFrequent;
        }

        /// <summary>
        ///     Safely parses a language code string into a LanguageCode enum.
        /// </summary>
        private LanguageCode ParseLanguageCode(string code)
        {
            if (Enum.TryParse<LanguageCode>(code, true, out var languageCode))
                return languageCode;

            return LanguageCode.EN;
        }

    }
}