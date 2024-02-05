using DCL.AssetsProvision;
using System;
using UnityEngine;

namespace DCL.Backpack
{
    public class HideCategoryGridView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject HideHeader { get; private set; }

        [field: SerializeField]
        public HideRowRef HideRow { get; private set; }

        [field: SerializeField]
        public HideCategoryRef HideCategory { get; private set; }

        [field: SerializeField]
        internal Transform HideCategoryRowsContainer { get; private set; }

        [Serializable]
        public class HideRowRef : ComponentReference<HideCategoryRowView>
        {
            public HideRowRef(string guid) : base(guid) { }
        }

        [Serializable]
        public class HideCategoryRef : ComponentReference<HideCategoryView>
        {
            public HideCategoryRef(string guid) : base(guid) { }
        }
    }
}
