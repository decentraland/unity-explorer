using System;
using System.Threading;

namespace DCL.Utilities
{
    public interface IReactiveProperty<T> : IReadonlyReactiveProperty<T>
    {
        void UpdateValue(T value);

        void ClearSubscriptionsList();
    }

    public interface IReadonlyReactiveProperty<out T>
    {
        event Action<T> OnUpdate;

        T Value { get; }
    }

    public static class ReactivePropertyExtensions
    {
        /// <summary>
        ///     Reacts immediately when the property is not null or waits until it is not null.
        /// </summary>
        public static void ReactOnceWhenNotNull<T, TContext>(this IReadonlyReactiveProperty<T?> property, TContext context, Action<T, TContext> onValueChanged, CancellationToken ct) where T: struct
        {
            if (property.Value.HasValue)
                onValueChanged(property.Value.Value, context);
            else
            {
                DisposableSubscription<T?> subscription = default;

                subscription = property.Subscribe(value =>
                {
                    if (value.HasValue)
                    {
                        if (!ct.IsCancellationRequested)
                            onValueChanged(value.Value, context);

                        // ReSharper disable once AccessToModifiedClosure
                        subscription.Dispose(); // Unsubscribe after the first value is received
                    }
                });
            }
        }

        public static DisposableSubscription<T> Subscribe<T>(this IReadonlyReactiveProperty<T> property, Action<T> observer)
        {
            property.OnUpdate += observer;
            return new DisposableSubscription<T>(property, observer);
        }

        public static void Unsubscribe<T>(this IReadonlyReactiveProperty<T> property, Action<T> observer)
        {
            property.OnUpdate -= observer;
        }

        public readonly struct DisposableSubscription<T> : IDisposable
        {
            private readonly IReadonlyReactiveProperty<T> reactiveProperty;
            private readonly Action<T> observer;

            public DisposableSubscription(IReadonlyReactiveProperty<T> reactiveProperty, Action<T> observer)
            {
                this.reactiveProperty = reactiveProperty;
                this.observer = observer;
            }

            public void Dispose()
            {
                reactiveProperty.Unsubscribe(observer);
            }
        }
    }
}
