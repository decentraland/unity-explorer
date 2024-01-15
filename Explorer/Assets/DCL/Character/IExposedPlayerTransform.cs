using UnityEngine;

namespace DCL.Character
{
    /// <summary>
    ///     Allows to access the player transform from the scene world
    /// </summary>
    public interface IExposedPlayerTransform
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }

        /// <summary>
        ///     Where the transform was changed after movement has been interpolated.
        ///     It take into consideration the actual player position affected by Physics.
        /// </summary>
        bool IsDirty { get; }
    }
}
