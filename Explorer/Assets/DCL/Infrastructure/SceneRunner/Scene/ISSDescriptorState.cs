using System;
using UnityEngine;

namespace DCL.SceneRunner.Scene
{
    /// <summary>
    ///     Resolution outcome for a scene's Initial Scene State. Carried on
    ///     <see cref="ISSDescriptor.CurrentState"/>.
    /// </summary>
    public enum ISSDescriptorState
    {
        /// <summary>
        ///     The descriptor has not yet been resolved. The radius gate spawns the resolver promise when it sees this state.
        /// </summary>
        Uninitialized,
        None,
        Bundle,
        Descriptor,
    }

    [Serializable]
    public struct ISSDescriptorAsset
    {
        public string hash;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
}
