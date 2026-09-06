// Boot-time loader hosted on Main.unity's "GPUI Runtime Settings" GameObject.
// On Awake it hands its authored GPUIRuntimeSettings ScriptableObject to
// GPUICoreAPI.ApplyRuntimeSettings, which seeds TreeRendererService's fallback
// world-bounds volume from instancingBoundsSize. On hardware that reports a
// compute work-group size below MIN_COMPUTE_WORK_GROUP_SIZE it applies nothing
// and disables itself, leaving the engine defaults in place. The script GUID
// is pinned in the .meta to 97687fbaae00d364e80ab55e4f261a43 so the scene's
// component reference resolves.

using UnityEngine;

namespace GPUInstancerPro
{
    public class GPUIRuntimeSettingsLoader : MonoBehaviour
    {
        // Compute-capable hardware reports a work-group size of at least 64;
        // headless CI and unsupported GPUs report less (or 0). Same threshold
        // DCL.Landscape.GPUIHelpers.DisableScriptIfGPUNotPresent uses.
        private const int MIN_COMPUTE_WORK_GROUP_SIZE = 64;

        [SerializeField] private GPUIRuntimeSettings runtimeSettingsOverwrite;

        private void Awake()
        {
            if (SystemInfo.maxComputeWorkGroupSize < MIN_COMPUTE_WORK_GROUP_SIZE)
            {
                // Leave the authored settings unapplied; the engine keeps its
                // compiled-in defaults.
                enabled = false;
                return;
            }

            if (runtimeSettingsOverwrite == null)
                return;

            GPUICoreAPI.ApplyRuntimeSettings(runtimeSettingsOverwrite);
        }
    }
}
