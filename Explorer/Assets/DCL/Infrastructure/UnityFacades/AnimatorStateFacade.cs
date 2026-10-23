using UnityEngine;

namespace DCL.UnityFacades
{
    /// <summary>
    ///     The narrow view of Mecanim state the project needs. Everything here reads <see cref="AnimatorStateInfo" />
    ///     so that no caller has to; under the shim this assembly is replaced by one backed by the Rust animation
    ///     graph (BevyMigration/docs/README.md § 9).
    /// </summary>
    public static class AnimatorStateFacade
    {
        /// <summary>
        ///     True while the layer's current state is the one with this short name hash.
        /// </summary>
        public static bool IsInState(this Animator animator, int layer, int shortNameHash) =>
            animator.GetCurrentAnimatorStateInfo(layer).shortNameHash == shortNameHash;

        /// <summary>
        ///     True while the layer's current state carries this name (its full path or its short name).
        /// </summary>
        public static bool IsInStateNamed(this Animator animator, int layer, string name) =>
            animator.GetCurrentAnimatorStateInfo(layer).IsName(name);

        /// <summary>
        ///     Progress of the layer's current state: 0 at its start, 1 at its end, above 1 for every loop since.
        /// </summary>
        public static float GetStateNormalizedTime(this Animator animator, int layer) =>
            animator.GetCurrentAnimatorStateInfo(layer).normalizedTime;

        /// <summary>
        ///     Hash of the tag the layer's current state is marked with, 0 when it has none.
        /// </summary>
        public static int GetStateTag(this Animator animator, int layer) =>
            animator.GetCurrentAnimatorStateInfo(layer).tagHash;

        /// <summary>
        ///     Short name hash and normalized time of the layer's current state in one read.
        /// </summary>
        public static void GetState(this Animator animator, int layer, out int shortNameHash, out float normalizedTime)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
            shortNameHash = info.shortNameHash;
            normalizedTime = info.normalizedTime;
        }
    }
}
