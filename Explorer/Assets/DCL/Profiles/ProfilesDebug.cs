using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System;
using System.Collections.Generic;

namespace DCL.Profiles
{
    public class ProfilesDebug
    {
        private readonly ElementBinding<int>? nonAggregatedCounter;
        private readonly ElementBinding<int>? aggregatedCounter;
        private readonly ElementBinding<int>? missingProfilesCounter;

        private readonly HashSet<string>? missingProfiles;

        private ProfilesDebug(ElementBinding<int> nonAggregatedCounter, ElementBinding<int> aggregatedCounter, ElementBinding<int> missingProfilesCounter)
        {
            this.nonAggregatedCounter = nonAggregatedCounter;
            this.aggregatedCounter = aggregatedCounter;
            this.missingProfilesCounter = missingProfilesCounter;

            missingProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        private ProfilesDebug() { }

        public void AddNonAggregated(int count = 1)
        {
            if (nonAggregatedCounter != null)
                nonAggregatedCounter.Value++;
        }

        public void AddAggregated(int count = 1)
        {
            if (aggregatedCounter != null)
                aggregatedCounter.Value++;
        }

        public void AddMissing(string id)
        {
            if (missingProfiles == null) return;

            if (missingProfiles.Add(id))
                missingProfilesCounter!.Value++;
        }

        public static ProfilesDebug Create(IDebugContainerBuilder builder)
        {
            DebugWidgetBuilder? widget = builder.TryAddWidget(IDebugContainerBuilder.Categories.PROFILES);

            if (widget == null)
                return new ProfilesDebug();

            var nonAggregated = new ElementBinding<int>(0);
            var aggregated = new ElementBinding<int>(0);
            var missing = new ElementBinding<int>(0);

            widget.AddControlWithLabel("Non-aggregated", new DebugIntFieldDef(nonAggregated))
                  .AddControlWithLabel("Aggregated", new DebugIntFieldDef(aggregated))
                  .AddControlWithLabel("Missing", new DebugIntFieldDef(missing));

            return new ProfilesDebug(nonAggregated, aggregated, missing);
        }
    }
}
