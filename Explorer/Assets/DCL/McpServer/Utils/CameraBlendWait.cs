using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using System.Threading;

namespace DCL.McpServer.Utils
{
    /// <summary>
    ///     Holds a capture while Cinemachine blends between cameras (an SDK virtual camera handing back, a mode
    ///     switch), so the frame read is the settled view rather than a mid-transition one.
    /// </summary>
    public static class CameraBlendWait
    {
        /// <summary>
        ///     Yields frames while the brain reports a blend, up to <paramref name="deadlineSec" />. Returns the
        ///     milliseconds spent waiting (0 without a brain or a blend) and whether the blend outlasted the deadline.
        /// </summary>
        public static async UniTask<(int waitedMs, bool stillBlending)> WaitAsync(IExposedCameraData camera, float deadlineSec, CancellationToken ct)
        {
            if (camera.CinemachineBrain == null || !camera.CinemachineBrain.IsBlending)
                return (0, false);

            float start = UnityEngine.Time.realtimeSinceStartup;
            float deadline = start + deadlineSec;

            while (camera.CinemachineBrain.IsBlending)
            {
                if (UnityEngine.Time.realtimeSinceStartup >= deadline)
                    return (ElapsedMs(start), true);

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }

            return (ElapsedMs(start), false);
        }

        private static int ElapsedMs(float start) =>
            (int)((UnityEngine.Time.realtimeSinceStartup - start) * 1000f);
    }
}
