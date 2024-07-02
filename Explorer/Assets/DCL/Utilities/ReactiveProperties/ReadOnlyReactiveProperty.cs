namespace DCL.Utilities
{
    public class ReadOnlyReactiveProperty<T> : IReadOnlyReactiveProperty<T>
    {
        private readonly ReactiveProperty<T> reactiveProperty;

        public T Value => reactiveProperty.Value;

        public ReadOnlyReactiveProperty(ReactiveProperty<T> reactiveProperty)
        {
            this.reactiveProperty = reactiveProperty;
        }

        public static implicit operator T(ReadOnlyReactiveProperty<T> value) =>
            value.Value;

        public override string ToString() =>
            reactiveProperty.Value!.ToString();
    }
}
