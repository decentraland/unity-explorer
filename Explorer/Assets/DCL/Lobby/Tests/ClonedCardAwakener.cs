using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DCL.Lobby.Tests
{
    /// <summary>
    ///     Runs the Awake that edit mode skips on cards a rail clones from an inactive template: activating such a clone does
    ///     not invoke its lifecycle callbacks, so whatever Awake wires - button listeners, hover subscriptions - stays dead.
    ///     A card is awakened at most once, so its listeners are never wired twice.
    /// </summary>
    public class ClonedCardAwakener
    {
        private readonly HashSet<Component> awakened = new ();

        public T Awaken<T>(T card) where T: Component
        {
            if (awakened.Add(card))
                typeof(T).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(card, null);

            return card;
        }
    }
}
