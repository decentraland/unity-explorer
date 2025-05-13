namespace ECS.LifeCycle.Components
{
    /// <summary>
    ///     Signals that the Entity will be destroyed by the end of the loop.
    ///     Systems that should execute logic on alive entities should filter this component out.
    /// </summary>
    public struct DeleteEntityIntention
    {
        public bool DeferDeletion;
    }
}
