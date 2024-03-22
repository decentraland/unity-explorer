using DCL.AssetsProvision;
using DCL.Backpack.Breadcrumb;
using DCL.Backpack.EmotesSection;
using DCL.UI;
using System;
using TMPro;
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
        public GameObject NoCategoryResults { get; private set; }

        [field: SerializeField]
        public BackpackBreadCrumbView BreadCrumbView { get; private set; }

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
