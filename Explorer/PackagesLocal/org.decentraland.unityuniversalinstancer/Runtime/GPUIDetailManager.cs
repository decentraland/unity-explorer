// REQ-032: serialized anchor for GPUIPRO_DetailsManager.prefab, whose
// MonoBehaviour references script GUID 0293089e577d0ef41bf2ac4c44548d66 —
// pinned in this file's .meta, and matched to this type by the file name, so
// the prefab resolves instead of importing as a missing script. The prefab is
// dormant in DCL's integration: instancing runs through GPUICoreAPI /
// TreeRendererService, so this component owns no runtime behaviour.

using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUIDetailManager : MonoBehaviour { }
}
