using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class EntitiesAnalyticsDebug
    {
        private readonly DebugWidgetBuilder? widget;

        private readonly Dictionary<string, BatchesCounter> counters = new ();

        public EntitiesAnalyticsDebug(DebugWidgetBuilder? widget)
        {
            this.widget = widget;
        }

        public BatchesCounter? GetOrDefault(string categoryName) =>
            counters.GetValueOrDefault(categoryName);

        public EntitiesAnalyticsDebug Add(string categoryName)
        {
            if (widget != null)
            {
                var counter = new BatchesCounter();

                widget.AddControlWithLabel($"{categoryName}: Batch.{nameof(BatchesCounter.Min)}", new DebugIntFieldDef(counter.Min));
                widget.AddControlWithLabel($"{categoryName}: Batch.{nameof(BatchesCounter.Avg)}", new DebugIntFieldDef(counter.Avg));
                widget.AddControlWithLabel($"{categoryName}: Batch.{nameof(BatchesCounter.Max)}", new DebugIntFieldDef(counter.Max));

                counters.Add(categoryName, counter);
            }

            return this;
        }

        public class BatchesCounter
        {
            public readonly ElementBinding<int> Min = new (0);
            public readonly ElementBinding<int> Avg = new (0);
            public readonly ElementBinding<int> Max = new (0);

            private int samplesCount;
            private int batchesSum;

            public void AddSample(int batchSize)
            {
                samplesCount++;
                batchSize += batchSize;

                Min.Value = Mathf.Min(Min.Value, batchSize);
                Max.Value = Mathf.Max(Max.Value, batchSize);
                Avg.Value = batchesSum / samplesCount;
            }
        }
    }
}
