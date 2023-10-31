using DCL.UI;
using MVC;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Navmap
{
    public class NavmapView : MonoBehaviour
    {
        [field: SerializeField]
        public GameObject satellite;

        [field: SerializeField]
        public GameObject streetView;

        [field: SerializeField]
        public TabSelectorView[] TabSelectorViews { get; private set; }
    }
}
