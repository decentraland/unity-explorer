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
        private readonly List<ProvidedAsset<TMP_FontAsset>> providedAssets = new();

        public FallbackFontsProvider(IAssetsProvisioner assetsProvisioner, List<AssetReferenceT<TMP_FontAsset>> fallbackFonts, CancellationToken ct)
        {
            LoadAndApplyFallbacksAsync(assetsProvisioner, fallbackFonts, ct).Forget();
        }

        private async UniTask LoadAndApplyFallbacksAsync(IAssetsProvisioner assetsProvisioner, List<AssetReferenceT<TMP_FontAsset>> fallbackFonts, CancellationToken ct)
        {
            try
            {
                foreach (AssetReferenceT<TMP_FontAsset>? fallbackFont in fallbackFonts)
                    providedAssets.Add(await assetsProvisioner.ProvideMainAssetAsync(fallbackFont, ct));

                if (ct.IsCancellationRequested) return;

                List<TMP_FontAsset> fallbackList = TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();

                foreach (ProvidedAsset<TMP_FontAsset> font in providedAssets)
                    if (font.Value != null && !fallbackList.Contains(font.Value))
                        fallbackList.Add(font.Value);

                TMP_Settings.fallbackFontAssets = fallbackList;
            }
            catch (Exception ex)
            {
                // ignore: fallback fonts are optional, avoid crashing chat on load errors
                ReportHub.LogWarning(ReportCategory.TRANSLATE,
                    $"Fallback fonts could not be loaded, some characters may not display correctly. Details: {ex.Message}");
            }
        }

        public void Dispose()
        {
            foreach (ProvidedAsset<TMP_FontAsset> asset in providedAssets)
                asset.Dispose();
        }
    }
}
