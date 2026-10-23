using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.Lobby;
using DCL.Settings.Settings;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.AddressableAssets;
using Utility;

namespace DCL.PluginSystem.Global
{
    /// <summary>
    ///     Keeps the lobby stage on the look chosen in Settings; looks are loaded the first time they are picked and stay resident until disposal.
    /// </summary>
    public class LobbyStagePresetSwitcher : IDisposable
    {
        private readonly LobbyStage stage;
        private readonly IReadOnlyList<LobbyPlugin.LobbyPluginSettings.StagePresetEntry> entries;
        private readonly LobbyStagePresetSetting setting;
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly ProvidedAsset<LobbyStagePreset>?[] loaded;

        private CancellationTokenSource? applyCancellation;

        public LobbyStagePresetSwitcher(LobbyStage stage, IReadOnlyList<LobbyPlugin.LobbyPluginSettings.StagePresetEntry> entries, LobbyStagePresetSetting setting, IAssetsProvisioner assetsProvisioner)
        {
            this.stage = stage;
            this.entries = entries;
            this.setting = setting;
            this.assetsProvisioner = assetsProvisioner;
            loaded = new ProvidedAsset<LobbyStagePreset>?[entries.Count];
        }

        public void Dispose()
        {
            setting.SelectedChanged -= Apply;
            applyCancellation.SafeCancelAndDispose();

            // A load cancelled before it reached its slot still holds the reference's handle
            for (var i = 0; i < loaded.Length; i++)
                if (loaded[i] is { } provided)
                    provided.Dispose();
                else if (entries[i].Preset.OperationHandle.IsValid())
                    entries[i].Preset.ReleaseAsset();
        }

        public void Start()
        {
            if (setting.SelectedIndex >= 0)
                Apply(setting.SelectedIndex);

            setting.SelectedChanged += Apply;
        }

        // A newer pick cancels the previous load so a late one cannot apply over it
        private void Apply(int index)
        {
            applyCancellation = applyCancellation.SafeRestart();
            ApplyAsync(index, applyCancellation.Token).Forget();
        }

        private async UniTaskVoid ApplyAsync(int index, CancellationToken ct)
        {
            try
            {
                ProvidedAsset<LobbyStagePreset>? preset = loaded[index];

                if (preset == null)
                {
                    AssetReferenceT<LobbyStagePreset> reference = entries[index].Preset;

                    // A load cancelled by an earlier pick is still running on the reference and would be handed back unfinished
                    if (reference.OperationHandle.IsValid())
                        await reference.OperationHandle.WithCancellation(ct);

                    preset = await assetsProvisioner.ProvideMainAssetAsync(reference, ct);
                    loaded[index] = preset;
                }

                stage.ApplyPreset(preset.Value.Value);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(new Exception($"Lobby stage preset \"{entries[index].Name}\" failed to load", e), ReportCategory.UI); }
        }
    }
}
