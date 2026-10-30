using DCL.McpServer.Utils;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DCL.McpServer.Tests
{
    public class CameraAimReportShould
    {
        [Test]
        public void OmitTheWarningWhenOnTarget()
        {
            // Arrange
            var result = new JObject();

            // Act
            float error = CameraAimReport.Append(result, Vector3.zero, Quaternion.identity, new Vector3(0f, 0f, 10f));

            // Assert
            Assert.That(error, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result["aimErrorDegrees"]!.Value<float>(), Is.EqualTo(0f).Within(0.01f));
            Assert.That(result["cameraRotationEuler"], Is.Not.Null);
            Assert.That(result["warning"], Is.Null);
        }

        [Test]
        public void AppendAWarningAboveTheThreshold()
        {
            // Arrange
            var result = new JObject();

            // Act — the camera looks along +z while the target sits 30 degrees to the side.
            float error = CameraAimReport.Append(result, Vector3.zero, Quaternion.identity, Quaternion.Euler(0f, 30f, 0f) * Vector3.forward);

            // Assert
            Assert.That(error, Is.EqualTo(30f).Within(0.1f));
            Assert.That(result["aimErrorDegrees"]!.Value<float>(), Is.EqualTo(30f).Within(0.1f));
            Assert.That(result["warning"]!.Value<string>(), Is.EqualTo(CameraAimReport.RESIDUAL_WARNING));
        }
    }
}
