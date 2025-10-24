using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using LiveKit.Proto;
using LiveKit.Rooms;
using LiveKit.Rooms.AsyncInstractions;
using LiveKit.Rooms.Tracks;
using LiveKit.RtcSources.Video;
using RichTypes;
using System;
using System.Text;
using System.Threading;
using Utility.Multithreading;

namespace DCL.Multiplayer.Connections.Cast
{
    public class DCLCast
    {
        private readonly Room room = new ();
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urls;
        private readonly SemaphoreSlim semaphoreSlim = new (initialCount: 1, maxCount: 1);

        /// <summary>
        /// Is ok to be mutable and shared per instance, single execution is protected by semaphore
        /// </summary>
        private readonly StringBuilder payloadBuilder = new ();

        private ITrack? currentVideoTrack;

        public DCLCast(IWebRequestController webRequestController, IDecentralandUrlsSource urls)
        {
            this.webRequestController = webRequestController;
            this.urls = urls;
        }

        public async UniTask<Result> StartAsync(string token, CancellationToken ct)
        {
            using SlimScope _ = await semaphoreSlim.LockAsync();
            await StopInternalAsync(ct);

            URLAddress url = URLAddress.FromString(urls.Url(DecentralandUrl.GateKeeperStreamToken));
            CommonArguments arguments = new CommonArguments(url);

            payloadBuilder.Clear();

            payloadBuilder.Append("{")
                          .Append("\"token\": ")
                          .Append('"')
                          .Append(token)
                          .Append('"')
                          .Append("}");

            string payload = payloadBuilder.ToString();

            GenericPostArguments postArguments = GenericPostArguments.CreateJson(payload);
            TokenResponse response;

            try
            {
                response =
                    await webRequestController
                         .PostAsync(arguments, postArguments, ct, ReportCategory.DCL_CAST)
                         .CreateFromJson<TokenResponse>(WRJsonParser.Unity);
            }
            catch (Exception e) { return Result.ErrorResult($"Cannot complete request to: {url.Value} with payload {payload}: " + (e.Message ?? "Error on request")); }

            Result<(string url, string token)> credentials = response.Credentials();

            if (credentials.Success == false)
                return Result.ErrorResult($"Invalid credentials: {credentials.ErrorMessage}");

            await room.DisconnectAsync(ct);
            Result result = await room.ConnectAsync(credentials.Value.url, credentials.Value.token, ct, false);

            if (result.Success == false)
                return Result.ErrorResult($"Cannot connect to room: {result.ErrorMessage}");

            //TODO publish audio
            Result<WebCameraVideoInput> videoInput = WebCameraVideoInput.NewDefault();

            if (videoInput.Success == false)
                return Result.ErrorResult($"Cannot initialize video input: {videoInput.ErrorMessage}");

            RtcVideoSource rtcVideoSource = new (videoInput.Value);
            currentVideoTrack = room.LocalTracks.CreateVideoTrack("stream", rtcVideoSource);

            TrackPublishOptions options = new ()
            {
                VideoEncoding = new VideoEncoding
                {
                    MaxFramerate = 60,
                },
                Source = TrackSource.SourceCamera,
            };

            PublishTrackInstruction instruction = room.Participants.LocalParticipant().PublishTrack(currentVideoTrack, options, ct);
            await UniTask.WaitUntil(() => instruction.IsDone, cancellationToken: ct);

            if (instruction.IsError)
                return Result.ErrorResult($"Cannot publish video track: {instruction.ErrorMessage}");

            return Result.SuccessResult();
        }

        public async UniTask StopAsync(CancellationToken ct)
        {
            using SlimScope _ = await semaphoreSlim.LockAsync();
            await StopInternalAsync(ct);
        }

        private async UniTask StopInternalAsync(CancellationToken ct)
        {
            if (currentVideoTrack == null)
                return;

            room.Participants.LocalParticipant().UnpublishTrack(currentVideoTrack, stopOnUnpublish: true);
            currentVideoTrack = null;
            await room.DisconnectAsync(ct);
        }

        [Serializable]
        private struct TokenResponse
        {
            public string? token;
            public string? url;
            public string? roomId;

            public Result<(string url, string token)> Credentials()
            {
                if (url == null)
                    return Result<(string url, string token)>.ErrorResult("Url is null");

                if (token == null)
                    return Result<(string url, string token)>.ErrorResult("Token is null");

                return Result<(string url, string token)>.SuccessResult((url, token));
            }
        }
    }
}
