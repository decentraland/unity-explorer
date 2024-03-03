using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.Time
{
    public interface IWorldTimeProvider : IDisposable
    {
        public UniTask<float> GetWorldTimeAsync(CancellationToken cancellationToken);

        public void SetPausedState(bool isPaused);
    }

    public class WorldTimeProvider : IWorldTimeProvider
    {
        private const string TIME_SERVER_URL = "https://peer.decentraland.org/about";
        private const int TIME_BETWEEN_UPDATES = 5000;
        private const float GAME_HOURS_PER_CYCLE = 24;
        private const float REAL_MINUTES_PER_CYCLE = 120;

        private DateTime cachedServerTime;
        private DateTime cachedSystemTime;
        private float cachedTimeInSeconds;
        private bool isPaused;

        public void SetPausedState(bool isPaused)
        {
            this.isPaused = isPaused;
        }

        public async UniTask<float> GetWorldTimeAsync(CancellationToken ct)
        {
            if (isPaused) return cachedTimeInSeconds;

            DateTime currentSystemTime = DateTime.Now;
            TimeSpan timeDifference = currentSystemTime - cachedServerTime;
            DateTime currentTime;

            if (timeDifference.TotalMilliseconds > TIME_BETWEEN_UPDATES)
            {
                var intent = new SubIntention(new CommonLoadingArguments(TIME_SERVER_URL));
                string serverDate = (await intent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, GetTimeFromServer, ReportCategory.ENGINE, ct)).UnwrapAndRethrow();
                cachedServerTime = ObtainDateTimeFromServerTime(serverDate);
                currentTime = cachedServerTime;
                cachedSystemTime = DateTime.Now;
            }
            else
            {
                TimeSpan systemOffset = DateTime.Now - cachedSystemTime;
                currentTime = cachedServerTime.Add(systemOffset);
            }

            float cycleHours = CalculateCycleTime(currentTime);

            return cachedTimeInSeconds = CalculateCycleTimeInSeconds(cycleHours);;
        }

    private float CalculateCycleTimeInSeconds(float cycleTime) => cycleTime * 3600;

    private float CalculateCycleTime(DateTime serverTime)
        {
            //This allows us to have a settable cycle time, for example, by default we have 24 game-hours cycles that take 2 real hours.
            double totalMinutes = serverTime.TimeOfDay.TotalMinutes;
            double timeInCycle = totalMinutes / REAL_MINUTES_PER_CYCLE;
            float cyclePercentage = (float)timeInCycle - (int)timeInCycle;
            return cyclePercentage * GAME_HOURS_PER_CYCLE;
        }

        private async UniTask<StreamableLoadingResult<string>> GetTimeFromServer(SubIntention intention, IAcquiredBudget budget, IPartitionComponent partition, CancellationToken ct)
        {
            using UnityWebRequest wr = await UnityWebRequest.Get(TIME_SERVER_URL).SendWebRequest().WithCancellation(ct);
            return new StreamableLoadingResult<string>(wr.GetResponseHeader("date"));
        }

        private DateTime ObtainDateTimeFromServerTime(string serverDate)
        {
            if (DateTime.TryParse(serverDate, out DateTime serverTime))
            {
                return serverTime.ToUniversalTime();
            }
            return default(DateTime);
        }

        public void Dispose()
        {


        }
    }
}
