using System;
using System.Collections;
using Unity.VisualScripting.Dependencies.Sqlite;
using UnityEngine;
using UnityEngine.Serialization;

public class TimeOfDay : MonoBehaviour
{
    [FormerlySerializedAs("longitude_Sun")]
    [SerializeField]
    float fLongitude_Sun;

    [FormerlySerializedAs("latitude_Sun")]
    [SerializeField]
    float fLatitude_Sun;

    private Vector4 vSunPos;
    private Vector4 vMoonPos;

    [SerializeField]
    private Vector4 vLightDir;

    [FormerlySerializedAs("longitude_Moon")]
    [SerializeField]
    float fLongitude_Moon;

    [FormerlySerializedAs("latitude_Moon")]
    [SerializeField]
    float fLatitude_Moon;

    [SerializeField]
    [Range(0, 24)]
    int hour;

    [SerializeField]
    [Range(0, 60)]
    int minutes;

    DateTime time;
    Light light;

    [SerializeField]
    float timeSpeed = 1;

    [SerializeField]
    int frameSteps = 1;
    int frameStep;

    [SerializeField]
    DateTime date;

    public void SetTime(int hour, int minutes)
    {
        this.hour = hour;
        this.minutes = minutes;
        OnValidate();
    }

    public void SetLocation(float longitude, float latitude)
    {
        this.fLongitude_Sun = longitude;
        this.fLatitude_Sun = latitude;
    }

    public void SetDate(DateTime dateTime)
    {
        this.hour = dateTime.Hour;
        this.minutes = dateTime.Minute;
        this.date = dateTime.Date;
        OnValidate();
    }

    public void SetUpdateSteps(int i)
    {
        frameSteps = i;
    }

    public void SetTimeSpeed(float speed)
    {
        timeSpeed = speed;
    }

    private void Awake()
    {
        light = GetComponent<Light>();

        time = DateTime.Now;
        hour = time.Hour;
        minutes = time.Minute;
        date = time.Date;
    }

    private void OnValidate()
    {
        time = date + new TimeSpan(hour, minutes, 0);
        Debug.Log(time);
    }

    IEnumerator Start()
    {
        Debug.Log("Start1");
        yield return new WaitForSeconds(2.5f);
        Debug.Log("Start2");
    }

    public void Update()
    {
        time = time.AddSeconds(timeSpeed * Time.deltaTime);
        if (frameStep==0)
        {
            SetLightPosition();
        }
        frameStep = (frameStep + 1) % frameSteps;
    }

    void SetLightPosition()
    {
        double alt;
        double azi;
        SunPosition.CalculateSunPosition(time, (double)fLatitude_Sun, (double)fLongitude_Sun, out azi, out alt);
        vSunPos.x = (float)alt * Mathf.Rad2Deg;
        vSunPos.y = (float)azi * Mathf.Rad2Deg;

        float sunPosOffset = vSunPos.x + 20.0f;
        float sunLoop = Mathf.InverseLerp(0.0f, 220.0f, sunPosOffset);
        if (sunLoop > 0.01f && sunLoop < 0.99f )
        {
            light.transform.localRotation = Quaternion.Euler(vSunPos);
            light.intensity = 1.0f - (Mathf.Abs((sunPosOffset / 110.0f) - 1.0f));
        }
        else
        {
            vMoonPos = vSunPos;
            vMoonPos.x = (vMoonPos.x + 180) % 360;
            light.transform.localRotation = Quaternion.Euler(vMoonPos);
            light.intensity = 0.1f;
        }
    }

    public float GetLightIntensity()
    {
        float sunPosOffset = vSunPos.x + 20.0f;
        return Mathf.Min(1.0f, (1.0f - (Mathf.Abs((sunPosOffset / 110.0f) - 1.0f))) * 2.0f);
    }
    public Vector3 GetSunPosLocal()
    {
        return vSunPos;
    }

    public Vector3 GetSunPosition()
    {
        Vector3 dir = new Vector3(0.0f, 0.0f, -1.0f);
        return Quaternion.Euler(vSunPos) * dir;
    }

    public Vector3 GetMoonPosition()
    {
        Vector3 dir = new Vector3(0.0f, 0.0f, 1.0f);
        return Quaternion.Euler(vMoonPos) * dir;
    }

    public Vector4 GetLightDirection()
    {
        Vector3 dir = new Vector3(0.0f, 0.0f, -1.0f);
        return Quaternion.Euler(vSunPos) * dir;
    }
}

