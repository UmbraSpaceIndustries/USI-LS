using System;

namespace LifeSupport
{
    public static class LifeSupportUtilities
    {
        public static double SecondsPerMonth()
        {
            const double secsPerMinute = 60d;
            const double secsPerHour = secsPerMinute * 60d;
            double secsPerDay = GameSettings.KERBIN_TIME ? secsPerHour * 6d : secsPerHour * 24d;
            // Our standard is 30 day months.
            return GameSettings.KERBIN_TIME ? secsPerDay * 30d : secsPerDay * 30.4d;
        }

        public static string SecondsToKerbinTime(double tSnack, bool shortTime = false)
        {
            const double secsPerMinute = 60d;
            const double secsPerHour = secsPerMinute * 60d;
            double secsPerDay = GameSettings.KERBIN_TIME ? secsPerHour * 6d : secsPerHour * 24d;
            double secsPerYear = GameSettings.KERBIN_TIME ? secsPerDay * 425d : secsPerDay * 365d;
            double s = Math.Abs(tSnack);

            double y = Math.Floor(s / secsPerYear);
            s = s - (y * secsPerYear);
            if (y > 100)
            {
                y = 100;
                s = 0;
            }
            double d = Math.Floor(s / secsPerDay);
            s = s - (d * secsPerDay);
            double h = Math.Floor(s / secsPerHour);
            s = s - (h * secsPerHour);
            double m = Math.Floor(s / secsPerMinute);
            s = s - (m * secsPerMinute);

            var sign = "";
            if (tSnack < 0)
                sign = "-";
            if (shortTime)
                return string.Format("{0}{1:0}y:{2:0}d", sign, y, d);
            else
                return string.Format("{0}{1:0}y:{2:0}d:{3:00}h:{4:00}m:{5:00}s", sign, y, d, h, m, s);
        }        
    }
}