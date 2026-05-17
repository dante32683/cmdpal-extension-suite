using System;
using System.Globalization;

namespace TimeDateDockExtension.Services;

internal static class TimeDateFormatService
{
    public static string FormatTime(DateTime value, SettingsManager settings)
    {
        var customFormat = settings.CustomTimeFormat;
        if (!string.IsNullOrEmpty(customFormat))
        {
            return SafeFormat(value, customFormat, TimeFormat(settings));
        }

        return value.ToString(TimeFormat(settings), CultureInfo.CurrentCulture);
    }

    public static string FormatDate(DateTime value, SettingsManager settings)
    {
        var customFormat = settings.CustomDateFormat;
        if (!string.IsNullOrEmpty(customFormat))
        {
            return SafeFormat(value, customFormat, DateFormat(settings));
        }

        return value.ToString(DateFormat(settings), CultureInfo.CurrentCulture);
    }

    public static string TimeFormat(SettingsManager settings)
    {
        var hour = settings.TimeUses24Hour
            ? settings.TimeLeadingZero ? "HH" : "H"
            : settings.TimeLeadingZero ? "hh" : "h";

        var format = settings.TimeShowSeconds ? $"{hour}:mm:ss" : $"{hour}:mm";
        return settings.TimeUses24Hour ? format : $"{format} tt";
    }

    public static string DateFormat(SettingsManager settings)
    {
        var month = settings.DateMonthLeadingZero ? "MM" : "M";
        var day = settings.DateDayLeadingZero ? "dd" : "d";
        var year = settings.DateFourDigitYear ? "yyyy" : "yy";

        return settings.DateOrder switch
        {
            "dmy" => $"{day}/{month}/{year}",
            "ymd" => $"{year}-{month}-{day}",
            _ => $"{month}/{day}/{year}",
        };
    }

    private static string SafeFormat(DateTime value, string requestedFormat, string fallbackFormat)
    {
        try
        {
            return value.ToString(requestedFormat, CultureInfo.CurrentCulture);
        }
        catch (FormatException)
        {
            return value.ToString(fallbackFormat, CultureInfo.CurrentCulture);
        }
    }
}
