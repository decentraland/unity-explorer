using DCL.ECSComponents;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated
{
    /// <summary>
    ///     Propagated to every scene root entity every frame
    /// </summary>
    public interface IGlobalInputEvents
    {
        IReadOnlyList<Entry> Entries { get; }

        public readonly struct Entry
        {
            public readonly InputAction InputAction;
            public readonly PointerEventType PointerEventType;

            public Entry(InputAction inputAction, PointerEventType pointerEventType)
            {
                InputAction = inputAction;
                PointerEventType = pointerEventType;
            }
        }
    }

    public class GlobalInputEvents : IGlobalInputEvents
    {
        private readonly List<IGlobalInputEvents.Entry> entries = new (10);

        public IReadOnlyList<IGlobalInputEvents.Entry> Entries => entries;

        public void Add(IGlobalInputEvents.Entry entry)
        {
            entries.Add(entry);
        }

        /// <summary>Removes every buffered entry of this action edge.</summary>
        public void Remove(InputAction inputAction, PointerEventType pointerEventType)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].InputAction == inputAction && entries[i].PointerEventType == pointerEventType)
                    entries.RemoveAt(i);
        }

        public void Clear()
        {
            entries.Clear();
        }
    }
}
