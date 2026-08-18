using System;

namespace DCL.BugReporting
{
    /// <summary>One option of the "Issue Type" list attribute on the Intercom "Bug Report" ticket type.</summary>
    public readonly struct BugReportIssueType : IEquatable<BugReportIssueType>
    {
        public readonly string Label;
        public readonly string OptionId;

        public BugReportIssueType(string label, string optionId)
        {
            Label = label;
            OptionId = optionId;
        }

        public bool Equals(BugReportIssueType other) =>
            Label == other.Label && OptionId == other.OptionId;

        public override bool Equals(object? obj) =>
            obj is BugReportIssueType other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(Label, OptionId);
    }

    /// <summary>The options the "Bug Report" ticket type declares, read back from GET /ticket_types.</summary>
    public static class BugReportIssueTypes
    {
        public static readonly BugReportIssueType PERFORMANCE = new ("Performance (Lag/FPS)", "84d3e47f-396f-40be-bb93-a8b36196cf97");

        public static readonly BugReportIssueType[] ALL =
        {
            PERFORMANCE,
            new ("Crash / Freeze", "10ab00f9-e944-4a7f-8b75-c8bf4e4ff270"),
            new ("Chat", "b2db7b2e-3634-4c9d-9f55-b732bfe41319"),
            new ("Voice Chat", "4395e4a3-7eb8-4bd1-a82e-546250d5c16d"),
            new ("Streaming / Video Player", "dbceaef0-c69c-409b-afb3-5e9523a4dec5"),
            new ("Hangouts / Events", "591a11e5-b440-4acb-9461-7d89e9ae4303"),
            new ("Friends", "4d3a9289-da5a-47a6-a3e0-772effdd78f0"),
            new ("Outfits", "ee7aadd6-6b28-4605-b7b9-908a0788b92c"),
            new ("Wearables / Emotes", "e4e9abb6-8304-48eb-a622-b2516e0a1719"),
            new ("Profile", "30f4a7a7-6366-4e05-9393-d1419a5b4008"),
            new ("Communities", "cf4e335e-98fe-4638-a95a-cc6468de00c3"),
            new ("Map & Minimap", "0c02dc0f-ed5e-4c57-adf1-eac0c91866f6"),
            new ("Rewards", "6c896e12-f790-4971-bb06-a10967589a4c"),
            new ("Gifting", "c602cf8d-a617-4ebf-95be-722f3e4b12c9"),
            new ("Scene", "291a3bee-10f7-4fd3-8019-8d6d9c992f29"),
            new ("Other", "30b90385-7138-4d42-99aa-87eeb1c85619"),
        };
    }

    /// <summary>The options of the "Meets Minimum Requirements" list attribute the client can tell apart.</summary>
    public static class BugReportMinimumSpecOptions
    {
        public const string BELOW_MIN_SPEC = "bf4067b3-4d19-456b-b615-4d21f7695228";
        public const string MEETS_MIN_SPEC = "d1b00226-638c-40d8-9c0c-952b7c74621a";
    }
}
