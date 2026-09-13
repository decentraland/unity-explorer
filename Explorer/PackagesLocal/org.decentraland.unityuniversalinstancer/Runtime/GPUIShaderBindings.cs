// Serialized anchor for GPUIShaderBindings.asset (loaded by Addressables).
// DCL's tree instancing binds its per-instance transform buffer directly in
// the tree shaders (GPUInstancerSetup.hlsl) rather than through a runtime
// name-to-shader fallback map, so this type carries no runtime state — its
// role is to let the companion .asset deserialize under a stable identity.
// The script GUID is pinned in the .meta to f01694a6d0ad91f4985ea0689f262c1b
// so that .asset reference resolves.

using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUIShaderBindings : ScriptableObject { }
}
