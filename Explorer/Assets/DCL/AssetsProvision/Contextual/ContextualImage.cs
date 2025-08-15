using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using Utility.Ownership;
using Utility.Types;

namespace DCL.AssetsProvision
{
    // Can be improved further with prewarming by asset groups
    [RequireComponent(typeof(Image))]
    public class ContextualImage : MonoBehaviour
    {
        [SerializeField] private Image image = null!;
        [SerializeField] private AssetReferenceT<Sprite> spriteAsset = null!;

        private ContextualAsset<Sprite> asset = null!;

        private void Awake()
        {
            if (image.sprite != null)
                ReportHub.LogError(ReportCategory.UI, "Image must not have a sprite to avoid hard linking the sprite into memory, when sprite is linked directly the contextual load won't apply optimization effect");

            asset = new ContextualAsset<Sprite>(spriteAsset.EnsureNotNull("reference != null"));
        }

        private void OnEnable()
        {
            if (asset.CurrentState is ContextualAsset<Sprite>.State.UNLOADED)
                LoadAsync().Forget();
        }

        private async UniTask LoadAsync()
        {
            Weak<Sprite> sprite = await asset.AssetAsync(destroyCancellationToken);
            Option<Sprite> resource = sprite.Resource;

            if (resource.Has) image.sprite = resource.Value;
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
                ContextualAsset<Sprite>.State.UNLOADED => LoadAsync(),
                ContextualAsset<Sprite>.State.LOADING => UniTask.WaitWhile(() => asset.CurrentState is ContextualAsset<Sprite>.State.LOADING, cancellationToken: token),
                ContextualAsset<Sprite>.State.LOADED => UniTask.CompletedTask,
                _ => throw new ArgumentOutOfRangeException()
            };
    }
}
