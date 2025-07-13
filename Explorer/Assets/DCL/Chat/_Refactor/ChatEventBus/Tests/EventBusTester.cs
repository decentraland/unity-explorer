using UnityEngine;

namespace DCL.Chat.EventBus.Tests
{
    public struct PlayerJumpedEvent
    {
        public float JumpHeight;
    }

    public class EventBusTester : MonoBehaviour
    {
        private IEventBus eventBus;
        private EventSubscriptionScope subscriptionScope;

        void Awake()
        {
            eventBus = new EventBus();
            subscriptionScope = new EventSubscriptionScope();

            subscriptionScope.Add(
                eventBus.Subscribe<PlayerJumpedEvent>(HandlePlayerJumped)
            );
        }
        
        [ContextMenu("Publish PlayerJumpedEvent")]
        public void PublishPlayerJumpedEvent()
        {
            eventBus.Publish(new PlayerJumpedEvent { JumpHeight = 5.0f });
        }

        [ContextMenu("Dispose Subscriptions")]
        public void DisposeSubscriptions()
        {
            subscriptionScope.Dispose();
        }

        private void HandlePlayerJumped(PlayerJumpedEvent evt)
        {
            Debug.Log($"EVENT RECEIVED: Player jumped with height: {evt.JumpHeight}");
        }

        void OnDestroy()
        {
            Debug.Log("EventBusTester is being destroyed. Cleaning up subscriptions.");
            subscriptionScope?.Dispose();
        }
    }
}