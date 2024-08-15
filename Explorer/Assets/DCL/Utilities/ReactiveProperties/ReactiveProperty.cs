using System;

namespace DCL.Utilities
{
    [Serializable]
    public class ReactiveProperty<T> : IReactiveProperty<T>, IDisposable
    {
        private T latestValue;

        public event Action<T>? OnUpdate;

        public T Value
        {
            get => latestValue;

            set
            {
                if (!Equals(latestValue, value))
                {
                    latestValue = value;
                    OnUpdate?.Invoke(latestValue);
                }
            }
        }

        public ReactiveProperty(T initialValue)
        {
            latestValue = initialValue;
        }

        public void Dispose()
        {
            OnUpdate = null;
        }

        public static implicit operator T(ReactiveProperty<T> value) =>
            value.Value;

        public void UpdateValue(T value)
        {
            Value = value;
        }

        public override string ToString() =>
            latestValue!.ToString();
    }
}
