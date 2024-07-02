namespace DCL.Utilities
{
    public interface IReadOnlyReactiveProperty<out T>
    {
        T Value { get; }
    }

    public interface IReactiveProperty<T> : IReadOnlyReactiveProperty<T>
    {
        new T Value { get; set; }
    }
}
