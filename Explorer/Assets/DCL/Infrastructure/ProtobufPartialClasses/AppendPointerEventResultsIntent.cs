﻿using JetBrains.Annotations;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

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

        public readonly IReadOnlyList<(InputAction inputAction, PointerEventType pointerEventType)> ValidInputActions => validInputActions;

        /// <summary>
        ///     Contains the indices of the <see cref="PBPointerEvents.Types.Entry" /> in this entity's <see cref="PBPointerEvents" />
        ///     that were validly hit and checked by the raycast logic.
        ///     <para>
        ///         The number of valid entries is limited to 64 elements (it's unlikely to be exceeded)
        ///     </para>
        ///     <para>
        ///         replaced to list because there was a bug with the fixed buffer
        ///     </para>
        /// </summary>
        private List<byte> validIndices;

        private List<(InputAction inputAction, PointerEventType pointerEventType)> validInputActions;

        public void Initialize(UnityEngine.RaycastHit raycastHit, Ray ray)
        {
            RaycastHit = raycastHit;
            Ray = ray;
            Clear();
        }

        public void InitializeWithAlloc()
        {
            Initialize(new List<byte>(), new List<(InputAction, PointerEventType)>());
        }

        public void Initialize(List<byte> list, List<(InputAction, PointerEventType)> dictionary)
        {
            validIndices = list;
            validInputActions = dictionary;
            Clear();
        }

        public void Release(
            [NotNull] IObjectPool<List<byte>> listPool,
            [NotNull] IObjectPool<List<(InputAction, PointerEventType)>> dictionaryPool
        )
        {
            listPool.Release(validIndices);
            dictionaryPool.Release(validInputActions);
            validIndices = null;
            validInputActions = null;
        }

        public void AddValidIndex(byte index)
        {
            validIndices!.Add(index);
        }

        public readonly byte ValidIndexAt(int at) =>
            validIndices![at];

        public readonly int ValidIndicesCount() =>
            validIndices?.Count ?? 0;

        public void AddInputAction(InputAction ecsInputAction, PointerEventType pointerEventType)
        {
            validInputActions!.Add((ecsInputAction, pointerEventType));
        }

        public void Clear()
        {
            validIndices?.Clear();
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
