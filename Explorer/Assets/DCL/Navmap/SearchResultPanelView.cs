using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

public class SearchResultPanelView : MonoBehaviour
{
    [field: SerializeField]
    public AssetReferenceGameObject ResultAssetReference { get; private set; }

    [field: SerializeField]
    public RectTransform searchResultsContainer;
}
