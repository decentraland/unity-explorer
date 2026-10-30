using Cysharp.Threading.Tasks;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Utility;

namespace DCL.Settings.ModuleViews
{
    /// <summary>
    ///     Lobby background picker: a cross-fading preview above a grid of one thumbnail per look.
    /// </summary>
    public class SettingsLobbyStagePresetModuleView : SettingsModuleView<SettingsLobbyStagePresetModuleView.Config>
    {
        private CancellationTokenSource? fadeCancellation;

        [field: SerializeField] public RawImage Preview { get; private set; } = null!;
        [field: SerializeField] public AspectRatioFitter PreviewFitter { get; private set; } = null!;
        [field: SerializeField] public RawImage IncomingPreview { get; private set; } = null!;
        [field: SerializeField] public AspectRatioFitter IncomingPreviewFitter { get; private set; } = null!;
        [field: SerializeField] public float PreviewFadeDuration { get; private set; } = 0.15f;
        [field: SerializeField] public RectTransform EntriesContainer { get; private set; } = null!;
        [field: SerializeField] public LobbyStagePresetEntryView EntryPrefab { get; private set; } = null!;
        [field: SerializeField] public CanvasGroup CanvasGroup { get; private set; } = null!;

        /// <summary>
        ///     The texture the preview shows, or is fading to.
        /// </summary>
        public Texture? PreviewTexture { get; private set; }

        public override void SetInteractable(bool interactable)
        {
            CanvasGroup.interactable = interactable;
            CanvasGroup.blocksRaycasts = interactable;
        }

        public override void SetActive(bool isActive) =>
            gameObject.SetActive(isActive);

        public LobbyStagePresetEntryView CreateEntry() =>
            Instantiate(EntryPrefab, EntriesContainer);

        public void SetPreview(Texture? texture)
        {
            if (texture == PreviewTexture) return;

            // A fade cut short keeps its half-shown image as the base of the next one instead of snapping back
            if (IncomingPreview.enabled)
                CoverFit.Apply(Preview, PreviewFitter, PreviewTexture);

            PreviewTexture = texture;
            fadeCancellation = fadeCancellation.SafeRestart();

            // Nothing to blend from an empty frame, and no frames drive a fade while hidden or outside play mode
            if (Preview.texture == null || texture == null || !isActiveAndEnabled || !Application.isPlaying)
            {
                Settle();
                return;
            }

            CoverFit.Apply(IncomingPreview, IncomingPreviewFitter, texture);
            FadeInAsync(fadeCancellation.Token).Forget();
        }

        private void OnDisable()
        {
            fadeCancellation.SafeCancelAndDispose();
            fadeCancellation = null;
            Settle();
        }

        protected override void Configure(Config configuration) =>
            SetInteractable(configuration.IsEnabled);

        private async UniTaskVoid FadeInAsync(CancellationToken ct)
        {
            var elapsed = 0f;
            SetIncomingAlpha(0f);

            while (elapsed < PreviewFadeDuration)
            {
                if (await UniTask.NextFrame(ct).SuppressCancellationThrow()) return;

                elapsed += UnityEngine.Time.deltaTime;
                SetIncomingAlpha(Mathf.Clamp01(elapsed / PreviewFadeDuration));
            }

            Settle();
        }

        private void Settle()
        {
            CoverFit.Apply(Preview, PreviewFitter, PreviewTexture);
            IncomingPreview.enabled = false;
        }

        private void SetIncomingAlpha(float alpha)
        {
            Color color = IncomingPreview.color;
            color.a = alpha;
            IncomingPreview.color = color;
        }

        // A configuration type equal to the base one makes the base's two Configure overloads collide
        [Serializable]
        public class Config : SettingsModuleViewConfiguration { }
    }
}
