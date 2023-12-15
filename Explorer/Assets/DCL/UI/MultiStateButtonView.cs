using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI
{
    public class MultiStateButtonView : MonoBehaviour
    {
        [field: SerializeField]
        internal Button button { get; private set; }

        [field: SerializeField]
        internal GameObject buttonImageFill { get; private set; }

        [field: SerializeField]
        internal GameObject buttonImageOutline { get; private set; }
    }
}
