using DCL.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace DCL.Web3.Identities
{
    /// <summary>
    ///     Writes the logged-in identity to the launcher's directory as <c>session-info-{session_id}.json</c>
    ///     so the launcher can submit a crash report on the user's behalf after the client exits unexpectedly.
    ///     The file is rewritten whenever the identity changes (re-login) and removed on logout; the
    ///     clean-shutdown path (wired through <c>ExitUtils</c> at the composition root) deletes it via
    ///     <see cref="DeleteSessionInfoFile" />, so a copy only survives a crash.
    ///     Nothing here logs the file content or the identity.
    /// </summary>
    public sealed class ExplorerSessionInfoWriter : IDisposable
    {
        public const string CLEANUP_CANDIDATE_NAME = nameof(ExplorerSessionInfoWriter);

        private const int FILE_VERSION = 1;

        private readonly IWeb3IdentityCache identityCache;
        private readonly PlayerPrefsIdentityProvider.IWeb3IdentityJsonSerializer identitySerializer;
        private readonly string sessionId;
        private readonly string explorerVersion;
        private readonly bool enabled;
        private readonly string filePath;
        private readonly string tempFilePath;

        private bool disposed;

        public ExplorerSessionInfoWriter(
            IWeb3IdentityCache identityCache,
            PlayerPrefsIdentityProvider.IWeb3IdentityJsonSerializer identitySerializer,
            string sessionId,
            string explorerVersion,
            string launcherDirectory)
        {
            this.identityCache = identityCache;
            this.identitySerializer = identitySerializer;
            this.sessionId = sessionId;
            this.explorerVersion = explorerVersion;

            // Without a session id (editor / manual runs) there is no launcher to hand the file to, so the
            // writer stays inert: it subscribes to nothing and writes nothing.
            enabled = !string.IsNullOrEmpty(sessionId);

            filePath = enabled ? Path.Combine(launcherDirectory, $"session-info-{sessionId}.json") : string.Empty;
            tempFilePath = enabled ? filePath + ".tmp" : string.Empty;

            if (!enabled)
                return;

            identityCache.OnIdentityChanged += OnIdentityChanged;
            identityCache.OnIdentityCleared += DeleteSessionInfoFile;

            // Write a copy immediately when a valid identity already exists (e.g. auto-login completed
            // before this writer was wired), so a crash before the next identity change stays reportable.
            if (identityCache.Identity != null)
                Write(identityCache.Identity);
        }

        public void Dispose()
        {
            if (disposed || !enabled) return;
            disposed = true;

            identityCache.OnIdentityChanged -= OnIdentityChanged;
            identityCache.OnIdentityCleared -= DeleteSessionInfoFile;
        }

        private void OnIdentityChanged()
        {
            IWeb3Identity? identity = identityCache.Identity;

            if (identity == null)
                DeleteSessionInfoFile();
            else
                Write(identity);
        }

        private void Write(IWeb3Identity identity)
        {
            try
            {
                // Reuse the identity serializer the client persists with, so the "identity" object matches
                // userdata_*.json byte-for-byte (field names, expiration "O" format) and the launcher reads
                // one shape.
                var identityJson = JObject.Parse(identitySerializer.Serialize(identity));

                string wallet = identity.Address;

                var root = new JObject
                {
                    ["version"] = FILE_VERSION,
                    ["session_id"] = sessionId,
                    ["wallet"] = wallet,
                    ["explorer_version"] = explorerVersion,
                    ["identity"] = identityJson,
                };

                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                // Write the temp file then move it over the target, so a reader never observes a
                // half-written credential file.
                File.WriteAllText(tempFilePath, root.ToString(Formatting.None));

                if (File.Exists(filePath))
                    File.Delete(filePath);

                File.Move(tempFilePath, filePath);
            }
            catch (Exception e)
            {
                // Never include the identity or file content in the log line.
                ReportHub.LogError(ReportCategory.AUTHENTICATION, $"Could not write launcher session info file: {e.GetType().Name}");
            }
        }

        /// <summary>
        ///     Removes the session-info file (and any stale temp file). Registered as the clean-shutdown
        ///     cleanup at the composition root, so the file survives only a crash.
        /// </summary>
        public void DeleteSessionInfoFile()
        {
            if (!enabled)
                return;

            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);

                if (File.Exists(tempFilePath))
                    File.Delete(tempFilePath);
            }
            catch (Exception e)
            {
                ReportHub.LogError(ReportCategory.AUTHENTICATION, $"Could not delete launcher session info file: {e.GetType().Name}");
            }
        }
    }
}
