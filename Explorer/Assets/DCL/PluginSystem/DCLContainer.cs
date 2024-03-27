using Cysharp.Threading.Tasks;
using System.Threading;

namespace DCL.PluginSystem
{
    public abstract class DCLContainer<TSettings> : IDCLPlugin<TSettings> where TSettings: IDCLPluginSettings, new()
    {
        protected TSettings settings { get; private set; } = default!;

        public virtual void Dispose()
        {
        }

        public UniTask InitializeAsync(TSettings settings, CancellationToken ct)
        {
            this.settings = settings;
            return InitializeInternalAsync(settings, ct);
        }

        protected virtual UniTask InitializeInternalAsync(TSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
