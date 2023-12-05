namespace DCL.Time
{
    /// <summary>
    ///     Provides a way to access physics tick (that is updated from the global world) in scene worlds
    /// </summary>
    public interface IPhysicsTickProvider
    {
        int Tick { get; }
    }

    public class PhysicsTickProvider : IPhysicsTickProvider
    {
        public int Tick { get; set; }
    }
}
