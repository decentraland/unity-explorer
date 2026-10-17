using GPUInstancerPro;
using System;
using UnityEngine;

namespace DCL.Landscape.GPUIHelpers
{
    /// <summary>
    ///     On CI GPU is not present and SystemInfo.maxComputeWorkGroupSize will report 0
    ///     that will break the tests as <see cref="GPUIRuntimeSettings.DetermineOperationMode" /> will throw an error. <br />
    ///     Disabling the associated script stops invoking that function
    /// </summary>
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-5000)]
    public class DisableScriptIfGPUNotPresent : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour scriptToDisable = null!;

        private void OnEnable()
        {
            // GPUIRuntimeSettings.cs:
            // if (maxComputeWorkGroupSize < 64)
            // Debug.LogError("Max. Compute Work Group Size is: " + maxComputeWorkGroupSize + ". GPUI requires minimum work group size of 64.");

            if (SystemInfo.maxComputeWorkGroupSize < 64)
                scriptToDisable.enabled = false;
        }
    }
}
