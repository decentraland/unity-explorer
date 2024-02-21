using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     When the world is injected, this delegate will be called to continue the initialization
    /// </summary>
    public delegate void ContinueInitialization(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments);

    public abstract class DCLGlobalPluginBase<TSettings> : IDCLGlobalPlugin<TSettings> where TSettings: IDCLPluginSettings, new()
    {
        private ContinueInitialization? initializationContinuation;
        protected TSettings settings { get; private set; } = default!;

        public virtual void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            initializationContinuation?.Invoke(ref builder, arguments);
            InjectSystems(ref builder, in arguments);
        }

        public async UniTask InitializeAsync(TSettings settings, CancellationToken ct)
        {
            this.settings = settings;
            initializationContinuation = await InitializeInternalAsync(settings, ct);
        }

        /// <summary>
        ///     Override this function to inject systems into the world, don't use it for dependencies initialization
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="arguments"></param>
        protected abstract void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments);

        /// <summary>
        ///     Override this method if it's possible to initialize dependencies without World and entities
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="ct"></param>
        /// <returns>Action to continue initialization when the world is set</returns>
        protected abstract UniTask<ContinueInitialization?> InitializeInternalAsync(TSettings settings, CancellationToken ct);
    }
}
