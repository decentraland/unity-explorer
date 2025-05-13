using System.Collections.Generic;
using UnityEngine;

namespace Utility
{
    public static class QuaternionUtils
    {
        public const float DEFAULT_ERROR = float.Epsilon;

        /// <summary>
        ///     <inheritdoc cref="QuaternionEqualityComparer" />
        /// </summary>
        public static IEqualityComparer<Quaternion> CreateEqualityComparer(float err = DEFAULT_ERROR) =>
            new QuaternionEqualityComparer(err);

        /// <summary>
        ///     Should not be used in dictionaries or hashsets,
        ///     GetHashCode does not take error into consideration
        /// </summary>
        private class QuaternionEqualityComparer : IEqualityComparer<Quaternion>
        {
            private readonly float allowedError;

            public QuaternionEqualityComparer(float allowedError)
            {
                this.allowedError = allowedError;
            }

            public bool Equals(Quaternion expected, Quaternion actual)
            {
                //I noticed some quaternion comparisons where returning false when both quaternions where exactly the same
                //that is fixed with these comparisons
                if (Mathf.Approximately(expected.x, actual.x) && Mathf.Approximately(expected.y, actual.y) &&
                    Mathf.Approximately(expected.z, actual.z) && Mathf.Approximately(expected.w, actual.w)) { return true; }

                float dot = Mathf.Abs(Quaternion.Dot(expected, actual));

                return dot > 1.0f - allowedError;
            }

            /// <summary>
            ///     Serves as the default hash function.
            /// </summary>
            /// <param name="quaternion">A not null Quaternion</param>
            /// <returns>Returns 0</returns>
            public int GetHashCode(Quaternion quaternion) =>
                0;
        }
    }
}
