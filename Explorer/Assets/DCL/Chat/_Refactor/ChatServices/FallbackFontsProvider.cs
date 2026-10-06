using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using TMPro;
using UnityEngine.AddressableAssets;

namespace DCL.Chat.ChatServices
{
    public class FallbackFontsProvider : IDisposable
    {
        private readonly List<ProvidedAsset<TMP_FontAsset>> providedAssets = new ();

        public FallbackFontsProvider(IAssetsProvisioner assetsProvisioner, List<AssetReferenceT<TMP_FontAsset>> fallbackFonts, CancellationToken ct)
        {
            LoadAndApplyFallbacksAsync(assetsProvisioner, fallbackFonts, ct).Forget();
        }

        private async UniTask LoadAndApplyFallbacksAsync(IAssetsProvisioner assetsProvisioner, List<AssetReferenceT<TMP_FontAsset>> fallbackFonts, CancellationToken ct)
        {
            try
            {
                List<TMP_FontAsset> fallbackList = EnsureGlobalFallbackList();

                // Each font is provided on its own so one unresolvable reference cannot cost the others, and is
                // appended to the live list as it arrives so it starts covering characters immediately.
                foreach (AssetReferenceT<TMP_FontAsset> reference in fallbackFonts)
                {
                    if (ct.IsCancellationRequested) return;

                    try
                    {
                        ProvidedAsset<TMP_FontAsset> provided = await assetsProvisioner.ProvideMainAssetAsync(reference, ct);

                        if (provided.Value == null)
                        {
                            ReportHub.LogError(ReportCategory.TRANSLATE,
                                $"Fallback font {reference.AssetGUID} provided no asset, so the characters it covers will not render.");

                            continue;
                        }

                        providedAssets.Add(provided);

                        if (!fallbackList.Contains(provided.Value))
                            fallbackList.Add(provided.Value);
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception e)
                    {
                        ReportHub.LogError(ReportCategory.TRANSLATE,
                            $"Fallback font {reference.AssetGUID} could not be loaded, so the characters it covers will not render. " + e.Message + e.StackTrace);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.TRANSLATE)); }
        }

        /// <summary>
        ///     Returns the list TMP resolves fallbacks against, installing one when the settings carry none, so that
        ///     appending to it afterwards is enough to register a font.
        /// </summary>
        private static List<TMP_FontAsset> EnsureGlobalFallbackList()
        {
            List<TMP_FontAsset> fallbackList = TMP_Settings.fallbackFontAssets;

            if (fallbackList != null)
                return fallbackList;

            fallbackList = new List<TMP_FontAsset>();
            TMP_Settings.fallbackFontAssets = fallbackList;
            return fallbackList;
        }

        public void Dispose()
        {
            foreach (ProvidedAsset<TMP_FontAsset> asset in providedAssets)
                asset.Dispose();
        }
    }
}
