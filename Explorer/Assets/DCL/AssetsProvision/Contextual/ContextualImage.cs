using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using RichTypes;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;

namespace DCL.AssetsProvision
{
    // Can be improved further with prewarming by asset groups
    [RequireComponent(typeof(Image))]
    public class ContextualImage : MonoBehaviour
    {
        [SerializeField] private Image image = null!;
        [SerializeField] private AssetReferenceT<Sprite> spriteAsset = null!;
        [SerializeField] private Color unloadedColor = Color.white;
        [SerializeField] private Color loadedColor = Color.white;

        private ContextualAsset<Sprite> asset = null!;

        private void Awake()
        {
            if (image.sprite != null)
                ReportHub.LogError(ReportCategory.UI, "Image must not have a sprite to avoid hard linking the sprite into memory, when sprite is linked directly the contextual load won't apply optimization effect");

            asset = new ContextualAsset<Sprite>(spriteAsset.EnsureNotNull("reference != null"));
        }

        private void OnEnable()
        {
            if (asset.CurrentState is ContextualAsset<Sprite>.State.Unloaded)
                LoadAsync().Forget();
        }

        private async UniTask LoadAsync()
        {
            image.color = unloadedColor;
            Weak<Sprite> sprite = await asset.AssetAsync(destroyCancellationToken);
            Option<Sprite> resource = sprite.Resource;

            if (resource.Has)
            {
                image.sprite = resource.Value;
                image.color = loadedColor;
            }
            else ReportHub.LogError(ReportCategory.UI, "Cannot load grid asset");
        }

        private void OnDisable()
        {
            image.sprite = null!;
            asset.Release();
        }

        private void OnDestroy()
        {
            image.sprite = null!;
            asset.Dispose();
        }

        public UniTask TriggerOrWaitReadyAsync(CancellationToken token) =>
            asset.CurrentState switch
            {
                ContextualAsset<Sprite>.State.Unloaded => LoadAsync(),
                ContextualAsset<Sprite>.State.Loading => UniTask.WaitWhile(() => asset.CurrentState is ContextualAsset<Sprite>.State.Loading, cancellationToken: token),
                ContextualAsset<Sprite>.State.Loaded => UniTask.CompletedTask,
                _ => throw new ArgumentOutOfRangeException()
            };
    }
}
