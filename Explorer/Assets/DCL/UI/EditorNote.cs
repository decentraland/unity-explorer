#if UNITY_EDITOR
using UnityEngine;

namespace DCL.UI
{
    public class EditorNote : MonoBehaviour
    {
        [TextArea(3,20)]
        public string Note = string.Empty;
    }
}
#endif
