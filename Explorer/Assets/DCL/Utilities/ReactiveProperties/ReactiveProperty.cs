﻿using System;

namespace DCL.Utilities
{
    [Serializable]
    public class ReactiveProperty<T> : IReactiveProperty<T>, IDisposable
    {
        private T latestValue;
        private Action<T>? valueChanged;

        public event Action<T>? OnUpdate;

        public T Value
        {
            get => latestValue;

            set
            {
                if (!Equals(latestValue, value))
                {
                    latestValue = value;
                    valueChanged?.Invoke(latestValue);
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
            valueChanged = null;
        }

        public IDisposable Subscribe(Action<T> observer)
        {
            valueChanged += observer;
            return new DisposableSubscription(this, observer);
        }

        public void Unsubscribe(Action<T> observer)
        {
            valueChanged -= observer;
        }

        public static implicit operator T(ReactiveProperty<T> value) =>
            value.Value;

        public void UpdateValue(T value)
        {
            Value = value;
        }

        public override string ToString() =>
            latestValue!.ToString();

        private class DisposableSubscription : IDisposable
        {
            private readonly ReactiveProperty<T> reactiveProperty;
            private readonly Action<T> observer;

            public DisposableSubscription(ReactiveProperty<T> reactiveProperty, Action<T> observer)
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
