using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class NavmapFilterView : MonoBehaviour
    {
        private const float ANIMATION_TIME = 0.2f;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; }

        [field: SerializeField]
        public RectTransform filterContentTransform;

        [field: SerializeField]
        public DeselectableUiElement FilterContentDeselectable { get; private set; }

        [field: SerializeField]
        public Button filterButton;

        [field: SerializeField]
        public Button infoButton;

        [field: SerializeField]
        public GameObject infoContent;

        private void Start()
        {
            filterButton.onClick.AddListener(OnSortDropdownClick);
            FilterContentDeselectable.gameObject.SetActive(false);
            FilterContentDeselectable.OnDeselectEvent += OnSortDropdownClick;
        }

        private void OnSortDropdownClick()
        {
            if (FilterContentDeselectable.gameObject.activeInHierarchy)
            {
                CanvasGroup.DOFade(0, ANIMATION_TIME).SetEase(Ease.InOutQuad).OnComplete(() => FilterContentDeselectable.gameObject.SetActive(false));
            }
            else
            {
                FilterContentDeselectable.gameObject.SetActive(true);
                FilterContentDeselectable.SelectElement();
                CanvasGroup.DOFade(1, ANIMATION_TIME).SetEase(Ease.InOutQuad);
            }
        }
    }
}
