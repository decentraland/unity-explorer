using Global.AppArgs;

namespace Global.Dynamic.LaunchModes
{
    public interface IRealmLaunchSettings
    {
        void ApplyConfig(IAppArgs applicationParameters);
    }
}
