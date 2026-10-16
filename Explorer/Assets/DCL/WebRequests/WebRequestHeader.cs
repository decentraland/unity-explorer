using DCL.Diagnostics;
using System;

namespace DCL.WebRequests
{
    public readonly struct WebRequestHeader
    {
        /// <summary>Carries the deep link sign-in secret; its value is never printed.</summary>
        public const string IDENTITY_VERIFIER = "x-identity-verifier";

        public readonly string Name;
        public readonly string Value;

        public WebRequestHeader(string name, string value)
        {
            Name = ValueOrEmpty(name);
            Value = ValueOrEmpty(value);
        }

        public override string ToString() =>
            string.Equals(Name, IDENTITY_VERIFIER, StringComparison.OrdinalIgnoreCase)
                ? $"WebRequestHeader({Name}: <redacted>)"
                : $"WebRequestHeader({Name}: {Value})";

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
