namespace DCL.Time.Components
{
    /// <summary>
    ///     Component can be read from the global world (where it is updated) but it's discouraged to reuse it
    ///     in the scene worlds. For that purpose <see cref="PhysicsTickProvider" /> exists.
    /// </summary>
    public struct PhysicsTickComponent
    {
        public int Tick;
    }
}
