using Newtonsoft.Json;
using System;

namespace Vin2Api
{
    public static class Externsions
    {
        public static string SerializeObject(this object obj)
        {
            return JsonConvert.SerializeObject(obj);
        }

        public static T DeserializeObject<T>(this string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }

        public static T DeserializeAnonymousType<T>(this string json, T template)
        {
            return JsonConvert.DeserializeAnonymousType(json, template);
        }

        public static DateTime JsUtcToCsLocal(this long time)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0).AddMilliseconds(time).ToLocalTime();
        }

        public static long CsLocalToJsUtc(this DateTime time)
        {
            return (time.ToUniversalTime().Ticks - new DateTime(1970, 1, 1, 0, 0, 0).Ticks) / 10000;
        }
    }
}
