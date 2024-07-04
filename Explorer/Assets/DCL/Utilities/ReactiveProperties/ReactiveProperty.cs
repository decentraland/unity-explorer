using System;

namespace DCL.Utilities
{
    [Serializable]
    public class ReactiveProperty<T> : IReactiveProperty<T>, IDisposable
    {
        private T latestValue;
        private Action<T>? valueChanged;

        public T Value
        {
            get => latestValue;

            set
            {
                if (!Equals(latestValue, value))
                {
                    latestValue = value;
                    valueChanged?.Invoke(latestValue);
                }
            }
        }

        public ReactiveProperty(T initialValue)
        {
            latestValue = initialValue;
        }

        public void Dispose()
        {
            valueChanged = null;
        }

        public IDisposable Subscribe(Action<T> observer)
        {
            valueChanged += observer;
            return new Subscription(this, observer);
        }

        public void Unsubscribe(Action<T> observer)
        {
            valueChanged -= observer;
        }

        public static implicit operator T(ReactiveProperty<T> value) =>
            value.Value;

        public override string ToString() =>
            latestValue!.ToString();

        private class Subscription : IDisposable
        {
            private readonly ReactiveProperty<T> reactiveProperty;
            private readonly Action<T> observer;

            public Subscription(ReactiveProperty<T> reactiveProperty, Action<T> observer)
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
