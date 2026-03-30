using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Optimization.ThreadSafePool;
using Sentry;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.WebRequests.Analytics
{
    public class SentryWebRequestSampler : SentrySampler.ISampler
    {
        internal const string SAMPLE_KEY = nameof(SentryWebRequestSampler);
        private const string PLACEHOLDER_MARKER = "{{";

        private static readonly Regex PLACEHOLDER_PATTERN = new (@"\{\{\d+\}\}", RegexOptions.Compiled);

        private readonly ThreadSafeObjectPool<Dictionary<string, object>> samplingContextPool;

        private readonly Dictionary<DecentralandUrl, TemplateData> templateCache = new ();

        private readonly IReadOnlyList<SentryTransactionConfiguration> urlsToSample;
        private readonly IDecentralandUrlsSource urlsSource;

        public SentryWebRequestSampler(IDecentralandUrlsSource urlsSource, IReadOnlyList<SentryTransactionConfiguration> urlsToSample, int maxConcurrency)
        {
            samplingContextPool = new ThreadSafeObjectPool<Dictionary<string, object>>(
                () => new Dictionary<string, object>(1) { [SAMPLE_KEY] = new SamplingContext() },
                actionOnRelease: d => ((SamplingContext)d[SAMPLE_KEY]).TransactionName = null,
                defaultCapacity: maxConcurrency,
                maxSize: maxConcurrency * 2,
                collectionCheck: PoolConstants.CHECK_COLLECTIONS
            );

            this.urlsToSample = urlsToSample;
            this.urlsSource = urlsSource;
        }

        public (PooledObject<Dictionary<string, object>> sentryContextObj, SamplingContext samplingContext) PoolContext(out Dictionary<string, object> raw)
        {
            PooledObject<Dictionary<string, object>> pooled = samplingContextPool.Get(out raw);
            return (pooled, (SamplingContext)raw[SAMPLE_KEY]);
        }

        public double? Execute(TransactionSamplingContext transactionSamplingContext)
        {
            double? result = null;

            if (transactionSamplingContext.TransactionContext.Operation == OpenTelemetrySemantics.OperationHttpClient &&
                transactionSamplingContext.CustomSamplingContext.TryGetValue(SAMPLE_KEY, out object? ctx) && ctx is SamplingContext samplingContext)
            {
                // Disallow urls that are not configured to sample
                result = 0;

                foreach (SentryTransactionConfiguration configuration in urlsToSample)
                {
                    // Url is written to Name
                    if (UrlMatchesTemplate(transactionSamplingContext.TransactionContext.Name, configuration.url, out string transactionName))
                    {
                        samplingContext.TransactionName = transactionName;
                        result = configuration.samplingRate;
                        break;
                    }
                }
            }

            return result;
        }

        /// <summary>
        ///     Checks if a URL matches a template URL pattern and returns the cached template data.
        ///     Uses simple StartsWith for templates without placeholders,
        ///     regex only when placeholders like {{0}} are present.
        /// </summary>
        private bool UrlMatchesTemplate(string url, DecentralandUrl templateType, out string transactionName)
        {
            string templateUrl = urlsSource.Url(templateType);

            transactionName = string.Empty;

            if (string.IsNullOrEmpty(templateUrl) || !SchemeIsValid(templateUrl, out _)) return false;

            if (!templateCache.TryGetValue(templateType, out TemplateData data))
            {
                data = BuildTemplateData(templateUrl);
                templateCache[templateType] = data;
            }

            transactionName = data.TransactionName;
            return data.MatchPredicate(templateUrl, url);
        }

        private static TemplateData BuildTemplateData(string templateUrl)
        {
            string transactionName = ExtractTargetFromTemplate(templateUrl);

            Func<string, string, bool> predicate;

            if (!templateUrl.Contains(PLACEHOLDER_MARKER))
                predicate = static (template, u) => u.StartsWith(template, StringComparison.Ordinal);
            else
            {
                string escaped = Regex.Escape(templateUrl);
                string pattern = PLACEHOLDER_PATTERN.Replace(escaped, "[^/]*");
                var regex = new Regex("^" + pattern, RegexOptions.Compiled);
                predicate = (_, u) => regex.IsMatch(u);

                // Convert {{0}} to {0} in transaction name
                transactionName = PLACEHOLDER_PATTERN.Replace(transactionName, static m => "{" + m.Value[2..^2] + "}");
            }

            return new TemplateData(predicate, transactionName);
        }

        private static string ExtractTargetFromTemplate(string templateUrl)
        {
            if (!SchemeIsValid(templateUrl, out int schemeEnd))
                return templateUrl;

            int hostStart = schemeEnd + 3;
            int pathStart = templateUrl.IndexOf('/', hostStart);

            return pathStart < 0 ? "/" : templateUrl[pathStart..];
        }

        private static bool SchemeIsValid(string url, out int schemeEnd)
        {
            schemeEnd = url.IndexOf("://", StringComparison.Ordinal);
            return schemeEnd >= 0;
        }

        /// <summary>
        ///     Parses a URL into OpenTelemetry parts using the pre-computed transaction name.
        /// </summary>
        /// <param name="url">The actual URL to parse</param>
        /// <param name="transactionName">The pre-computed parameterized transaction name from UrlMatchesTemplate</param>
        /// <param name="method">Http Method</param>
        /// <param name="parts">The parsed URL parts</param>
        internal static bool TryParseUrlParts(string url, string transactionName, string method, out OpenTelemetryUrlParts parts)
        {
            if (string.IsNullOrEmpty(url))
            {
                parts = default(OpenTelemetryUrlParts);
                return false;
            }

            if (!SchemeIsValid(url, out int schemeEnd))
            {
                parts = default(OpenTelemetryUrlParts);
                return false;
            }

            string scheme = url[..schemeEnd];
            int hostStart = schemeEnd + 3;

            int pathStart = url.IndexOf('/', hostStart);
            string host;
            string target;

            if (pathStart < 0)
            {
                host = url[hostStart..];
                target = "/";
            }
            else
            {
                host = url.Substring(hostStart, pathStart - hostStart);
                target = url[pathStart..];
            }

            parts = new OpenTelemetryUrlParts(scheme, host, target, url, $"{method} {transactionName}");
            return true;
        }

        private readonly struct TemplateData
        {
            public readonly Func<string, string, bool> MatchPredicate;
            public readonly string TransactionName;

            public TemplateData(Func<string, string, bool> matchPredicate, string transactionName)
            {
                MatchPredicate = matchPredicate;
                TransactionName = transactionName;
            }
        }

        [Serializable]
        public struct SentryTransactionConfiguration
        {
            public DecentralandUrl url;

            [Range(0, 1f)]
            public float samplingRate;
        }

        /// <summary>
        ///     Pooled and reused
        /// </summary>
        public class SamplingContext
        {
            /// <summary>
            ///     It's assigned if the template has a match
            /// </summary>
            public string? TransactionName;
        }

        /// <summary>
        ///     URL parts according to OpenTelemetry HTTP semantic conventions
        /// </summary>
        public readonly struct OpenTelemetryUrlParts
        {
            /// <summary>http.scheme - The URI scheme (e.g., "https")</summary>
            public readonly string Scheme;

            /// <summary>http.host - The host and port (e.g., "example.com:8080")</summary>
            public readonly string Host;

            /// <summary>http.target - The path and query string (e.g., "/api/users?id=123")</summary>
            public readonly string Target;

            /// <summary>http.url - The full URL</summary>
            public readonly string Url;

            /// <summary>Parameterized route for transaction name (e.g., "/api/users/{0}")</summary>
            public readonly string TransactionName;

            public OpenTelemetryUrlParts(string scheme, string host, string target, string url, string transactionName)
            {
                Scheme = scheme;
                Host = host;
                Target = target;
                Url = url;
                TransactionName = transactionName;
            }
        }
    }
}
