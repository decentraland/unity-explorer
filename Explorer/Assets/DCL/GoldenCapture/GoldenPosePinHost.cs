using UnityEngine;

namespace DCL.GoldenCapture
{
    /// <summary>
    ///     Re-applies the skeleton poses <see cref="GoldenFreeze"/> pinned for the freeze. LateUpdate runs after
    ///     the animators wrote their pose for the frame and before the avatar bone gather at the end of
    ///     PreLateUpdate, so the skinning matrices are built from the pinned pose rather than from the
    ///     animator's default values for the bones its state does not key. The execution order puts this
    ///     LateUpdate after every other script's, so no later bone writer undoes the pin.
    /// </summary>
    [DefaultExecutionOrder(32000)]
    internal sealed class GoldenPosePinHost : MonoBehaviour
    {
        private void LateUpdate() =>
            GoldenFreeze.ApplyPinnedPoses(fromLateUpdate: true);
    }
}
