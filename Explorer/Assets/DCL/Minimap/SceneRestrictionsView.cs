using System;
using UnityEngine;

namespace DCL.Minimap
{
    public class SceneRestrictionsView : MonoBehaviour
    {
        [field: SerializeField]
        internal RectTransform sceneRestrictionsIcon { get; private set; }

        [field: SerializeField]
        internal GameObject restrictionTextPrefab { get; private set; }

        [field: SerializeField]
        internal CanvasGroup toastCanvasGroup { get; private set; }

        [field: SerializeField]
        internal GameObject toastTextParent { get; private set; }

        [field: SerializeField]
        internal float fadeTime { get; set; } = 0.3f;

        internal RectTransform toastRectTransform { get; private set; }

        internal event Action? OnPointerEnterEvent;
        internal event Action? OnPointerExitEvent;

        public void OnPointerEnter() => OnPointerEnterEvent?.Invoke();
        public void OnPointerExit() => OnPointerExitEvent?.Invoke();

        private void Awake()
        {
            toastCanvasGroup.alpha = 0;
            sceneRestrictionsIcon.gameObject.SetActive(false);
            toastRectTransform = toastCanvasGroup.GetComponent<RectTransform>();
        }
    }
}
