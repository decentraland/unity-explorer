using DG.Tweening;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Backpack
{
    public class BackpackSortDropdownView : MonoBehaviour
    {
        [field: SerializeField]
        internal Toggle sortNewest { get; private set; }

        [field: SerializeField]
        internal Toggle sortOldest { get; private set; }

        [field: SerializeField]
        internal Toggle sortRarest { get; private set; }

        [field: SerializeField]
        internal Toggle sortLessRares { get; private set; }

        [field: SerializeField]
        internal Toggle sortNameAz { get; private set; }

        [field: SerializeField]
        internal Toggle sortNameZa { get; private set; }

        [field: SerializeField]
        internal Toggle collectiblesOnly { get; private set; }

        [field: SerializeField]
        internal Button sortDropdownButton { get; private set; }

        [field: SerializeField]
        internal RectTransform sortContent { get; private set; }

        private Tween openCloseTween;
        private readonly Vector3 startContentPosition = new Vector3(1, 0, 1);

        private void Start()
        {
            sortDropdownButton.onClick.AddListener(OnSortDropdownClick);
            sortContent.gameObject.SetActive(false);
        }

        private void OnSortDropdownClick()
        {
            if (sortContent.gameObject.activeInHierarchy)
            {
                openCloseTween?.Kill();
                sortContent.localScale = Vector3.one;
                openCloseTween = sortContent.DOScaleY(0, 0.3f)
                                            .SetEase(Ease.Flash)
                                            .OnComplete(() => sortContent.gameObject.SetActive(false));
            }
            else
            {
                openCloseTween?.Kill();
                sortContent.gameObject.SetActive(true);
                sortContent.localScale = startContentPosition;
                openCloseTween = sortContent.DOScaleY(1, 0.3f).SetEase(Ease.Flash);
            }
        }
    }
}
