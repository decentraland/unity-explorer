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

            public bool Equals(Quaternion expected, Quaternion actual) =>
                Mathf.Abs(Quaternion.Dot(expected, actual)) > 1.0f - allowedError;

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
