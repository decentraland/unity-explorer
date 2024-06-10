using DCL.Diagnostics;
using JetBrains.Annotations;
using System;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus
{
    public class SDKMessageBusCommsControllerAPIWrapper : IJsApiWrapper
    {
        private readonly ISDKMessageBusCommsControllerAPI api;

        public SDKMessageBusCommsControllerAPIWrapper(ISDKMessageBusCommsControllerAPI api)
        {
            this.api = api;
        }

        public void OnSceneIsCurrentChanged(bool isCurrent)
        {
            api.OnSceneIsCurrentChanged(isCurrent);
        }

        [UsedImplicitly]
        public void Send(string data)
        {
            try
            {
                api.Send(data);
            }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ENGINE);
                throw;
            }
        }

        public void Dispose()
        {
            api.Dispose();
        }
    }
}
