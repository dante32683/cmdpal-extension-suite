// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.MediaControlsExtension.Pages;

internal static class MediaDisplayText
{
    public static string TitleWithArtist(MediaSource mediaSource)
    {
        if (string.IsNullOrWhiteSpace(mediaSource.Name))
        {
            return string.IsNullOrWhiteSpace(mediaSource.Artist)
                ? mediaSource.ApplicationName ?? string.Empty
                : mediaSource.Artist;
        }

        if (string.IsNullOrWhiteSpace(mediaSource.Artist))
        {
            return mediaSource.Name;
        }

        return $"{mediaSource.Name} - {mediaSource.Artist}";
    }
}
