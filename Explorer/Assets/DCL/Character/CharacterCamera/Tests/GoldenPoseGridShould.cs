using DCL.CharacterCamera;
using NUnit.Framework;
using System;
using UnityEngine;

namespace DCL.Character.CharacterCamera.Tests
{
    /// <summary>
    ///     The frozen camera pose has to land on one grid point from every platform's float noise:
    ///     inputs a fraction of a step apart snap to the same value, a snapped position reproduces
    ///     itself exactly, and a snapped rotation is the renormalised grid point, unit-length.
    /// </summary>
    [TestFixture]
    public class GoldenPoseGridShould
    {
        private const int SAMPLES = 2000;
        private const int SEED = 0x5EED;
        private const double POSITION_STEP = 1.0 / GoldenPoseGrid.POSITION_GRID;
        private const double ROTATION_STEP = 1.0 / GoldenPoseGrid.ROTATION_GRID;
        private const double HALF_STEP_MARGIN = 0.45;

        private System.Random random = null!;

        [SetUp]
        public void SetUp()
        {
            random = new System.Random(SEED);
        }

        [Test]
        public void SnapPositionsIdempotently()
        {
            for (var i = 0; i < SAMPLES; i++)
            {
                Vector3 once = GoldenPoseGrid.SnapPosition(RandomPosition());
                Vector3 twice = GoldenPoseGrid.SnapPosition(once);

                Assert.That(twice.x, Is.EqualTo(once.x), $"x re-snapped {once} -> {twice}");
                Assert.That(twice.y, Is.EqualTo(once.y), $"y re-snapped {once} -> {twice}");
                Assert.That(twice.z, Is.EqualTo(once.z), $"z re-snapped {once} -> {twice}");
            }
        }

        [Test]
        public void SnapPositionsOntoThe32ndOfAMetreGrid()
        {
            for (var i = 0; i < SAMPLES; i++)
            {
                Vector3 input = RandomPosition();
                Vector3 snapped = GoldenPoseGrid.SnapPosition(input);

                for (var axis = 0; axis < 3; axis++)
                {
                    double scaled = snapped[axis] * GoldenPoseGrid.POSITION_GRID;
                    Assert.That(scaled, Is.EqualTo(Math.Round(scaled)), $"axis {axis} of {snapped} is off the grid");
                    Assert.That(Math.Abs(snapped[axis] - input[axis]), Is.LessThanOrEqualTo(POSITION_STEP / 2 + 1e-4), $"axis {axis} moved more than half a step from {input}");
                }
            }
        }

        [Test]
        public void SnapNeighbouringPositionsToTheSameGridPoint()
        {
            for (var i = 0; i < SAMPLES; i++)
            {
                Vector3 gridPoint = RandomGridPosition();
                Vector3 expected = GoldenPoseGrid.SnapPosition(gridPoint);
                Assert.That(expected, Is.EqualTo(gridPoint), "a grid point must snap to itself");

                Vector3 neighbour = new (
                    (float)(gridPoint.x + Jitter(POSITION_STEP)),
                    (float)(gridPoint.y + Jitter(POSITION_STEP)),
                    (float)(gridPoint.z + Jitter(POSITION_STEP)));

                Vector3 snapped = GoldenPoseGrid.SnapPosition(neighbour);
                Assert.That(snapped.x, Is.EqualTo(expected.x), $"{neighbour} left the grid point {gridPoint}");
                Assert.That(snapped.y, Is.EqualTo(expected.y), $"{neighbour} left the grid point {gridPoint}");
                Assert.That(snapped.z, Is.EqualTo(expected.z), $"{neighbour} left the grid point {gridPoint}");
            }
        }

        [Test]
        public void SnapRotationsToTheRenormalisedGridPoint()
        {
            for (var i = 0; i < SAMPLES; i++)
            {
                Quaternion input = RandomRotation();
                Quaternion snapped = GoldenPoseGrid.SnapRotation(input);

                double gx = GoldenPoseGrid.Snap(input.x, GoldenPoseGrid.ROTATION_GRID);
                double gy = GoldenPoseGrid.Snap(input.y, GoldenPoseGrid.ROTATION_GRID);
                double gz = GoldenPoseGrid.Snap(input.z, GoldenPoseGrid.ROTATION_GRID);
                double gw = GoldenPoseGrid.Snap(input.w, GoldenPoseGrid.ROTATION_GRID);
                // mirrors the implementation's arithmetic (reciprocal, then multiply) so the float
                // cast lands on the same value bit for bit instead of within a rounding-boundary ULP
                double inv = 1.0 / Math.Sqrt((gx * gx) + (gy * gy) + (gz * gz) + (gw * gw));

                // the grid point itself sits on the 2^-11 lattice; the pose is that point scaled to unit length
                Assert.That(gx * GoldenPoseGrid.ROTATION_GRID, Is.EqualTo(Math.Round(gx * GoldenPoseGrid.ROTATION_GRID)));
                Assert.That(snapped.x, Is.EqualTo((float)(gx * inv)), $"x of {input} did not land on the renormalised grid point");
                Assert.That(snapped.y, Is.EqualTo((float)(gy * inv)), $"y of {input} did not land on the renormalised grid point");
                Assert.That(snapped.z, Is.EqualTo((float)(gz * inv)), $"z of {input} did not land on the renormalised grid point");
                Assert.That(snapped.w, Is.EqualTo((float)(gw * inv)), $"w of {input} did not land on the renormalised grid point");

                double norm = Math.Sqrt(((double)snapped.x * snapped.x) + ((double)snapped.y * snapped.y) + ((double)snapped.z * snapped.z) + ((double)snapped.w * snapped.w));
                Assert.That(norm, Is.EqualTo(1.0).Within(1e-6), $"{snapped} is not unit length");

                for (var component = 0; component < 4; component++)
                    Assert.That(Math.Abs(snapped[component] - input[component]), Is.LessThanOrEqualTo(ROTATION_STEP), $"component {component} moved more than one grid step from {input}");
            }
        }

