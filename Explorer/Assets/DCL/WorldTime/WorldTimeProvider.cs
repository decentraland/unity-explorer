﻿using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Threading;
using UnityEngine.Networking;

namespace DCL.Time
{
    public class WorldTimeProvider : IWorldTimeProvider
    {
        private const int TIME_BETWEEN_UPDATES = 5000;
        private const float GAME_HOURS_PER_CYCLE = 24;
        private const float REAL_MINUTES_PER_CYCLE = 120;
        private const float STARTING_CYCLE_HOUR = 0.01f;

        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly IWebRequestController webRequestController;

        private DateTime cachedServerTime;
        private DateTime cachedSystemTime;
        private float cachedTimeInSeconds;
        private bool isPaused;

        private string TIME_SERVER_URL => decentralandUrlsSource.Url(DecentralandUrl.PeerAbout);

        public WorldTimeProvider(IDecentralandUrlsSource decentralandUrlsSource, IWebRequestController webRequestController)
        {
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.webRequestController = webRequestController;
        }

        public async UniTask<float> GetWorldTimeAsync(CancellationToken ct)
        {
            if (isPaused) return cachedTimeInSeconds;

            DateTime currentSystemTime = DateTime.UtcNow;
            TimeSpan timeDifference = currentSystemTime - cachedServerTime;
            DateTime currentTime;

            if (timeDifference.TotalMilliseconds > TIME_BETWEEN_UPDATES)
            {
                var intent = new SubIntention(new CommonLoadingArguments(TIME_SERVER_URL));
                string serverDate = (await intent.RepeatLoopAsync(NoAcquiredBudget.INSTANCE, PartitionComponent.TOP_PRIORITY, GetTimeFromServerAsync, ReportCategory.ENGINE, ct)).UnwrapAndRethrow();
                cachedServerTime = ObtainDateTimeFromServerTime(serverDate);
                currentTime = cachedServerTime;
                cachedSystemTime = DateTime.Now;
            }
            else
            {
                TimeSpan systemOffset = DateTime.Now - cachedSystemTime;
                currentTime = cachedServerTime.Add(systemOffset);
            }

            float cycleHour = CalculateCycleTime(currentTime);

            return cachedTimeInSeconds = CalculateCycleTimeInSeconds(cycleHour);;
        }

        private float CalculateCycleTimeInSeconds(float cycleHour) => cycleHour * 3600;

        private float CalculateCycleTime(DateTime serverTime)
        {
            double totalMinutes = serverTime.TimeOfDay.TotalMinutes;
            double cyclesPassed = totalMinutes / REAL_MINUTES_PER_CYCLE;
            float currentCyclePercentage = (float)cyclesPassed - (int)cyclesPassed;
            float cycleHour = currentCyclePercentage * GAME_HOURS_PER_CYCLE;
            return cycleHour > 0? cycleHour : STARTING_CYCLE_HOUR;
        }

        private async UniTask<StreamableLoadingResult<string>> GetTimeFromServerAsync(SubIntention intention, IAcquiredBudget budget, IPartitionComponent partition, CancellationToken ct)
        {
            string date = await webRequestController.GetAsync(TIME_SERVER_URL, ct, ReportCategory.JAVASCRIPT)
                                                    .GetResponseHeaderAsync("date");

            return new StreamableLoadingResult<string>(date);
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
