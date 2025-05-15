using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace XeroNetStandardApp.Helpers;

public static class SessionExtensions
{
    public static void SetObjectAsJson(this ISession session, string key, object value)
        => session.SetString(key, JsonConvert.SerializeObject(value));

    public static T? GetObjectFromJson<T>(this ISession session, string key)
    {
        var json = session.GetString(key);
        return json is null ? default : JsonConvert.DeserializeObject<T>(json);
    }
}
