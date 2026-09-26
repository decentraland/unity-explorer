using System;
using UnityEngine;

namespace DCL.Minimap
{
    public interface ISceneRestrictionsView
    {
        RectTransform SceneRestrictionsIcon { get; set; }
        GameObject RestrictionTextPrefab { get; set; }
        CanvasGroup ToastCanvasGroup { get; set; }
        GameObject ToastTextParent { get; set; }
        float FadeTime { get; set; }
        RectTransform ToastRectTransform { get; set; }

        event Action? OnPointerEnterEvent;
        event Action? OnPointerExitEvent;
    }

    public class SceneRestrictionsView : MonoBehaviour, ISceneRestrictionsView
    {
        [field: SerializeField]
        public RectTransform SceneRestrictionsIcon { get; set; }

        [field: SerializeField]
        public GameObject RestrictionTextPrefab { get; set; }

        [field: SerializeField]
        public CanvasGroup ToastCanvasGroup { get; set; }

        [field: SerializeField]
        public GameObject ToastTextParent { get; set; }

        [field: SerializeField]
        public float FadeTime { get; set; } = 0.3f;

        public RectTransform ToastRectTransform { get; set; }

        public event Action? OnPointerEnterEvent;
        public event Action? OnPointerExitEvent;

        public void OnPointerEnter() => OnPointerEnterEvent?.Invoke();
        public void OnPointerExit() => OnPointerExitEvent?.Invoke();

        private void Awake()
        {
            ToastCanvasGroup.alpha = 0;
            ToastCanvasGroup.gameObject.SetActive(false);
            SceneRestrictionsIcon.gameObject.SetActive(false);
            ToastRectTransform = ToastCanvasGroup.GetComponent<RectTransform>();
        }
    }
}
