using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using System.Threading;
using DCL.AssetsProvision;
using TMPro;
using UnityEngine.AddressableAssets;

namespace DCL.Chat.ChatServices
{
    public class FallbackFontsProvider : IDisposable
    {
        private List<ProvidedAsset<TMP_FontAsset>> providedAssets;

        public FallbackFontsProvider(IAssetsProvisioner assetsProvisioner, List<AssetReferenceT<TMP_FontAsset>> fallbackFonts, CancellationToken ct)
        {
            LoadAndApplyFallbacksAsync(assetsProvisioner, fallbackFonts, ct).Forget();
        }

        private async UniTask LoadAndApplyFallbacksAsync(IAssetsProvisioner assetsProvisioner, List<AssetReferenceT<TMP_FontAsset>> fallbackFonts, CancellationToken ct)
        {
            try
            {
                providedAssets = new List<ProvidedAsset<TMP_FontAsset>>();

                foreach (AssetReferenceT<TMP_FontAsset>? fallbackFont in fallbackFonts)
                    providedAssets.Add(await assetsProvisioner.ProvideMainAssetAsync(fallbackFont, ct));

                if (ct.IsCancellationRequested) return;

                List<TMP_FontAsset> fallbackList = TMP_Settings.fallbackFontAssets ?? new List<TMP_FontAsset>();

                foreach (ProvidedAsset<TMP_FontAsset> font in providedAssets)
                    if (font.Value != null && !fallbackList.Contains(font.Value))
                        fallbackList.Add(font.Value);

                TMP_Settings.fallbackFontAssets = fallbackList;
            }
            catch (Exception)
            {
                // ignore: fallback fonts are optional, avoid crashing chat on load errors
            }
        }

        public void Dispose()
        {
            foreach (ProvidedAsset<TMP_FontAsset> asset in providedAssets)
                asset.Dispose();
        }
    }
}
