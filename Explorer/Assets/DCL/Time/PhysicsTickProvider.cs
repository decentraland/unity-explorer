using CodeLess.Attributes;

namespace DCL.Time
{
    /// <summary>
    ///     Provides a way to access physics tick (that is updated from the global world) in scene worlds
    /// </summary>
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION | SingletonGenerationBehavior.GENERATE_STATIC_ACCESSORS)]
    public partial class PhysicsTickProvider
    {
        internal int tick { get; set; }
    }
}
