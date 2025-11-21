namespace SceneRuntime.ScenePermissions
{
    public interface IJsApiPermissionsProvider
    {
        bool CanOpenExternalUrl();

        bool CanInvokeWebSocketsAPI();

        bool CanInvokeFetchAPI();

        bool CanInvokeWeb3API();
    }
}
