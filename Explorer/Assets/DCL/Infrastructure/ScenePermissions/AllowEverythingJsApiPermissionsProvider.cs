namespace SceneRuntime.ScenePermissions
{
    public class AllowEverythingJsApiPermissionsProvider : IJsApiPermissionsProvider
    {
        public bool CanOpenExternalUrl() => true;

        public bool CanInvokeWebSocketsAPI() => true;

        public bool CanInvokeFetchAPI() => true;

        public bool CanInvokeWeb3API() => true;
    }
}
