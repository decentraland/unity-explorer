using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DCL.SkyBox.Rendering
{
    /*
     * References:
     * http://guideving.blogspot.co.uk/2010/08/sun-position-in-c.html
     * http://www.astro.uio.no/~bgranslo/aares/calculate.html
     * https://en.wikipedia.org/wiki/Sidereal_time
     */
    public static class SunPosition
    {
        private const float DEG2_RAD = math.TORADIANS;
        private const float RAD_2DEG = math.TODEGREES;

        /*!
         * Suns position calculated from a given date and time in local time, latitude and longitude
         * Time and date are local time.
         * Latitude expressed in decimal degrees.
         * Longitude expressed in decimal degrees.
         */
        [BurstCompile]
        public struct LightJob : IJob
        {
            public struct Result
            {
                public quaternion LightRotation;
                public float LightIntensity;
                public float3 MoonPos;
                public float3 SunPos;
            }

            public int Year;
            public int Month;
            public int Day;
            public float TotalHours;

            public float Latitude;
            public float Longitude;

            public NativeReference<Result> Output;

            public void Execute()
            {
                // Julian calendar (J2000.0) date calculation
                float julianDate = (367 * Year) - (int)(7.0f / 4.0f * (Year + (int)((Month + 9.0f) / 12.0f))) + (int)(275.0f * Month / 9.0f) + Day - 730531.5f;
                float julianCenturies = julianDate / 36525f;

                // Sidereal Time
                float siderealTimeHours = 6.6974f + (2400.0513f * julianCenturies);
                float siderealTimeUT = siderealTimeHours + (366.2422f / 365.2422f * TotalHours);
                float siderealTime = (siderealTimeUT * 15f) + Longitude;

                // Refine to number of days (fractional) to specific time.
                julianDate += TotalHours / 24f;
                julianCenturies = julianDate / 36525f;

                // Solar Coordinates
                float meanLongitude = CorrectAngle(DEG2_RAD * (280.466f + (36000.77f * julianCenturies)));
                float meanAnomaly = CorrectAngle(DEG2_RAD * (357.529f + (35999.05f * julianCenturies)));
                float equationOfCenter = DEG2_RAD * (((1.915f - (0.005f * julianCenturies)) * math.sin(meanAnomaly)) + (0.02f * math.sin(2 * meanAnomaly)));
                float elipticalLongitude = CorrectAngle(meanLongitude + equationOfCenter);
                float obliquity = (23.439f - (0.013f * julianCenturies)) * DEG2_RAD;

                // Right Ascension
                float rightAscension = math.atan2(math.cos(obliquity) * math.sin(elipticalLongitude), math.cos(elipticalLongitude));
                float declination = math.asin(math.sin(rightAscension) * math.sin(obliquity));

                // Horizontal Coordinates
                float hourAngle = CorrectAngle(siderealTime * DEG2_RAD) - rightAscension;

                if (hourAngle > math.PI) hourAngle -= 2 * math.PI;

                float outAltitude = math.asin((math.sin(Latitude * DEG2_RAD) * math.sin(declination)) + (math.cos(Latitude * DEG2_RAD) * math.cos(declination) * math.cos(hourAngle)));

                // Nominator and denominator for calculating Azimuth angle.
                float aziNom = -math.sin(hourAngle);
                float aziDenom = (math.tan(declination) * math.cos(Latitude * DEG2_RAD)) - (math.sin(Latitude * DEG2_RAD) * math.cos(hourAngle));
                float outAzimuth = math.atan(aziNom / aziDenom);

                if (aziDenom < 0) // within 2nd or 3rd quadrant
                {
                    outAzimuth += math.PI;
                }
                else if (aziNom < 0) // within 4th quadrant
                {
                    outAzimuth += 2 * math.PI;
                }

                CalculateLightPosition(outAzimuth, outAltitude);
            }

            private static float CorrectAngle(float angleInRadians)
            {
                if (angleInRadians < 0) return (2.0f * math.PI) - (math.abs(angleInRadians) % (2 * math.PI));

                if (angleInRadians > 2 * math.PI) return angleInRadians % (2 * math.PI);

                return angleInRadians;
            }

            private void CalculateLightPosition(float azi, float alt)
            {
                var result = new Result();

                result.SunPos.x = alt * RAD_2DEG;
                result.SunPos.y = azi * RAD_2DEG;

                float sunPosOffset = result.SunPos.x + 20.0f;
                float sunLoop = math.unlerp(0.0f, 220.0f, sunPosOffset);
                result.MoonPos = result.SunPos;
                result.MoonPos.x = (result.MoonPos.x + 180) % 360;

                if (sunLoop is > 0.01f and < 0.99f)
                {
                    result.LightRotation = quaternion.Euler(result.SunPos * DEG2_RAD);
                    result.LightIntensity = 1.0f - math.abs((sunPosOffset / 110.0f) - 1.0f);
                }
                else
                {
                    result.LightRotation = quaternion.Euler(result.MoonPos * DEG2_RAD);
                    result.LightIntensity = 0.1f;
                }

                Output.Value = result;
            }
        }
    }
}
