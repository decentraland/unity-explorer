using System;
using System.Linq;
using System.Reflection;
using Thirdweb;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Web3.Authenticators.ManualTest
{
    /// <summary>
    ///     Helper class for manual testing that uses reflection to find the active ThirdWebAuthenticator instance.
    ///     This avoids the need for singletons while still allowing Editor/runtime testing via ContextMenu.
    ///     NOT intended for production use.
    /// </summary>
    public static class ThirdWebTestHelper
    {
        private static ThirdWebAuthenticator? cachedAuthenticator;

        /// <summary>
        ///     Gets the current ThirdWebAuthenticator instance by searching through registered services.
        ///     Uses reflection to find the instance without requiring a singleton pattern.
        /// </summary>
        public static ThirdWebAuthenticator? GetAuthenticator()
        {
            if (cachedAuthenticator != null)
                return cachedAuthenticator;

            cachedAuthenticator = FindAuthenticatorViaReflection();

            if (cachedAuthenticator == null)
                Debug.LogWarning("[ThirdWebTestHelper] Could not find ThirdWebAuthenticator instance. Make sure the app is fully initialized and logged in.");

            return cachedAuthenticator;
        }

        /// <summary>
        ///     Gets the active wallet from the authenticator.
        /// </summary>
        public static IThirdwebWallet? GetActiveWallet()
        {
            ThirdWebAuthenticator? auth = GetAuthenticator();
            return auth?.ActiveWallet;
        }

        /// <summary>
        ///     Clears the cached authenticator reference. Call this when the scene reloads or app restarts.
        /// </summary>
        public static void ClearCache()
        {
            cachedAuthenticator = null;
        }

        private static ThirdWebAuthenticator? FindAuthenticatorViaReflection()
        {
            try
            {
                // Approach 1: Find MainSceneLoader (MonoBehaviour) and extract bootstrapContainer from it
                ThirdWebAuthenticator? authFromMainSceneLoader = FindViaMainSceneLoader();

                if (authFromMainSceneLoader != null)
                    return authFromMainSceneLoader;

                // Approach 2: Search for CompositeWeb3Provider in static fields across assemblies
                ThirdWebAuthenticator? authFromStaticFields = FindViaStaticFields();

                if (authFromStaticFields != null)
                    return authFromStaticFields;

                Debug.LogWarning("[ThirdWebTestHelper] Could not find ThirdWebAuthenticator via reflection. Is the app fully initialized?");
            }
            catch (Exception ex) { Debug.LogError($"[ThirdWebTestHelper] Error finding authenticator: {ex.Message}"); }

            return null;
        }

        /// <summary>
        ///     Finds ThirdWebAuthenticator via MainSceneLoader -> bootstrapContainer -> CompositeWeb3Provider -> thirdWebAuth
        /// </summary>
        private static ThirdWebAuthenticator? FindViaMainSceneLoader()
        {
            // Find MainSceneLoader type
            Type? mainSceneLoaderType = AppDomain.CurrentDomain.GetAssemblies()
                                                 .SelectMany(a =>
                                                  {
                                                      try { return a.GetTypes(); }
                                                      catch { return Array.Empty<Type>(); }
                                                  })
                                                 .FirstOrDefault(t => t.Name == "MainSceneLoader");

            if (mainSceneLoaderType == null)
            {
                Debug.Log("[ThirdWebTestHelper] MainSceneLoader type not found");
                return null;
            }

            // Find the active MainSceneLoader instance in the scene
            Object[] mainSceneLoaders = Object.FindObjectsByType(mainSceneLoaderType, FindObjectsSortMode.None);

            if (mainSceneLoaders.Length == 0)
            {
                Debug.Log("[ThirdWebTestHelper] No MainSceneLoader instance found in scene");
                return null;
            }

            object mainSceneLoader = mainSceneLoaders[0];
            Debug.Log($"[ThirdWebTestHelper] Found MainSceneLoader: {mainSceneLoader}");

            // Get bootstrapContainer field (private)
            FieldInfo? bootstrapContainerField = mainSceneLoaderType.GetField("bootstrapContainer", BindingFlags.NonPublic | BindingFlags.Instance);

            if (bootstrapContainerField == null)
            {
                Debug.Log("[ThirdWebTestHelper] bootstrapContainer field not found on MainSceneLoader");
                return null;
            }

            object? bootstrapContainer = bootstrapContainerField.GetValue(mainSceneLoader);

            if (bootstrapContainer == null)
            {
                Debug.Log("[ThirdWebTestHelper] bootstrapContainer is null - app may not be fully initialized");
                return null;
            }

            Debug.Log($"[ThirdWebTestHelper] Found BootstrapContainer: {bootstrapContainer}");

            // Get CompositeWeb3Provider property
            PropertyInfo? compositeProperty = bootstrapContainer.GetType().GetProperty("CompositeWeb3Provider", BindingFlags.Public | BindingFlags.Instance);

            if (compositeProperty != null)
            {
                object? compositeProvider = compositeProperty.GetValue(bootstrapContainer);

                if (compositeProvider != null)
                {
                    ThirdWebAuthenticator? auth = ExtractThirdWebAuthFromComposite(compositeProvider);

                    if (auth != null)
                    {
                        Debug.Log("[ThirdWebTestHelper] Found ThirdWebAuthenticator via MainSceneLoader -> BootstrapContainer -> CompositeWeb3Provider");
                        return auth;
                    }
                }
            }

            // Fallback: try EthereumApi property
            PropertyInfo? ethereumApiProperty = bootstrapContainer.GetType().GetProperty("EthereumApi", BindingFlags.Public | BindingFlags.Instance);

            if (ethereumApiProperty != null)
            {
                object? ethereumApi = ethereumApiProperty.GetValue(bootstrapContainer);

                if (ethereumApi is ThirdWebAuthenticator directAuth)
                {
                    Debug.Log("[ThirdWebTestHelper] Found ThirdWebAuthenticator via MainSceneLoader -> BootstrapContainer -> EthereumApi");
                    return directAuth;
                }

                if (ethereumApi != null)
                {
                    ThirdWebAuthenticator? auth = ExtractThirdWebAuthFromComposite(ethereumApi);

                    if (auth != null)
                    {
                        Debug.Log("[ThirdWebTestHelper] Found ThirdWebAuthenticator via MainSceneLoader -> BootstrapContainer -> EthereumApi (composite)");
                        return auth;
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Searches for ThirdWebAuthenticator or CompositeWeb3Provider in static fields across DCL assemblies.
        /// </summary>
        private static ThirdWebAuthenticator? FindViaStaticFields()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                string? assemblyName = assembly.FullName;

                if (assemblyName == null)
                    continue;

                if (!assemblyName.StartsWith("DCL") && !assemblyName.StartsWith("Global") && !assemblyName.StartsWith("Assembly-CSharp"))
                    continue;

                Type[] types;

                try { types = assembly.GetTypes(); }
                catch { continue; }

                foreach (Type type in types)
                {
                    FieldInfo[] staticFields = type.GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

                    foreach (FieldInfo field in staticFields)
                    {
                        try
                        {
                            object? value = field.GetValue(null);

                            if (value == null)
                                continue;

                            // Direct ThirdWebAuthenticator
                            if (value is ThirdWebAuthenticator directAuth)
                            {
                                Debug.Log($"[ThirdWebTestHelper] Found ThirdWebAuthenticator in static field {type.Name}.{field.Name}");
                                return directAuth;
                            }

                            // Check if it's a composite provider
                            if (value.GetType().Name == "CompositeWeb3Provider")
                            {
                                ThirdWebAuthenticator? auth = ExtractThirdWebAuthFromComposite(value);

                                if (auth != null)
                                {
                                    Debug.Log($"[ThirdWebTestHelper] Found ThirdWebAuthenticator via CompositeWeb3Provider in static field {type.Name}.{field.Name}");
                                    return auth;
                                }
                            }
                        }
                        catch
                        {
                            // Ignore reflection errors on individual fields
                        }
                    }
                }
            }

            return null;
        }

        /// <summary>
        ///     Extracts ThirdWebAuthenticator from CompositeWeb3Provider using reflection.
        /// </summary>
        private static ThirdWebAuthenticator? ExtractThirdWebAuthFromComposite(object compositeProvider)
        {
            try
            {
                Type compositeType = compositeProvider.GetType();

                // CompositeWeb3Provider has a private readonly field "thirdWebAuth"
                FieldInfo? thirdWebField = compositeType.GetField("thirdWebAuth", BindingFlags.NonPublic | BindingFlags.Instance);

                if (thirdWebField != null)
                {
                    object? thirdWebAuth = thirdWebField.GetValue(compositeProvider);

                    if (thirdWebAuth is ThirdWebAuthenticator auth)
                        return auth;
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[ThirdWebTestHelper] Failed to extract ThirdWebAuthenticator from composite: {ex.Message}"); }

            return null;
        }
    }
}
