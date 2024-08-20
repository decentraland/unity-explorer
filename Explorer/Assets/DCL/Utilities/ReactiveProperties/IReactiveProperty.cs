using System;

namespace DCL.Utilities
{
    public interface IReactiveProperty<T> : IReadonlyReactiveProperty<T>
    {
        void UpdateValue(T value);
    }

    public interface IReadonlyReactiveProperty<out T>
    {
        event Action<T> OnUpdate;

        T Value { get; }
    }

    public static class ReactivePropertyExtensions
    {
        public static IDisposable Subscribe<T>(this IReadonlyReactiveProperty<T> property, Action<T> observer)
        {
            property.OnUpdate += observer;
            return new DisposableSubscription<T>(property, observer);
        }

        public static void Unsubscribe<T>(this IReadonlyReactiveProperty<T> property, Action<T> observer)
        {
            property.OnUpdate -= observer;
        }

        private class DisposableSubscription<T> : IDisposable
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
