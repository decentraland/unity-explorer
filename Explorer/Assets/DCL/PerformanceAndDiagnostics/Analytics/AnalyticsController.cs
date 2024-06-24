using CommunicationData.URLHelpers;
using DCL.Character;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Segment.Serialization;
using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics
{
    public class AnalyticsController
    {
        private const string UNDEFINED = "undefined";

        private readonly IAnalyticsService analytics;
        private readonly IRealmNavigator realmNavigator;
        private readonly ICharacterObject playerCharacter;
        private readonly IWeb3IdentityCache identityCache;

        private readonly JsonObject commonFields;

        public JsonObject CommonFields
        {
            get
            {
                commonFields["dcl_eth_address"] = identityCache.Identity == null ? UNDEFINED : identityCache.Identity.Address.ToString();
                commonFields["realm"] = realmNavigator == null ? UNDEFINED : realmNavigator.CurrentRealm.ToString();
                commonFields["position"] = playerCharacter == null ? UNDEFINED : playerCharacter.Position.ToShortString();

                return commonFields;
            }
        }

        private readonly JsonElement[] staticCommonFields;

        public AnalyticsController(IAnalyticsService analyticsService, IRealmNavigator realmNavigator,
            ICharacterObject playerCharacter, IWeb3IdentityCache identityCache)
        {
            analytics = analyticsService;
            this.realmNavigator = realmNavigator;
            this.playerCharacter = playerCharacter;
            this.identityCache = identityCache;

            commonFields = CreateCommonFields();
            staticCommonFields = new JsonElement[]
            {
                commonFields["dcl_eth_address"],
                commonFields["realm"],
                commonFields["position"],
                commonFields["dcl_renderer_type"],
                commonFields["session_id"],
                commonFields["renderer_version"],
                commonFields["runtime"],
            };

            SendSystemInfo();
        }

        private JsonObject CreateCommonFields() =>
            new ()
            {
                ["dcl_eth_address"] = identityCache.Identity != null ? identityCache.Identity.Address.ToString() : "not defined yet",
                ["realm"] = realmNavigator.CurrentRealm.ToString(),
                ["position"] = playerCharacter.Position.ToShortString(),
                ["dcl_renderer_type"] = SystemInfo.deviceType.ToString(), // Desktop, Console, Handeheld (Mobile), Unknown
                ["session_id"] = GenerateSessionId(),
                ["renderer_version"] = Application.version,
                ["runtime"] = Application.isEditor ? "editor" : "build", // do we need it❓
            };


        private static string GenerateSessionId()
        {
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");

            string rawSessionID = deviceId + timestamp;

            return ComputeSha256Hash(rawSessionID);

            string ComputeSha256Hash(string rawData)
            {
                using var sha256Hash = SHA256.Create();

                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                var builder = new StringBuilder();

                foreach (byte b in bytes)
                    builder.Append(b.ToString("x2"));

                return builder.ToString();
            }
        }

        private void SendSystemInfo()
        {

            analytics.Track("system_info_report", new JsonObject
            {
                ["device_model"] = SystemInfo.deviceModel, // "XPS 17 9720 (Dell Inc.)"
                ["operating_system"] = SystemInfo.operatingSystem, // "Windows 11  (10.0.22631) 64bit"
                ["system_memory_size"] = SystemInfo.systemMemorySize, // 65220 in [MB]

                ["processor_type"] = SystemInfo.processorType, // "12th Gen Intel(R) Core(TM) i7-12700H"
                ["processor_count"] = SystemInfo.processorCount, // 20

                ["graphics_device_name"] = SystemInfo.graphicsDeviceName, // "NVIDIA GeForce RTX 3050 Laptop GPU"
                ["graphics_memory_size"] = SystemInfo.graphicsMemorySize, // 3965 in [MB]
                ["graphics_device_type"] = SystemInfo.graphicsDeviceType.ToString(), // "Direct3D11", Vulkan, OpenGLCore, XBoxOne...
                ["graphics_device_version"] = SystemInfo.graphicsDeviceVersion, // "Direct3D 11.0 [level 11.1]"
            });
        }
    }
}
