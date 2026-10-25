using System.Text.Json.Serialization;

namespace LibreKO.Common.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ScheduleRepeat : byte
{
    Daily = 0,
    Weekly = 1,
    Once = 2,
    Monthly = 3,
}
