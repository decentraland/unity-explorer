using System;
using UnityEngine;
using Utility;

namespace MVC
{
    /// <summary>
    ///     Events bus that can be used pop events from views without injecting manually from the code
    /// </summary>
    public class ViewEventBus : MonoBehaviour, IEventBus
    {
        private readonly EventBus eventBusImplementation = new (true);

        public void Publish<T>(T evt)
        {
            eventBusImplementation.Publish(evt);
        }

        public IDisposable Subscribe<T>(Action<T> handler) =>
            eventBusImplementation.Subscribe(handler);
    }
}
