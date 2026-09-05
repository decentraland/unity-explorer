using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace UUAV.Tests
{
    /// <summary>
    /// Coroutine waits with wall-clock timeouts. Every timeout fails the test
    /// with a full runtime diagnostic instead of a bare NUnit timeout, because
    /// "stuck in Opening with these native errors" is the actual finding.
    /// </summary>
    public static class Wait
    {
        public static IEnumerator ForState(UUAVPlayer player, UUAVState expected, float timeout, string context)
        {
            yield return Until(
                () => player.State == expected,
                timeout,
                () => $"waiting for state {expected.ToStringNoAlloc()} ({context})\n{Diagnostics(player)}"
            );
        }

        /// <summary>
        /// Polls until <paramref name="condition"/> holds; fails the test after
        /// <paramref name="timeout"/> wall-clock seconds. A non-zero
        /// <paramref name="pollInterval"/> throttles conditions that are
        /// expensive to evaluate (spawning ps/pgrep, native queries).
        /// </summary>
        public static IEnumerator Until(Func<bool> condition, float timeout, Func<string> failureDetail, float pollInterval = 0f)
        {
            float start = Time.realtimeSinceStartup;
            float lastPoll = float.NegativeInfinity;
            while (true)
            {
                if (Time.realtimeSinceStartup - lastPoll >= pollInterval)
                {
                    lastPoll = Time.realtimeSinceStartup;
                    if (condition())
                    {
                        yield break;
                    }
                }

                if (Time.realtimeSinceStartup - start >= timeout)
                {
                    Assert.Fail($"timed out after {timeout:F0}s: {failureDetail()}");
                }

                yield return null;
            }
        }

        public static IEnumerator SecondsRealtime(float seconds)
        {
            float start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < seconds)
            {
                yield return null;
            }
        }

        public static IEnumerator ForCapture(AudioTapBehaviour audio, float captureSeconds)
        {
            // generous bound: capture advances in real time, so 3x + slack
            // only trips when consumption stalled outright
            yield return Until(
                () => audio.CaptureComplete,
                (captureSeconds * 3f) + 5f,
                () => $"audio capture of {captureSeconds:F1}s never completed (pumpMode={audio.PumpMode})"
            );
        }

        public static string Diagnostics(UUAVPlayer player)
        {
            var sb = new StringBuilder();
            UUAVDebug.Info info = UUAVDebug.Query();
            sb.Append("state=").Append(player.State.ToStringNoAlloc())
              .Append(" url=").Append(player.CurrentUrl)
              .Append(" time=").Append(player.CurrentTime.ToString("F2"))
              .Append(" duration=").Append(player.Duration.ToString("F2"))
              .Append(" lifecycle=").Append(info.Lifecycle)
              .Append(" players=").Append(info.PlayersCount)
              .AppendLine();

            player.CopyDspStats(out long requested, out long returned, out long silenced);
            sb.Append("dsp requested=").Append(requested)
              .Append(" returned=").Append(returned)
              .Append(" silenced=").Append(silenced)
              .AppendLine();

            if (UUAVDebug.TryGetAudioStats(player.PlayerId, out AudioStats audio))
            {
                sb.Append("jitter primed=").Append(audio.JitterPrimed)
                  .Append(" underruns=").Append(audio.JitterUnderruns)
                  .Append(" fillMs=").Append(audio.JitterFillMs.ToString("F1"))
                  .AppendLine();
            }

            var recent = new List<string>();
            UUAVDebug.CopyRecentMessages(recent);
            foreach (string line in recent)
            {
                sb.Append("native: ").AppendLine(line);
            }

            return sb.ToString();
        }
    }
}