        [Test]
        public void SnapNeighbouringRotationsToTheSameGridPoint()
        {
            for (var i = 0; i < SAMPLES; i++)
            {
                Quaternion gridPoint = RandomGridRotation();
                Quaternion expected = GoldenPoseGrid.SnapRotation(gridPoint);

                Quaternion neighbour = new (
                    (float)(gridPoint.x + Jitter(ROTATION_STEP)),
                    (float)(gridPoint.y + Jitter(ROTATION_STEP)),
                    (float)(gridPoint.z + Jitter(ROTATION_STEP)),
                    (float)(gridPoint.w + Jitter(ROTATION_STEP)));

                Quaternion snapped = GoldenPoseGrid.SnapRotation(neighbour);
                Assert.That(snapped.x, Is.EqualTo(expected.x), $"{neighbour} left the grid point {gridPoint}");
                Assert.That(snapped.y, Is.EqualTo(expected.y), $"{neighbour} left the grid point {gridPoint}");
                Assert.That(snapped.z, Is.EqualTo(expected.z), $"{neighbour} left the grid point {gridPoint}");
                Assert.That(snapped.w, Is.EqualTo(expected.w), $"{neighbour} left the grid point {gridPoint}");
            }
        }

        [Test]
        public void KeepReSnappedRotationsWithinOneGridStep()
        {
            // renormalisation pulls the components off the lattice by up to half a step, so a
            // re-snap may pick a neighbouring lattice point; it never drifts further than one step
            for (var i = 0; i < SAMPLES; i++)
            {
                Quaternion once = GoldenPoseGrid.SnapRotation(RandomRotation());
                Quaternion twice = GoldenPoseGrid.SnapRotation(once);

                for (var component = 0; component < 4; component++)
                    Assert.That(Math.Abs(twice[component] - once[component]), Is.LessThanOrEqualTo(ROTATION_STEP), $"component {component} drifted {once} -> {twice}");
            }
        }

        [TestCase(0.0, 32.0, 0.0)]
        [TestCase(0.015, 32.0, 0.0)]
        [TestCase(0.016, 32.0, 0.03125)]
        [TestCase(-0.016, 32.0, -0.03125)]
        [TestCase(1.03125, 32.0, 1.03125)]
        [TestCase(0.75, 2048.0, 0.75)]
        [TestCase(0.7501, 2048.0, 0.75)]
        public void SnapScalarsToTheNearestGridLine(double value, double grid, double expected)
        {
            Assert.That(GoldenPoseGrid.Snap(value, grid), Is.EqualTo(expected));
        }

        private Vector3 RandomPosition() =>
            new ((float)Uniform(-1000, 1000), (float)Uniform(-1000, 1000), (float)Uniform(-1000, 1000));

        private Vector3 RandomGridPosition() =>
            new ((float)(random.Next(-32000, 32000) * POSITION_STEP), (float)(random.Next(-32000, 32000) * POSITION_STEP), (float)(random.Next(-32000, 32000) * POSITION_STEP));

        private Quaternion RandomRotation()
        {
            double x = Gaussian(), y = Gaussian(), z = Gaussian(), w = Gaussian();
            double inv = 1.0 / Math.Sqrt((x * x) + (y * y) + (z * z) + (w * w));
            return new Quaternion((float)(x * inv), (float)(y * inv), (float)(z * inv), (float)(w * inv));
        }

        // integer multiples of 2^-11 are exact in float, so the lattice point survives the cast
        private Quaternion RandomGridRotation()
        {
            Quaternion unit = RandomRotation();

            return new Quaternion(
                (float)GoldenPoseGrid.Snap(unit.x, GoldenPoseGrid.ROTATION_GRID),
                (float)GoldenPoseGrid.Snap(unit.y, GoldenPoseGrid.ROTATION_GRID),
                (float)GoldenPoseGrid.Snap(unit.z, GoldenPoseGrid.ROTATION_GRID),
                (float)GoldenPoseGrid.Snap(unit.w, GoldenPoseGrid.ROTATION_GRID));
        }

        private double Jitter(double step) =>
            Uniform(-HALF_STEP_MARGIN, HALF_STEP_MARGIN) * step;

        private double Uniform(double min, double max) =>
            min + (random.NextDouble() * (max - min));

        private double Gaussian()
        {
            double u1 = 1.0 - random.NextDouble();
            double u2 = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
