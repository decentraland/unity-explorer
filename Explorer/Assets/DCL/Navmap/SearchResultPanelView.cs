using DCL.AssetsProvision;
using System;
using UnityEngine;

namespace DCL.Navmap
{
    public class SearchResultPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject NoResultsContainer { get; private set; }

        [field: SerializeField]
        public ResultAssetReference ResultRef { get; private set; }

        [field: SerializeField]
        public RectTransform searchResultsContainer;

        [field: SerializeField]
        public CanvasGroup CanvasGroup { get; private set; }

        [field: SerializeField]
        public Animator panelAnimator;

        private void OnEnable()
        {
            panelAnimator.enabled = true;
        }

        private void OnDisable()
        {
            panelAnimator.enabled = false;
        }

        [Serializable]
        public class ResultAssetReference : ComponentReference<FullSearchResultsView>
        {
            public ResultAssetReference(string guid) : base(guid) { }
        }
    }
}
