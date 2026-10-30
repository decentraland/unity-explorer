using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.Lobby;
using DCL.Settings.Settings;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Loads every configured lobby stage look at startup and keeps it resident, handing each backdrop to the setting.
    ///     It must stay the only loader of these references: a second load on an in-flight reference hands back an unfinished asset.
    /// </summary>
    public class LobbyStagePresetCatalog : IDisposable
    {
        private readonly IReadOnlyList<LobbyPlugin.LobbyPluginSettings.StagePresetEntry> entries;
        private readonly LobbyStagePresetSetting setting;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ProvidedAsset<LobbyStagePreset>?[] presets;
        private readonly CancellationTokenSource loadCancellation = new ();

        public event Action<int>? PresetLoaded;

        public LobbyStagePresetCatalog(IReadOnlyList<LobbyPlugin.LobbyPluginSettings.StagePresetEntry> entries, LobbyStagePresetSetting setting, IAssetsProvisioner assetsProvisioner)
        {
            this.entries = entries;
            this.setting = setting;
            this.assetsProvisioner = assetsProvisioner;
            presets = new ProvidedAsset<LobbyStagePreset>?[entries.Count];
        }

        public void Dispose()
        {
            loadCancellation.SafeCancelAndDispose();

            // A cancelled load keeps running on the reference with no provided asset to release it through
            for (var i = 0; i < entries.Count; i++)
                if (presets[i] is { } preset)
                    preset.Dispose();
                else if (entries[i].Preset.OperationHandle.IsValid())
                    entries[i].Preset.ReleaseAsset();
        }

        public void Start()
        {
            for (var i = 0; i < entries.Count; i++)
                LoadAsync(i, loadCancellation.Token).Forget();
        }

        public LobbyStagePreset? PresetAt(int index) =>
            presets[index]?.Value;

        private async UniTaskVoid LoadAsync(int index, CancellationToken ct)
        {
            try
            {
                ProvidedAsset<LobbyStagePreset> preset = await assetsProvisioner.ProvideMainAssetAsync(entries[index].Preset, ct);

                if (ct.IsCancellationRequested) return;

                presets[index] = preset;

                if (preset.Value.Backdrop != null)
                    setting.SetBackdrop(index, preset.Value.Backdrop);

                PresetLoaded?.Invoke(index);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(new Exception($"Lobby stage preset \"{entries[index].Name}\" failed to load", e), ReportCategory.UI); }
        }
    }
}
