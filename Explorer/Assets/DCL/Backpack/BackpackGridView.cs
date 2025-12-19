using DCL.AssetsProvision;
using DCL.Backpack.Breadcrumb;
using DCL.Backpack.EmotesSection;
using DCL.UI;
using DG.Tweening;
using System;
using UnityEngine;

namespace DCL.Backpack
{
    public class BackpackGridView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackItemRef BackpackItem { get; private set; }

        [field: SerializeField]
        public BackpackEmoteGridItemRef EmoteGridItem { get; private set; }

        [field: SerializeField]
        public PageSelectorView PageSelectorView { get; private set; }

        [field: SerializeField]
        public GameObject RegularResults { get; private set; }

        [field: SerializeField]
        public GameObject NoSearchResults { get; private set; }

        [field: SerializeField]
        public TMP_Text_ClickeableLink NoSearchResultsMarketplaceTextLink { get; private set; }

        [field: SerializeField]
        public GameObject NoCategoryResults { get; private set; }

        [field: SerializeField]
        public TMP_Text_ClickeableLink NoCategoryResultsMarketplaceTextLink { get; private set; }

        [field: SerializeField]
        public BackpackBreadCrumbView BreadCrumbView { get; private set; }

        [field: SerializeField]
        public CanvasGroup LoadingCanvasGroup { get; private set; }

        [field: SerializeField]
        public GameObject LoadingSpinner { get; private set; }

        public void SetLoading(bool isLoading)
        {
            LoadingCanvasGroup.DOKill();
            LoadingCanvasGroup.blocksRaycasts = !isLoading;
            LoadingSpinner.SetActive(isLoading);

            LoadingCanvasGroup.DOFade( isLoading ? 0.2f : 1f, 0.3f);
        }

        [Serializable]
        public class BackpackItemRef : ComponentReference<BackpackItemView>
        {
            public BackpackItemRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class BackpackEmoteGridItemRef : ComponentReference<BackpackEmoteGridItemView>
        {
            public BackpackEmoteGridItemRef(string guid) : base(guid) { }
        }
    }
}
