using System.Collections.Generic;
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

        public readonly IReadOnlyDictionary<InputAction, PointerEventType> ValidInputActions => validInputActions;

        /// <summary>
        ///     Contains the indices of the <see cref="PBPointerEvents.Types.Entry" /> in this entity's <see cref="PBPointerEvents" />
        ///     that were validly hit and checked by the raycast logic.
        ///     <para>
        ///         The number of valid entries is limited to 64 elements (it's unlikely to be exceeded)
        ///     </para>
        /// </summary>
        private FixedList64Bytes<byte> validIndices;

        private Dictionary<InputAction, PointerEventType> validInputActions;

        public void Initialize(UnityEngine.RaycastHit raycastHit, Ray ray)
        {
            RaycastHit = raycastHit;
            Ray = ray;
            validIndices.Clear();

            if (validInputActions == null) validInputActions = new ();
            else validInputActions.Clear();
        }

        public void AddValidIndex(byte index)
        {
            validIndices.Add(index);
        }

        public readonly byte ValidIndexAt(int at) =>
            validIndices[at];

        public readonly int ValidIndicesCount() =>
            validIndices.Length;

        public void AddInputAction(InputAction ecsInputAction, PointerEventType pointerEventType)
        {
            validInputActions.Add(ecsInputAction, pointerEventType);
        }

        public void Clear()
        {
            validIndices.Clear();
            validInputActions?.Clear();
        }

        /// <summary>
        ///     Adds hover input if the entry is qualified for listening to it
        ///     <para>
        ///         Entry is qualified if the expected Button is "Pointer" or "Any", and event type is corresponding "HoverEnter"/"HoverExit"
        ///     </para>
        /// </summary>
        public void TryAppendHoverInput(PointerEventType? hoverEventType, in PBPointerEvents.Types.Entry entry, int entryIndex)
        {
            if (!hoverEventType.HasValue) return;

            if (entry.EventType == hoverEventType.Value
                && entry.EventInfo.Button is InputAction.IaPointer or InputAction.IaAny
               )
                AddValidIndex((byte)entryIndex);
        }
    }

    public partial class PBPointerEvents
    {
        public AppendPointerEventResultsIntent AppendPointerEventResultsIntent;

        public void Reset()
        {
            PointerEvents?.Clear();
            AppendPointerEventResultsIntent.Clear();
        }
    }
}
