using Cysharp.Threading.Tasks;
using DCL.Communities;
using DCL.Diagnostics;
using DCL.UI;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.Shop
{
    public static class ShopThumbnails
    {
        private static readonly int[] RETRY_DELAYS_MS = { 11_000, 21_000 };

        public static async UniTaskVoid LoadWithRetryAsync(ThumbnailLoader loader, string? url, ImageView view, Sprite? fallback, CancellationToken ct)
        {
            view.ImageColor = Color.clear;
            view.SetImage(fallback!, true);
            view.IsLoading = true;

            Sprite? sprite = null;

            if (!string.IsNullOrEmpty(url) && loader.Cache != null)
            {
                for (var attempt = 0;; attempt++)
                {
                    try { sprite = await loader.Cache.GetSpriteAsync(url, true, ct: ct); }
                    catch (OperationCanceledException) { return; }
                    catch (Exception e) { ReportHub.LogException(e, new ReportData(ReportCategory.UI)); }

                    if (ct.IsCancellationRequested)
                        return;

                    if (sprite != null || attempt >= RETRY_DELAYS_MS.Length)
                        break;

                    if (await UniTask.Delay(RETRY_DELAYS_MS[attempt], cancellationToken: ct).SuppressCancellationThrow())
                        return;
                }
            }

            view.IsLoading = false;

            if (sprite != null)
                view.SetImage(sprite, true);

            view.ShowImageAnimated();
        }
    }
}
