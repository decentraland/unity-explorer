using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.PerformanceAndDiagnostics.Analytics;
using System;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public class ProfilesDebug
    {
        private const string NAME = "Profiles";

        private readonly ElementBinding<int>? nonCombinedCounter;
        private readonly ElementBinding<int>? totalPostRequests;
        private readonly ElementBinding<int>? aggregatedCounter;
        private readonly ElementBinding<int>? missingProfilesCounter;

        private readonly EntitiesAnalyticsDebug entitiesAnalyticsDebug;
        private readonly HashSet<string>? missingProfiles;

        private ProfilesDebug(
            EntitiesAnalyticsDebug entitiesAnalyticsDebug,
            ElementBinding<int> aggregatedCounter,
            ElementBinding<int> missingProfilesCounter,
            ElementBinding<int>? nonCombinedCounter,
            ElementBinding<int> totalPostRequests)
        {
            this.entitiesAnalyticsDebug = entitiesAnalyticsDebug;
            this.aggregatedCounter = aggregatedCounter;
            this.missingProfilesCounter = missingProfilesCounter;
            this.nonCombinedCounter = nonCombinedCounter;
            this.totalPostRequests = totalPostRequests;

            missingProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            entitiesAnalyticsDebug.Add(NAME);
        }

        private ProfilesDebug() { }

        /// <summary>
        ///     Requests are non-aggregated when a single id is served in a post request
        /// </summary>
        /// <param name="count"></param>
        public void AddNonAggregated(int count = 1)
        {
            if (nonCombinedCounter != null)
                nonCombinedCounter.Value += count;

            if (totalPostRequests != null)
                totalPostRequests.Value++;
        }

        public void AddAggregated()
        {
            if (aggregatedCounter != null)
                aggregatedCounter.Value += 1;

            if (totalPostRequests != null)
                totalPostRequests.Value++;
        }

        public void AddMissing(string id)
        {
            if (missingProfiles == null) return;

            if (missingProfiles.Add(id))
                missingProfilesCounter!.Value++;
        }

        public void AddBatchSample(int batchSize) =>
            entitiesAnalyticsDebug.GetOrDefault(NAME)?.AddSample(batchSize);

        public static ProfilesDebug Create(DebugWidgetBuilder? widget, EntitiesAnalyticsDebug entitiesAnalyticsDebug)
        {
            if (widget == null)
                return new ProfilesDebug();

            var nonAggregated = new ElementBinding<int>(0);
            var aggregated = new ElementBinding<int>(0);
            var totalPostRequests = new ElementBinding<int>(0);
            var missing = new ElementBinding<int>(0);
            var nonCombined = new ElementBinding<int>(0);

            widget.AddControlWithLabel($"{NAME}: Non-aggregated", new DebugIntFieldDef(nonAggregated))
                  .AddControlWithLabel($"{NAME}: Non-combined", new DebugIntFieldDef(nonCombined))
                  .AddControlWithLabel($"{NAME}: Post Requests", new DebugIntFieldDef(totalPostRequests))
                  .AddControlWithLabel($"{NAME}: Aggregated", new DebugIntFieldDef(aggregated))
                  .AddControlWithLabel($"{NAME}: Missing", new DebugIntFieldDef(missing));

            return new ProfilesDebug(entitiesAnalyticsDebug, aggregated, missing, nonCombined, totalPostRequests);
        }
    }
}
