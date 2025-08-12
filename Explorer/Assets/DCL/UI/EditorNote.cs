#if UNITY_EDITOR
using UnityEngine;

namespace DCL.UI
{
    public class EditorNote : MonoBehaviour
    {
        [TextArea]
        public string Note;
    }
}
#endif
