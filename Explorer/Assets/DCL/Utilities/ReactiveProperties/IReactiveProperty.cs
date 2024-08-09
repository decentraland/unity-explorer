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
}
