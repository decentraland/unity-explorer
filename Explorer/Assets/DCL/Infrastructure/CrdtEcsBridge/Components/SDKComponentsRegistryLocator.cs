namespace CrdtEcsBridge.Components
{
    /// <summary>
    ///     Глобальный локатор реестра SDK-компонентов для утилит, у которых нет DI-доступа.
    /// </summary>
    public static class SDKComponentsRegistryLocator
    {
        private static ISDKComponentsRegistry registry;

        public static void Register(ISDKComponentsRegistry value)
        {
            registry = value;
        }

        public static bool TryGet(out ISDKComponentsRegistry value)
        {
            value = registry;
            return value != null;
        }
    }
}
