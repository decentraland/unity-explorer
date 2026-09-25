using DCL.Audio;
using System;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewView : ViewBase, IView
    {
        [field: SerializeField] public GameObject Spinner { get; private set; }
        [field: SerializeField] public RawImage RawImage { get; private set; }
        [field: SerializeField] public CharacterPreviewInputDetector CharacterPreviewInputDetector { get; private set; }
        [field: SerializeField] public CharacterPreviewCursorContainer CharacterPreviewCursorContainer { get; private set; }
        [field: SerializeField] public CharacterPreviewSettingsSO CharacterPreviewSettingsSo { get; private set;}

        [field: Header("Configuration")]
        [field: SerializeField] public bool EnableZooming = true;
        [field: SerializeField] public bool EnablePanning = true;
        [field: SerializeField] public bool EnableRotating = true;

        [field: Header("Audio")]
        [field: SerializeField]
        public AudioClipConfig HoverAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig RotateAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig VerticalPanAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ZoomInAudio { get; private set; }
        [field: SerializeField]
        public AudioClipConfig ZoomOutAudio { get; private set; }

        /// <summary>
        ///     Raised when the view rect is resized, so a render texture sized to it can follow.
        /// </summary>
        public event Action? RectDimensionsChanged;

        private void OnRectTransformDimensionsChange()
        {
            RectDimensionsChanged?.Invoke();
        }
    }
}
