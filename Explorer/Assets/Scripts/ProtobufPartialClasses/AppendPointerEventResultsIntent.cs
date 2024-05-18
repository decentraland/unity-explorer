using Unity.Collections;
using UnityEngine;

namespace DCL.ECSComponents
{
    /// <summary>
    ///     <para>
    ///         Added from the global system to an entity that was validly hit by a raycast originated from player
    ///     </para>
    ///     <para>
    ///         Accompanies <see cref="PBPointerEvents" />: must be added and deleted along with the component
    ///     </para>
    /// </summary>
    public struct AppendPointerEventResultsIntent
    {
        public Ray Ray { get; private set; }
        public UnityEngine.RaycastHit RaycastHit { get; private set; }

        /// <summary>
        ///     Contains the indices of the <see cref="PBPointerEvents.Types.Entry" /> in this entity's <see cref="PBPointerEvents" />
        ///     that were validly hit and checked by the raycast logic.
        ///     <para>
        ///         The number of valid entries is limited to 64 elements (it's unlikely to be exceeded)
        ///     </para>
        /// </summary>
        public FixedList64Bytes<byte> ValidIndices;

        public void Initialize(UnityEngine.RaycastHit raycastHit, Ray ray)
        {
            RaycastHit = raycastHit;
            Ray = ray;
            ValidIndices.Clear();
        }
    }

    public partial class PBPointerEvents
    {
        public AppendPointerEventResultsIntent AppendPointerEventResultsIntent;
    }
}
