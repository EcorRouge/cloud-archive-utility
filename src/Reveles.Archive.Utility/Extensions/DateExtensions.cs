using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Reveles.Archive.Utility.Extensions
{
    public static class DateExtensions
    {
        public static readonly DateTime EPOCH = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static ulong ToUnixTime(this DateTime time)
        {
            return (ulong)(((time.ToUniversalTime() - EPOCH).TotalSeconds) * 1000.0);
        }
    }
}
