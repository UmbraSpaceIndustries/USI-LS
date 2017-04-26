using System;
using System.Collections.Generic;

namespace LifeSupport
{
    public static class LifeSupportUtilities
    {

        public static double SecondsPerDay()
        {
            return GameSettings.KERBIN_TIME ? 21600d : 86400d;
        }

        public static double SecondsPerYear()
        {
            return GameSettings.KERBIN_TIME ? SecondsPerDay() * 426d : SecondsPerDay() * 365d;
        }

        public static double SecondsPerMonth()
        {
            double secsPerDay = SecondsPerDay();
            // Our standard is 30 day months.
            return GameSettings.KERBIN_TIME ? secsPerDay * 30d : secsPerDay * 30.4d;
        }


        public enum TimeFormatLength { Full, Short, Smart, Compact };

        public static string SmartDurationDisplay(double s)
        {
            return DurationDisplay(s, TimeFormatLength.Smart);
        }

        public static string CompactDurationDisplay(double s)
        {
            return DurationDisplay(s, TimeFormatLength.Compact);
        }

        public static string DurationDisplay(double s, TimeFormatLength length = TimeFormatLength.Full)
        {
            if (s < 0)
                return "-" + DurationDisplay(-s, length);

            const double secsPerMinute = 60d;
            const double secsPerHour = secsPerMinute * 60d;
            double secsPerDay = SecondsPerDay();
            double secsPerYear = SecondsPerYear();

            double y = Math.Floor(s / secsPerYear);
            s = s - (y * secsPerYear);
            double d = Math.Floor(s / secsPerDay);
            s = s - (d * secsPerDay);
            double h = Math.Floor(s / secsPerHour);
            s = s - (h * secsPerHour);
            double m = Math.Floor(s / secsPerMinute);
            s = s - (m * secsPerMinute);

            if (length == TimeFormatLength.Short)
                return string.Format ("{0:0}y:{1:0}d", y, d);
            else if (length == TimeFormatLength.Full)
                return string.Format ("{0:0}y:{1:0}d:{2:00}h:{3:00}m:{4:00}s", y, d, h, m, s);
            else if (length == TimeFormatLength.Smart)
                return SmartDurationFormat(y, d, h, m, s);
            else if (length == TimeFormatLength.Compact)
                return CompactDurationFormat(y, d, h, m, s);
            else
                throw new ArgumentOutOfRangeException();
        }

        private static string SmartDurationFormat(double y, double d, double h, double m, double s)
        {
            if (y > 10d)
            {
                return string.Format("{0:0}y", y);
            }
            else if (y > 0d)
            {
                return string.Format("{0:0}y:{1:0}d", y, d);
            }
            else if (d > 20d)
            {
                return string.Format("{0:0}d", d);
            }
            else if (d > 0d)
            {
                return string.Format("{0:0}d:{1:00}h", d, h);
            }
            else
            {
                return string.Format("{0:00}h:{1:00}m:{2:00}s", h, m, s);
            }
        }

        // Might be slow. Don't call often
        private static string CompactDurationFormat(double y, double d, double h, double m, double s)
        {
            var parts = new List<string>();
            if (y > 0d)
                parts.Add(string.Format("{0:0}y", y));
            if (d > 0d)
                parts.Add(string.Format("{0:0}d", d));
            if (h > 0d)
                parts.Add(string.Format("{0:00}h", h));
            if (m > 0d)
                parts.Add(string.Format("{0:00}m", m));
            if (s > 0d)
                parts.Add(string.Format("{0:00}s", s));
            return string.Join(":", parts.ToArray());
        }

    }
}
