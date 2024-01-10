using DCL.AssetsProvision;
using DCL.UI;
using System;
using UnityEngine;

namespace DCL.Backpack
{
    public class BackpackGridView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackItemRef BackpackItem { get; private set; }

        [field: SerializeField]
        public PageSelectorView PageSelectorView { get; private set; }

        [Serializable]
        public class BackpackItemRef : ComponentReference<BackpackItemView>
        {
            public BackpackItemRef(string guid) : base(guid) { }
        }
    }
}
