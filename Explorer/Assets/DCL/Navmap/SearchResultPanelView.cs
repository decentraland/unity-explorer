using DCL.AssetsProvision;
using System;
using UnityEngine;
using UnityEngine.UI;

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

        [Serializable]
        public class ResultAssetReference : ComponentReference<PlaceElementView>
        {
            public ResultAssetReference(string guid) : base(guid) { }
        }
    }
}
