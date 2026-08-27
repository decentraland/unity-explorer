using Cysharp.Threading.Tasks;
using DCL.SyntheticInput.Core;
using UnityEngine;

namespace DCL.SyntheticInput.Components
{
    /// <summary>
    ///     Camera-look input requested by an automation driver, present on the player entity while it lasts.
    ///     Either a held look delta (<see cref="AxisValue" /> re-asserted into the camera input each frame until
    ///     <see cref="EndTime" />) or an absolute look-at (<see cref="LookAtTarget" />, translated by
    ///     SyntheticCameraLookSystem into the production camera look-at intent and completed once consumed).
    /// </summary>
    public struct SyntheticCameraLookIntent : IEcsRequest<SyntheticInputDelivery>
    {
        /// <summary>Cinemachine input-axis value held while the intent lasts; ignored when <see cref="LookAtTarget" /> is set.</summary>
        public Vector2 AxisValue;

        /// <summary>Value of Time.time at which the hold expires; ignored for a look-at.</summary>
        public float EndTime;

        /// <summary>Absolute world point to aim the camera at; when set, <see cref="AxisValue" /> and <see cref="EndTime" /> are ignored.</summary>
        public Vector3? LookAtTarget;

        /// <summary>Set once the look-at was handed to the camera; the request completes when the camera consumed it.</summary>
        public bool LookAtIssued;

        public UniTaskCompletionSource<SyntheticInputDelivery>? Completion { get; set; }
    }
}
