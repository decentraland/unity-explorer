namespace DCL.AvatarRendering.AvatarShape.Components
{
    /// <summary>
    ///     The single definition of when an avatar may skip its skinning dispatch, shared between
    ///     FinishAvatarMatricesCalculationSystem, which acts on it, and the editor gizmo, which reports it. It lives
    ///     outside the system so the debug view cannot drift from the decision it shows.
    /// </summary>
    public static class AvatarCullingRule
    {
        /// <param name="exemptFromCulling">
        ///     True for the main player, whose pose is sampled by reflections and portraits outside the player
        ///     frustum, and for preview avatars, which their own camera draws into a render texture.
        /// </param>
        public static bool IsCulled(bool exemptFromCulling, bool isVisible, bool isInFrustum) =>
            !exemptFromCulling && (!isVisible || !isInFrustum);
    }
}
