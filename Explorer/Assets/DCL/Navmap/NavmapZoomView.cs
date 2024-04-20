using DCL.Audio;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class NavmapZoomView : MonoBehaviour
    {
        [SerializeField] private ZoomInput zoomIn;
        [SerializeField] private ZoomInput zoomOut;

        [field: SerializeField] internal AnimationCurve normalizedZoomCurve { get; private set; }
        [field: SerializeField] internal Vector2Int zoomVerticalRange { get; set; } = new (28, 50);
        [field: SerializeField] internal float scaleDuration { get; private set; } = 0.2f;

        [field: Header("Audio")]
        [field: SerializeField]
        internal AudioClipConfig ZoomInAudio { get; private set; }
        [field: SerializeField]
        internal AudioClipConfig ZoomOutAudio { get; private set; }

        internal ZoomInput ZoomIn => zoomIn;
        internal ZoomInput ZoomOut => zoomOut;



        [Serializable]
        internal class ZoomInput
        {
            private static Color normalColor = new (0f, 0f, 0f, 1f);
            private static Color disabledColor = new (0f, 0f, 0f, 0.5f);

            public Button Button;

            [SerializeField] private Image Image;

            public void SetUiInteractable(bool isInteractable)
            {
                Button.interactable = isInteractable;
                Image.color = isInteractable ? normalColor : disabledColor;
            }
        }
    }
}
