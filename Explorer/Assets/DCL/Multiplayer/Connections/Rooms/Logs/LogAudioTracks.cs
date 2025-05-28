using DCL.Diagnostics;
using LiveKit;
using LiveKit.Rooms;
using LiveKit.Rooms.Tracks;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace DCL.Multiplayer.Connections.Rooms.Logs
{
    public class LogAudioTracks : IAudioTracks
    {
        private const string PREFIX = "LogAudioTracks:";
        private const int LOG_INTERVAL_MS = 5000;
        
        private readonly IAudioTracks origin;
        private readonly List<(RtcAudioSource source, string trackName)> trackedSources = new();
        private CancellationTokenSource? cancellationTokenSource;
        private bool isLoggingActive = false;

        public LogAudioTracks(IAudioTracks origin)
        {
            this.origin = origin;
        }

        public ITrack CreateAudioTrack(string name, RtcAudioSource source)
        {
            ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: create Audio Track with name {name}");
            var audioTrack = origin.CreateAudioTrack(name, source);
            ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: created Audio Track with name {name} and SID: {audioTrack.Sid}");
            
            //TODO: Add here logic to turn off periodic logging from appArgs or similar
            if (source != null)
            {
                lock (trackedSources)
                {
                    trackedSources.Add((source, name));
                    
                    if (!isLoggingActive && trackedSources.Count == 1)
                    {
                        StartPeriodicLogging();
                    }
                }
                LogAudioSourceStats(source, name);
            }
            
            return audioTrack;
        }

        private void StartPeriodicLogging()
        {
            if (isLoggingActive) return;
            
            isLoggingActive = true;
            cancellationTokenSource = new CancellationTokenSource();
            StartPeriodicLoggingAsync(cancellationTokenSource.Token).Forget();
            ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: Started periodic logging");
        }

        private void StopPeriodicLogging()
        {
            if (!isLoggingActive) return;

            isLoggingActive = false;
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
            ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: Stopped periodic logging");
        }

        private async UniTaskVoid StartPeriodicLoggingAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && isLoggingActive)
                {
                    await UniTask.Delay(LOG_INTERVAL_MS, cancellationToken: cancellationToken);

                    if (!isLoggingActive) break;

                    List<(RtcAudioSource source, string trackName)> sourcesToLog;
                    bool hasActiveSources;

                    lock (trackedSources)
                    {
                        for (int i = trackedSources.Count - 1; i >= 0; i--)
                        {
                            if (trackedSources[i].source == null)
                            {
                                trackedSources.RemoveAt(i);
                            }
                        }

                        hasActiveSources = trackedSources.Count > 0;
                        sourcesToLog = hasActiveSources ? new List<(RtcAudioSource, string)>(trackedSources) : new List<(RtcAudioSource, string)>();

                        if (!hasActiveSources && isLoggingActive)
                        {
                            isLoggingActive = false;
                        }
                    }

                    if (!hasActiveSources)
                    {
                        ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: No active sources, stopping periodic logging");
                        break;
                    }

                    foreach (var (source, trackName) in sourcesToLog)
                    {
                        if (source != null)
                        {
                            LogAudioSourceStats(source, trackName);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: Periodic logging cancelled");
            }
            catch (Exception ex)
            {
                ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: Error in periodic logging: {ex.Message}");
            }
            finally
            {
                isLoggingActive = false;
            }
        }

        private void LogAudioSourceStats(RtcAudioSource source, string trackName)
        {
            if (source == null)
            {
                ReportHub.Log(ReportCategory.LIVEKIT_AUDIO, $"{PREFIX}: RtcAudioSource is null for track {trackName}");
                return;
            }

            ReportHub.Log(ReportCategory.LIVEKIT_AUDIO,
                $"{PREFIX}: Audio Track '{trackName}' - Configuration: {source.CurrentSampleRate}Hz, {source.CurrentChannels} channels, Running: {source.IsRunning}");

            ReportHub.Log(ReportCategory.LIVEKIT_AUDIO,
                $"{PREFIX}: Audio Track '{trackName}' - Queue Stats: Size={source.QueueSize}, " +
                $"Frames Sent={source.TotalFramesSent}, Blank Frames={source.TotalBlankFramesSent}, " +
                $"Dropped Frames={source.TotalDroppedFrames}, Batches Sent={source.TotalBatchesSent}");

            var totalFrames = source.TotalFramesSent + source.TotalBlankFramesSent;
            var dropRate = totalFrames > 0 ? (source.TotalDroppedFrames * 100.0 / totalFrames) : 0;
            var avgBatchSize = source.TotalBatchesSent > 0 ? (totalFrames / (double)source.TotalBatchesSent) : 0;

            ReportHub.Log(ReportCategory.LIVEKIT_AUDIO,
                $"{PREFIX}: Audio Track '{trackName}' - Performance: Drop Rate={dropRate:F2}%, " +
                $"Avg Batch Size={avgBatchSize:F1} frames/batch");

            if (source.QueueSize >= 8)
            {
                ReportHub.Log(ReportCategory.LIVEKIT_AUDIO,
                    $"{PREFIX}: Audio Track '{trackName}' - Queue approaching capacity ({source.QueueSize}/10)");
            }

            if (source.TotalDroppedFrames > 0)
            {
                ReportHub.Log(ReportCategory.LIVEKIT_AUDIO,
                    $"{PREFIX}: Audio Track '{trackName}' - {source.TotalDroppedFrames} frames have been dropped due to queue overrun");
            }

            if (!source.IsRunning)
            {
                ReportHub.Log(ReportCategory.LIVEKIT_AUDIO,
                    $"{PREFIX}: Audio Track '{trackName}' - RtcAudioSource is not running");
            }
        }
    }
}
