// Canvas-controller component on GPUIDebuggerCanvas.prefab, which
// GPUIDebugSystem instantiates when the LANDSCAPE_GPUI "Debug" button is
// pressed. The prefab is a self-contained UGUI canvas; this component is the
// typed handle GPUIDebugSystem holds and instantiates, and carries no
// per-frame state of its own. The script GUID is pinned in the .meta to
// a9f8d1ff44dcb5c4b944b77a33d6283e so the prefab's m_Script reference resolves.

using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUIDebuggerCanvas : MonoBehaviour { }
}
