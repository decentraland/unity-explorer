using SceneRunner.Scene;

namespace DCL.Gizmos
{
    public delegate SceneGizmosProviderBase CreateSceneGizmosDelegate();

    /// <summary>
    ///     Base class to draw debug visuals for scene entities
    /// </summary>
    public abstract class SceneGizmosProviderBase
    {
        private string defaultName;

        internal virtual string name => defaultName ??= GetType().Name;

        public ISceneData SceneData { get; internal set; }

        public virtual void OnInitialize() { }

        public virtual void OnDrawGizmosSelected() { }

        public virtual void OnDrawGizmos() { }
    }
}
