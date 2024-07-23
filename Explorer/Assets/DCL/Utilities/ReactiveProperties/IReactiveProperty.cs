namespace DCL.Utilities
{
    public interface IReactiveProperty<T>
    {
        new T Value { get; set; }
    }
}
