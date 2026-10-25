using System;

namespace LibreKO.Domain;

public static class GmPanelText
{
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int DaysShownAsAgo = 30;

    public static string SeenAgo(DateTime? lastSeenUtc, DateTime nowUtc)
    {
        if (lastSeenUtc is not { } seen) return "Offline";
        var gap = nowUtc - seen;
        if (gap.TotalMinutes < 1) return "Seen just now";
        if (gap.TotalMinutes < MinutesPerHour) return $"Seen {(int)gap.TotalMinutes} min ago";
        if (gap.TotalHours < HoursPerDay) return $"Seen {(int)gap.TotalHours} h ago";
        if (gap.TotalDays < DaysShownAsAgo) return (int)gap.TotalDays == 1 ? "Seen yesterday" : $"Seen {(int)gap.TotalDays} days ago";
        return $"Seen {seen.ToLocalTime():yyyy-MM-dd}";
    }

    public static string Presence(bool online, DateTime? lastSeenUtc, DateTime nowUtc) => online ? "Online" : SeenAgo(lastSeenUtc, nowUtc);

    public static string FoundCount(int shown, int total) => total > shown ? $"first {shown} of {total}" : $"{shown} found";

    public static bool TryParseAmount(string text, out long amount) =>
        long.TryParse(text.Trim().Replace(",", ""), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out amount);

    public static string Percent(int chance, int scale) =>
        scale <= 0 ? "0" : (chance * 100.0 / scale).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public static bool TryParsePercent(string text, int scale, out int chance)
    {
        chance = 0;
        string cleaned = text.Trim().TrimEnd('%').Trim().Replace(',', '.');
        if (!double.TryParse(cleaned, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double percent))
            return false;
        int value = (int)Math.Round(percent * scale / 100.0);
        if (value < 1 || value > scale) return false;
        chance = value;
        return true;
    }
}
