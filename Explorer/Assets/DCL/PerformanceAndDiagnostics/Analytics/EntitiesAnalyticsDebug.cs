using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class EntitiesAnalyticsDebug
    {
        public readonly DebugWidgetBuilder? Widget;

        private readonly Dictionary<string, BatchesCounter> counters = new ();

        public EntitiesAnalyticsDebug(DebugWidgetBuilder? widget)
        {
            Widget = widget;
        }

        public BatchesCounter? GetOrDefault(string categoryName) =>
            counters.GetValueOrDefault(categoryName);

        public EntitiesAnalyticsDebug Add(string categoryName)
        {
            if (Widget != null)
            {
                var counter = new BatchesCounter();

                Widget.AddControlWithLabel($"{categoryName}: Batch.{nameof(BatchesCounter.Min)}", new DebugIntFieldDef(counter.Min));
                Widget.AddControlWithLabel($"{categoryName}: Batch.{nameof(BatchesCounter.Avg)}", new DebugIntFieldDef(counter.Avg));
                Widget.AddControlWithLabel($"{categoryName}: Batch.{nameof(BatchesCounter.Max)}", new DebugIntFieldDef(counter.Max));

                counters.Add(categoryName, counter);
            }

            return this;
        }

        public class BatchesCounter
        {
            public readonly ElementBinding<int> Min = new (int.MaxValue);
            public readonly ElementBinding<int> Avg = new (0);
            public readonly ElementBinding<int> Max = new (0);

            private int samplesCount;
            private int batchesSum;

            public void AddSample(int batchSize)
            {
                samplesCount++;
                batchesSum += batchSize;

                Min.Value = Mathf.Min(Min.Value, batchSize);
                Max.Value = Mathf.Max(Max.Value, batchSize);
                Avg.Value = batchesSum / samplesCount;
            }
        }
    }
}
