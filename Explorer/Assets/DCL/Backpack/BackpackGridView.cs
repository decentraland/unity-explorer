using DCL.AssetsProvision;
using System;
using UnityEngine;

namespace DCL.Backpack
{
    public class BackpackGridView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackItemRef BackpackItem { get; private set; }

        [Serializable]
        public class BackpackItemRef : ComponentReference<BackpackItemView>
        {
            public BackpackItemRef(string guid) : base(guid) { }
        }
    }
}
