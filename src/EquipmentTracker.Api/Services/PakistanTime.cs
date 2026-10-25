namespace EquipmentTracker.Api.Services;

// "Today" and dates shown to users are in Pakistan time (AD-7); the database stores UTC.
public static class PakistanTime
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public static DateOnly ToDate(DateTime utc) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone));

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Pakistan Standard Time"); } // Windows
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Karachi"); } // Linux/macOS
    }
}
