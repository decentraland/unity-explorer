using CodeLess.Attributes;
using DCL.Diagnostics;
using UnityEngine.CrashReportHandler;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    [Singleton(SingletonGenerationBehavior.ALLOW_IMPLICIT_CONSTRUCTION)]
    public partial class UnityDiagnosticsCenter
    {
        public void SetWallet(string wallet)
        {
            CrashReportHandler.SetUserMetadata("wallet", wallet);
        }

        public void SetMeetsMinimumRequirements(bool meets)
        {
            CrashReportHandler.SetUserMetadata("meets_minimum_requirements", meets.ToString());
        }

        public void SetCurrentScene(SceneShortInfo sceneInfo)
        {
            CrashReportHandler.SetUserMetadata("current_scene.base_parcel", sceneInfo.BaseParcel.ToString());
            CrashReportHandler.SetUserMetadata("current_scene.name", sceneInfo.Name);

            if (!string.IsNullOrEmpty(sceneInfo.SdkVersion))
                CrashReportHandler.SetUserMetadata("current_scene.sdk_version", sceneInfo.SdkVersion);
        }

        public void SetRealmInfo(string baseCatalystUrl, string baseContentUrl, string baseLambdaUrl)
        {
            CrashReportHandler.SetUserMetadata("base_catalyst_url", baseCatalystUrl);
            CrashReportHandler.SetUserMetadata("base_content_url", baseContentUrl);
            CrashReportHandler.SetUserMetadata("base_lambda_url", baseLambdaUrl);
        }
    }
}
