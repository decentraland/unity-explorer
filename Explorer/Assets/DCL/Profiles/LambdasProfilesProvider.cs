using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DCL.Profiles
{
    public class LambdasProfilesProvider
    {
        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource urlsSource;

        private string lambdasProfilesBaseUrl => urlsSource.Url(DecentralandUrl.LambdasProfiles);

        public LambdasProfilesProvider(
            IWebRequestController webRequestController,
            IDecentralandUrlsSource urlsSource)
        {
            this.webRequestController = webRequestController;
            this.urlsSource = urlsSource;
        }

        public async UniTask<List<GetAvatarsDetailsDto>> GetAvatarsDetailsAsync(List<string> userIds, CancellationToken ct)
        {
            StringBuilder bodyBuilder = new ();
            bodyBuilder.Clear();
            bodyBuilder.Append("{\"ids\":[");

            for (var i = 0; i < userIds.Count; ++i)
            {
                bodyBuilder.Append('\"');
                bodyBuilder.Append(userIds[i]);
                bodyBuilder.Append('\"');

                if (i != userIds.Count - 1)
                    bodyBuilder.Append(",");
            }

            bodyBuilder.Append("]}");

            GenericDownloadHandlerUtils.Adapter<GenericPostRequest, GenericPostArguments> result = webRequestController.PostAsync(
                lambdasProfilesBaseUrl,
                GenericPostArguments.CreateJson(bodyBuilder.ToString()),
                ct,
                ReportCategory.PROFILE);

            var response = await result.CreateFromJson<List<GetAvatarsDetailsDto>>(WRJsonParser.Newtonsoft,
                createCustomExceptionOnFailure: static (_, text) => new Exception($"Error parsing Get Avatars details response: {text}"));

            if (response == null)
                throw new Exception("No Avatars details info retrieved");

            return response;
        }
    }
}
