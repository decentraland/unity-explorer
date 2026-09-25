using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Lobby
{
    /// <summary>
    ///     One dot per page of a paged rail, the selected one stretched and tinted. Dots are cloned from an inactive template child.
    /// </summary>
    public class LobbyCarouselDotsView : MonoBehaviour
    {
        private const float ANIMATION_DURATION = 0.2f;

        [SerializeField] private Image dotTemplate = null!;
        [SerializeField] private Color selectedColor = Color.white;
        [SerializeField] private Color unselectedColor = Color.gray;
        [SerializeField] private float selectedWidth = 24f;
        [SerializeField] private float unselectedWidth = 8f;

        private readonly List<Image> dots = new ();

        /// <summary>
        ///     Shows one dot per page, cloning the missing ones and hiding the surplus. A single page needs no navigation hint.
        /// </summary>
        public void Show(int pageCount)
        {
            int visibleDots = pageCount > 1 ? pageCount : 0;

            for (var i = 0; i < visibleDots; i++)
            {
                if (i == dots.Count)
                    dots.Add(Instantiate(dotTemplate, dotTemplate.transform.parent));

                dots[i].gameObject.SetActive(true);
            }

            for (int i = visibleDots; i < dots.Count; i++)
                dots[i].gameObject.SetActive(false);
        }

        public void Select(int page)
        {
            for (var i = 0; i < dots.Count; i++)
            {
                if (!dots[i].gameObject.activeSelf) continue;

                bool selected = i == page;
                RectTransform dot = dots[i].rectTransform;
                dots[i].color = selected ? selectedColor : unselectedColor;

                DOTween.To(() => dot.sizeDelta, size => dot.sizeDelta = size, new Vector2(selected ? selectedWidth : unselectedWidth, dot.sizeDelta.y), ANIMATION_DURATION)
                       .SetEase(Ease.OutCubic)
                       .SetLink(dot.gameObject);
            }
        }
    }
}
