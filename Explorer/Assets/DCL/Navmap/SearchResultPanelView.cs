using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.Navmap
{
    public class SearchResultPanelView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject LoadingContainer { get; private set; }

        [field: SerializeField]
        public ResultAssetReference ResultRef { get; private set; }

        [field: SerializeField]
        public RectTransform searchResultsContainer;

        [Serializable]
        public class ResultAssetReference : ComponentReference<FullSearchResultsView>
        {
            public ResultAssetReference(string guid) : base(guid) { }
        }
    }
}
