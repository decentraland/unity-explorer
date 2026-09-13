// Serialized anchor for GPUIPRO_TreeManager.prefab. In DCL's integration the
// tree instancing pipeline is driven directly through GPUICoreAPI /
// TreeRendererService (LandscapePlugin registers each prototype), so this
// scene-manager component owns no runtime behaviour; the prefab is a dormant
// asset that exists only for identity. The script GUID is pinned in the .meta
// to bc071a606fd17964d988019d4f90155e so the prefab's m_Script reference
// resolves.

using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUITreeManager : MonoBehaviour { }
}
