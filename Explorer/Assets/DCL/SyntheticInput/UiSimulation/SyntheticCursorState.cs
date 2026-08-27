namespace DCL.SyntheticInput.UiSimulation
{
    /// <summary>
    ///     Frame-stamped flag raised by the virtual-device gesture pipeline while it steers the pointer, so the
    ///     cursor systems skip their OS-cursor warps instead of fighting the injected positions. Static by design
    ///     (the same pattern as PhysicsTickProvider): retail builds never write it, and the frame stamp makes it
    ///     expire on its own — an aborted gesture leaves no residue to sweep.
    /// </summary>
    public static class SyntheticCursorState
    {
        private static int suppressOsWarpUntilFrame = -1;

        /// <summary>An automation gesture is steering the pointer this frame; skip OS-cursor warps.</summary>
        public static bool SuppressOsWarp => UnityEngine.Time.frameCount <= suppressOsWarpUntilFrame;

        /// <summary>Keeps the suppression alive through the next frame; re-assert it every gesture frame.</summary>
        public static void AssertSuppressionThisFrame() =>
            suppressOsWarpUntilFrame = UnityEngine.Time.frameCount + 1;

        /// <summary>Test hygiene only: editor tests share frames, so an asserted suppression would leak between fixtures.</summary>
        internal static void Reset() =>
            suppressOsWarpUntilFrame = -1;
    }
}
