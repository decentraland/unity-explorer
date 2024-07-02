using System;

namespace DCL.Utilities
{
    [Serializable]
    public class ReactiveProperty<T> : IReactiveProperty<T>, IDisposable
    {
        private T latestValue;
        private Action<T> valueChanged;

        public T Value
        {
            get => latestValue;

            set
            {
                latestValue = value;
                valueChanged?.Invoke(value);
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

        public void Subscribe(Action<T> observer) =>
            valueChanged += observer;

        public void Unsubscribe(Action<T> observer) =>
            valueChanged -= observer;

        public static implicit operator T(ReactiveProperty<T> value) =>
            value.Value;

        public override string ToString() =>
            latestValue!.ToString();
    }
}
