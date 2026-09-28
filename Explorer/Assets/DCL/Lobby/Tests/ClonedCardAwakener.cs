using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DCL.Lobby.Tests
{
    /// <summary>
    ///     Runs the Awake edit mode skips on cards cloned from an inactive template, at most once per card so listeners are never wired twice.
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
