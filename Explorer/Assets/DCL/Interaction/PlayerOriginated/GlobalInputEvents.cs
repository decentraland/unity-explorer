using DCL.ECSComponents;
using System.Collections.Generic;

namespace DCL.Interaction.PlayerOriginated
{
    /// <summary>
    ///     Propagated to every scene root entity every frame
    /// </summary>
    public interface IGlobalInputEvents
    {
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

        IReadOnlyList<Entry> Entries { get; }
    }

    public class GlobalInputEvents : IGlobalInputEvents
    {
        private readonly List<IGlobalInputEvents.Entry> entries = new (10);

        public void Add(IGlobalInputEvents.Entry entry)
        {
            entries.Add(entry);
        }

        public void Clear()
        {
            entries.Clear();
        }

        public IReadOnlyList<IGlobalInputEvents.Entry> Entries => entries;
    }
}
