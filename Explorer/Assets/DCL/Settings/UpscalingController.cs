using DCL.CharacterPreview;
using DCL.Quality.Runtime;
using System;
using System.Collections.Generic;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DCL.Utilities
{
    /// <summary>
    ///     Applies the user's Resolution Scale and forces 100% while anything renders into a transparent RenderTexture.
    /// </summary>
    public class UpscalingController : IDisposable
    {
        private const float FULL_RENDER_SCALE = 1f;

        private readonly CharacterPreviewEventBus characterPreviewEventBus;

        // Keyed by owner so a release without a require, or a repeated require, cannot unbalance the override.
        private readonly HashSet<object> fullRenderScaleRequesters = new ();

        private float savedUserRenderScale;

        public UpscalingController(CharacterPreviewEventBus characterPreviewEventBus)
        {
            this.characterPreviewEventBus = characterPreviewEventBus;
            characterPreviewEventBus.OnAnyShownChangedEvent += OnAnyCharacterPreviewShownChanged;
        }

        public void Dispose()
        {
            characterPreviewEventBus.OnAnyShownChangedEvent -= OnAnyCharacterPreviewShownChanged;
        }

        //Should always get in decimal form
        public void UpdateUpscaling(float newValue)
        {
            if (fullRenderScaleRequesters.Count > 0)
                savedUserRenderScale = newValue;
            else
                URPSettingsApplier.ApplyUpscaling(newValue, UpscalingFilterSelection.FSR);
        }

        public void RequireFullRenderScale(object requester)
        {
            if (!fullRenderScaleRequesters.Add(requester) || fullRenderScaleRequesters.Count > 1) return;

            savedUserRenderScale = ((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).renderScale;

            // FSR keeps running at 100% and writes alpha 1, which turns transparent render targets black.
            URPSettingsApplier.ApplyUpscaling(FULL_RENDER_SCALE, UpscalingFilterSelection.Auto);
        }

        public void ReleaseFullRenderScale(object requester)
        {
            if (!fullRenderScaleRequesters.Remove(requester) || fullRenderScaleRequesters.Count > 0) return;

            UpdateUpscaling(savedUserRenderScale);
        }

        private void OnAnyCharacterPreviewShownChanged(bool anyShown)
        {
            if (anyShown)
                RequireFullRenderScale(characterPreviewEventBus);
            else
                ReleaseFullRenderScale(characterPreviewEventBus);
        }
    }
}
