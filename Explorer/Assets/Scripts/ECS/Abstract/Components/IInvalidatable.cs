namespace ECS.Abstract.Components
{
    /// <summary>
    ///     A derivative from a SDK component that can be invalidated due to the data change
    /// </summary>
    public interface IInvalidatableComponent<TKey>
    {
        bool IsInvalidated { get; set; }
    }
}