/*
 * The following source came from this blog:
 * http://guideving.blogspot.co.uk/2010/08/sun-position-in-c.html
 */
public static class SunPosition
{
    private const double Deg2Rad = Math.PI / 180.0;
    private const double Rad2Deg = 180.0 / Math.PI;

    /*!
     * \brief Calculates the sun light.
     *
     * CalcSunPosition calculates the suns "position" based on a
     * given date and time in local time, latitude and longitude
     * expressed in decimal degrees. It is based on the method
     * found here:
     * http://www.astro.uio.no/~bgranslo/aares/calculate.html
     * The calculation is only satisfiably correct for dates in
     * the range March 1 1900 to February 28 2100.
     * \param dateTime Time and date in local time.
     * \param latitude Latitude expressed in decimal degrees.
     * \param longitude Longitude expressed in decimal degrees.
     */
    public static void CalculateSunPosition(DateTime dateTime, double latitude, double longitude, out double outAzimuth, out double outAltitude)
    {
        // Convert to UTC
        dateTime = dateTime.ToUniversalTime();

        // Number of days from J2000.0.
        double julianDate = 367 * dateTime.Year -
            (int)((7.0 / 4.0) * (dateTime.Year +
            (int)((dateTime.Month + 9.0) / 12.0))) +
            (int)((275.0 * dateTime.Month) / 9.0) +
            dateTime.Day - 730531.5;

        double julianCenturies = julianDate / 36525.0;

        // Sidereal Time
        double siderealTimeHours = 6.6974 + 2400.0513 * julianCenturies;

        double siderealTimeUT = siderealTimeHours + (366.2422 / 365.2422) * (double)dateTime.TimeOfDay.TotalHours;

        double siderealTime = siderealTimeUT * 15 + longitude;

        // Refine to number of days (fractional) to specific time.
        julianDate += (double)dateTime.TimeOfDay.TotalHours / 24.0;
        julianCenturies = julianDate / 36525.0;

        // Solar Coordinates
        double meanLongitude = CorrectAngle(Deg2Rad * (280.466 + 36000.77 * julianCenturies));

        double meanAnomaly = CorrectAngle(Deg2Rad * (357.529 + 35999.05 * julianCenturies));

        double equationOfCenter = Deg2Rad * ((1.915 - 0.005 * julianCenturies) * Math.Sin(meanAnomaly) + 0.02 * Math.Sin(2 * meanAnomaly));

        double elipticalLongitude = CorrectAngle(meanLongitude + equationOfCenter);

        double obliquity = (23.439 - 0.013 * julianCenturies) * Deg2Rad;

        // Right Ascension
        double rightAscension = Math.Atan2(Math.Cos(obliquity) * Math.Sin(elipticalLongitude), Math.Cos(elipticalLongitude));
        double declination = Math.Asin(Math.Sin(rightAscension) * Math.Sin(obliquity));

        // Horizontal Coordinates
        double hourAngle = CorrectAngle(siderealTime * Deg2Rad) - rightAscension;

        if (hourAngle > Math.PI)
        {
            hourAngle -= 2 * Math.PI;
        }

        double altitude = Math.Asin(Math.Sin(latitude * Deg2Rad) * Math.Sin(declination) + Math.Cos(latitude * Deg2Rad) * Math.Cos(declination) * Math.Cos(hourAngle));

        // Nominator and denominator for calculating Azimuth
        // angle. Needed to test which quadrant the angle is in.
        double aziNom = -Math.Sin(hourAngle);
        double aziDenom = Math.Tan(declination) * Math.Cos(latitude * Deg2Rad) - Math.Sin(latitude * Deg2Rad) * Math.Cos(hourAngle);
        double azimuth = Math.Atan(aziNom / aziDenom);

        if (aziDenom < 0) // In 2nd or 3rd quadrant
        {
            azimuth += Math.PI;
        }
        else if (aziNom < 0) // In 4th quadrant
        {
            azimuth += 2 * Math.PI;
        }

        outAltitude = altitude;
        outAzimuth = azimuth;
    }

    /*!
    * \brief Corrects an angle.
    *
    * \param angleInRadians An angle expressed in radians.
    * \return An angle in the range 0 to 2*PI.
    */
    private static double CorrectAngle(double angleInRadians)
    {
        if (angleInRadians < 0)
        {
            return 2 * Math.PI - (Math.Abs(angleInRadians) % (2 * Math.PI));
        }
        else if (angleInRadians > 2 * Math.PI)
        {
            return angleInRadians % (2 * Math.PI);
        }
        else
        {
            return angleInRadians;
        }
    }
}
