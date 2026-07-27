using DCL.RealmNavigation;
using Newtonsoft.Json.Linq;

namespace DCL.LoadingTimes
{
    public static class LoadingTimeSampler
    {
        private const string STAGE_PREFIX = "loading_stage_";
        private const string START_LABEL = "start_time_s";
        private const string STOP_LABEL = "stop_time_s";
        private const string DURATION_LABEL = "duration_s";

        private static readonly StageMeasure[] STAGE_MEASURES = new StageMeasure[(byte)LoadingStatus.LoadingStage.Completed]; //Auth-screen disabled
        private static float startTime;
        private static float stopTime;
        private static int current = 1; //Init happens before our subscription, we consider it the starting point

        public static void Sample(LoadingStatus.LoadingStage stage)
        {
            float time = UnityEngine.Time.realtimeSinceStartup;

            if (current > 0)
                STAGE_MEASURES[current - 1].StopTime = time;

            if (current < STAGE_MEASURES.Length)
                STAGE_MEASURES[current] = new StageMeasure(stage, time);

            if (stage == LoadingStatus.LoadingStage.Completed)
            {
                stopTime = time;
                STAGE_MEASURES[current].StopTime = time;
            }

            current++;
        }

        public static JObject ToJObject()
        {
            JObject jObject = new JObject
            {
                { START_LABEL, startTime },
                { STOP_LABEL, stopTime },
                { DURATION_LABEL, stopTime - startTime }
            };

            foreach (var measure in STAGE_MEASURES)
                jObject.Add($"{STAGE_PREFIX}{measure.Stage}", new JObject
                {
                    { START_LABEL, measure.StartTime },
                    { STOP_LABEL, measure.StopTime },
                    { DURATION_LABEL, measure.StopTime - measure.StartTime }
                });

            return jObject;
        }
    }
}
