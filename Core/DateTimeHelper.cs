using System;
using System.Globalization;

namespace MESInsight.Core
{
    public static class DateTimeHelper
    {
        internal static readonly string[] TimestampFormats =
        {
            "dd.MM.yyyy HH:mm:ss.ffff",
            "dd.MM.yyyy HH:mm:ss.fff",
            "dd.MM.yyyy HH:mm:ss",
            "dd/MM/yyyy HH:mm:ss.ffff",
            "dd/MM/yyyy HH:mm:ss.fff",
            "dd/MM/yyyy HH:mm:ss",
            "yyyy-MM-dd HH:mm:ss.ffff",
            "yyyy-MM-dd HH:mm:ss.fff",
            "yyyy-MM-dd HH:mm:ss.ff",
            "yyyy-MM-dd HH:mm:ss.f",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss.fff",
            "yyyy-MM-ddTHH:mm:ss.ff",
            "yyyy-MM-ddTHH:mm:ss.f",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-dd HH:mm:ss fff",
        };

        public static bool TryParseTimestamp(string value, out DateTime result)
        {
            return DateTime.TryParseExact(
                value?.Trim() ?? "",
                TimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result);
        }
    }
}