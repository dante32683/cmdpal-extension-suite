using System;

namespace ActionCenterExtension.Services;

/// <summary>
/// Compensates for a Command Palette dock quirk: the center band has a minimum text area
/// (about 33 DIP) and left-aligns text inside it, so short titles sit left of center while
/// longer ones are centered. Leading spaces move a short title to the middle of that area.
/// Tuned against PowerToys 0.101 (Segoe UI, 12 DIP); a layout change there only shifts the label slightly.
/// </summary>
internal static class DockLabel
{
    private const double FontSize = 12;
    private const double MinTextAreaDip = 33.5;
    private const double SpaceWidthDip = 0.27 * FontSize;

    /// <summary>Returns <paramref name="title"/> with leading spaces so it reads as centered in the dock's minimum text area.</summary>
    public static string PadToCenter(string title)
    {
        var padDip = (MinTextAreaDip - EstimateWidthDip(title)) / 2;
        var spaces = (int)Math.Round(padDip / SpaceWidthDip);
        return spaces > 0 ? new string(' ', spaces) + title : title;
    }

    // Rough Segoe UI advance widths in em; accurate to about 1-2 DIP for short names, which is enough here.
    internal static double EstimateWidthDip(string text)
    {
        var em = 0.0;
        foreach (var c in text)
        {
            em += c switch
            {
                ' ' => 0.27,
                'i' or 'I' or 'j' or 'l' or '.' or ',' or ':' or ';' or '\'' or '|' or '!' => 0.27,
                'f' or 't' or 'r' => 0.34,
                'm' or 'M' or 'w' or 'W' => 0.85,
                _ => 0.56,
            };
        }

        return em * FontSize;
    }
}
