using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.HealthChecks;
using System;
using System.Diagnostics;
using System.Threading;
using Utility.Types;

namespace DCL.UserInAppInitializationFlow.StartupOperations
{
    public class EnsureLivekitConnectionStartupOperation
    {
        private readonly IHealthCheck healthCheck;

        public EnsureLivekitConnectionStartupOperation(IHealthCheck healthCheck)
        {
            this.healthCheck = healthCheck;
        }

        public void LaunchLivekitConnection(World world, Entity playerEntity, RealUserInAppInitializationFlow realUserInAppInitializationFlow, CancellationToken ct)
        {
            LaunchConnection(world, playerEntity, realUserInAppInitializationFlow, ct).Forget();
        }

        private async UniTask LaunchConnection(World world, Entity playerEntity, RealUserInAppInitializationFlow realUserInAppInitializationFlow, CancellationToken ct)
        {
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                UnityEngine.Debug.Log("JUANI LIVEKIT STARTED CONNECTING");
                await healthCheck.IsRemoteAvailableAsync(ct).Timeout(TimeSpan.FromSeconds(60));
                stopwatch.Stop();
                UnityEngine.Debug.Log($"JUANI LIVEKIT CONNECTED {stopwatch.ElapsedMilliseconds} ms");
            }
            catch (OperationCanceledException e)
            {

            }
            catch (Exception e)
            {
                DispatchFallbackToMainScreen(world, playerEntity, realUserInAppInitializationFlow);
            }

        }

        private void DispatchFallbackToMainScreen(World world, Entity playerEntity, RealUserInAppInitializationFlow realUserInAppInitializationFlow)
        {
            ReportHub.LogError(ReportCategory.LIVEKIT, "Livekit initialization failed. Fallback to main screen");
            var parameters = new UserInAppInitializationFlowParameters(
                true,
                true,
                IUserInAppInitializationFlow.LoadSource.Recover,
                world,
                playerEntity,
                EnumResult<TaskError>.ErrorResult(TaskError.Timeout, "Livekit connection Error")
            );
            realUserInAppInitializationFlow.ExecuteAsync(parameters, (new CancellationTokenSource()).Token).Forget();
        }
    }
}
