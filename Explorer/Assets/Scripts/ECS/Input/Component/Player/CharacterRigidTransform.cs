using UnityEngine;

namespace ECS.CharacterMotion.Components
{
    /// <summary>
    ///     Characters do not contain rigid bodies but are driven by <see cref="CharacterController" />.
    ///     But all calculations of velocity happen in FixedUpdate.
    ///     <para>
    ///         Rotation is not driven by Rigid Transform but evaluated directly in Update as it does not impact Physics interactions
    ///     </para>
    ///     <para>
    ///         This component should be reused for other avatars to enable interpolation
    ///     </para>
    /// </summary>
    public class CharacterRigidTransform
    {
        /// <summary>
        ///     Last time when component was modified
        /// </summary>
        public float ModificationTimestamp;

        /// <summary>
        ///     Used for the interpolating purpose, assigned before <see cref="TargetPosition" /> is calculated
        /// </summary>
        public Vector3 PreviousTargetPosition;

        /// <summary>
        ///     Target Position the object tends to
        /// </summary>
        public Vector3 TargetPosition;
    }
}
