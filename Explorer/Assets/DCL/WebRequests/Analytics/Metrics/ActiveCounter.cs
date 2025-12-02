using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using System;

namespace DCL.WebRequests.Analytics.Metrics
{
    public class ActiveCounter : RequestMetricBase
    {
        private ElementBinding<DebugOngoingWebRequestDef.DataSource>? dataSourceBinding;

        private ulong counter { get; set; }

        public override DebugLongMarkerDef.Unit GetUnit() =>
            DebugLongMarkerDef.Unit.NoFormat;

        public override void CreateDebugMenu(DebugWidgetBuilder? builder, IWebRequestsAnalyticsContainer.RequestType requestType)
        {
            base.CreateDebugMenu(builder, requestType);

            if (builder == null) return;

            dataSourceBinding = new ElementBinding<DebugOngoingWebRequestDef.DataSource>(new DebugOngoingWebRequestDef.DataSource());
            builder.AddControl(new DebugOngoingWebRequestDef(dataSourceBinding), null);
        }

        public override void UpdateDebugMenu()
        {
            base.UpdateDebugMenu();

            dataSourceBinding?.Value.UpdateTime(DateTime.Now);
        }

        public override ulong GetMetric() =>
            counter;

        public override void OnRequestStarted(ITypedWebRequest request, DateTime startTime)
        {
            counter++;

            // Create a short version of URL to prevent a mess
            // Skip up to the path fragment (if it is present) for decentraland URLs

            string? fullURL = request.UnityWebRequest.url;
            ReadOnlySpan<char> urlSpan = fullURL.AsSpan();

            int contentStart = 0;
            int contentLength = fullURL.Length;

            if (urlSpan.Contains("decentraland".AsSpan(), StringComparison.Ordinal))
            {
                // Find the third '/' which marks the start of the path
                // URL format: https://domain.com/path
                // Slashes:    ^     ^          ^
                //             1st   2nd        3rd (path start)

                int slashCount = 0;
                int pathStartIndex = -1;

                for (int i = 0; i < urlSpan.Length; i++)
                {
                    if (urlSpan[i] == '/')
                    {
                        slashCount++;

                        if (slashCount == 3)
                        {
                            pathStartIndex = i;
                            break;
                        }
                    }
                }

                // If we found a path and it's not empty, use it
                if (pathStartIndex != -1 && pathStartIndex < urlSpan.Length - 1)
                {
                    contentStart = pathStartIndex;
                    contentLength = fullURL.Length - pathStartIndex;
                }
            }

            // Wrap in underline tag for UI display - single allocation using string.Create
            string shortenedURL = string.Create(
                contentLength + 7,
                (url: fullURL, start: contentStart, length: contentLength),
                (chars, state) =>
                {
                    "<u>".AsSpan().CopyTo(chars);
                    state.url.AsSpan(state.start, state.length).CopyTo(chars.Slice(3));
                    "</u>".AsSpan().CopyTo(chars.Slice(3 + state.length));
                });

            dataSourceBinding?.Value.Add(new DebugOngoingWebRequestDef.DebugWebRequestInfo
            {
                Request = request.UnityWebRequest,
                StartTime = startTime,
                ShortenedUrl = shortenedURL,
                Duration = 0,
            });
        }

        public override void OnRequestEnded(ITypedWebRequest request, TimeSpan duration)
        {
            counter--;
            dataSourceBinding?.Value.Remove(request.UnityWebRequest);
        }
    }
}
