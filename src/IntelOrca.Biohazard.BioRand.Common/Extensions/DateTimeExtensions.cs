using System;

namespace IntelOrca.Biohazard.BioRand.Common.Extensions
{
    internal static class DateTimeExtensions
    {
        public static long ToUnixTimeSeconds(this DateTime dt)
        {
            var offset = new DateTimeOffset(dt);
            return offset.ToUnixTimeSeconds();
        }

        public static DateTime ToDateTime(this long unixTimeSeconds)
        {
            var offset = DateTimeOffset.FromUnixTimeSeconds(unixTimeSeconds);
            return offset.UtcDateTime;
        }
    }
}
