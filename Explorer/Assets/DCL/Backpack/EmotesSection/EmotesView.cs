using UnityEngine;

namespace DCL.Backpack
{
    public class EmotesView : MonoBehaviour
    {
        [field: SerializeField]
        public BackpackInfoPanelView BackpackInfoPanelView { get; private set; }

        [field: SerializeField]
        public BackpackGridView GridView { get; private set; }
    }
}
