using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.Core;
using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>Camera-look input requested by an automation driver, present on the player entity while it lasts. Either a held look delta or an absolute look-at.</summary>
    public struct SyntheticCameraLookIntent : IEcsRequest<SyntheticInputDelivery>
    {
        /// <summary>Cinemachine input-axis value, held while the intent lasts.</summary>
        public Vector2 AxisValue;

        /// <summary>Value of Time.time at which the hold expires. For a look-at, the system overwrites it with the aim-correction deadline.</summary>
        public float EndTime;

        /// <summary>World point to aim the camera at. When set, <see cref="AxisValue" /> is ignored.</summary>
        public Vector3? LookAtTarget;

        /// <summary>Written by the system once the look-at was handed to the camera.</summary>
        public bool LookAtIssued;

        /// <summary>Smallest aim error, in degrees, seen so far.</summary>
        public float LookAtBestErrorDegrees;

        /// <summary>Consecutive correction frames that did not improve on <see cref="LookAtBestErrorDegrees" />.</summary>
        public int LookAtStallFrames;

        public UniTaskCompletionSource<SyntheticInputDelivery>? Completion { get; set; }
    }
}
