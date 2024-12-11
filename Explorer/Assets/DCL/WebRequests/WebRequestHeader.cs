using DCL.Diagnostics;

namespace DCL.WebRequests
{
    public readonly struct WebRequestHeader
    {
        public readonly string Name;
        public readonly string Value;

        public WebRequestHeader(string name, string value)
        {
            Name = ValueOrEmpty(name);
            Value = ValueOrEmpty(value);
        }

        public override string ToString() =>
            $"WebRequestHeader({Name}: {Value})";

        private static string ValueOrEmpty(string value)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (value == null)
            {
                ReportHub.LogError(ReportCategory.ECS, "Propagated null value in WebRequestHeader.");
                return string.Empty;
            }

            return value;
        }
    }
}
