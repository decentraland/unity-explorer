using DCL.Diagnostics;
using JetBrains.Annotations;
using System;
using System.Threading;

namespace SceneRuntime.Apis.Modules.CommunicationsControllerApi.SDKMessageBus
{
    public class SDKMessageBusCommsControllerAPIWrapper : JsApiWrapper<ISDKMessageBusCommsControllerAPI>
    {
        public SDKMessageBusCommsControllerAPIWrapper(ISDKMessageBusCommsControllerAPI api, CancellationTokenSource disposeCts)
            : base(api, disposeCts) { }

        [UsedImplicitly]
        public void Send(string data)
        {
            try { api.Send(data); }
            catch (Exception e)
            {
                ReportHub.LogException(e, ReportCategory.ENGINE);
                throw;
            }
        }
    }
}
