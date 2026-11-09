using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using System.Threading;
using UnityEngine;

namespace DCL.McpServer.Utils
{
    public static class ExposedCameraDataExtensions
    {
        /// <summary>
        ///     The camera pose once a completed camera gesture is visible in <see cref="IExposedCameraData" />, which its
        ///     own system writes: the read waits one frame so the pose reflects the new rotation.
        /// </summary>
        public static async UniTask<(Vector3 position, Quaternion rotation)> ReadSettledPoseAsync(this IExposedCameraData exposedCameraData, CancellationToken ct)
        {
            await UniTask.DelayFrame(1, cancellationToken: ct);

            return (exposedCameraData.WorldPosition.Value, exposedCameraData.WorldRotation.Value);
        }
    }
}
