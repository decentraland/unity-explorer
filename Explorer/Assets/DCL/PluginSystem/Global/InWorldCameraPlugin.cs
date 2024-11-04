using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Systems;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class InWorldCameraPlugin : IDCLGlobalPlugin
    {
        private readonly DCLInput input;

        public InWorldCameraPlugin(DCLInput input)
        {
            this.input = input;
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            InWorldCameraInputSystem.InjectToWorld(ref builder, input.InWorldCamera);
        }
    }
}
