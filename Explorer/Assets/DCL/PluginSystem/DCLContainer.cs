using Cysharp.Threading.Tasks;
using System;
using System.Threading;

namespace DCL.PluginSystem
{
    public abstract class DCLContainer<TSettings> : IDCLPlugin<TSettings> where TSettings: IDCLPluginSettings, new()
    {
        protected TSettings settings { get; private set; } = default!;

        public virtual void Dispose() { }

        public async UniTask InitializeAsync(TSettings settings, CancellationToken ct)
        {
            this.settings = settings;

            try
            {
                await settings.EnsureValidAsync();
                await InitializeInternalAsync(settings, ct);
            }
            catch (Exception e) { throw new Exception($"Cannot initialize container {typeof(TSettings).FullName}", e); }
        }

        protected virtual UniTask InitializeInternalAsync(TSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;
    }
}
