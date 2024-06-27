using Cysharp.Threading.Tasks;

namespace SceneRuntime.Apis.Modules.PortableExperiencesApi
{
    public interface IPortableExperiencesApi
    {
        UniTask<object> SpawnAsync(string pid, string ens);

        bool KillAsync(string pid);

        bool ExitAsync(string predefinedEmote);

        bool GetPortableExperiencesLoadedAsync();
    }
}
