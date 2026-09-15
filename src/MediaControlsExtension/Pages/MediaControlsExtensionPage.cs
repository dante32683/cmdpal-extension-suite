// ------------------------------------------------------------
//
// Copyright (c) Jiří Polášek. All rights reserved.
//
// ------------------------------------------------------------

namespace JPSoftworks.MediaControlsExtension.Pages;

internal sealed partial class MediaControlsExtensionPage : ListPage, IDisposable
{
    private readonly SettingsManager _settingsManager;
    private readonly YetAnotherHelper _yetAnotherHelper;
    private readonly MediaService _mediaService;
    private readonly Lock _refreshLock = new();
    private readonly bool _isBandPage;
    private bool _disposed;

    private bool _isInitialized;
    private readonly NowPlayingListItem _playPauseCurrentSessionItem;
    private readonly DockHeadItem? _bandFirstItem;
    private readonly ListItem _nextTrackCurrentSessionItem;
    private readonly ListItem _prevTrackCurrentSessionItem;
    private readonly ListItem _muteCommandItem;
    private List<MediaSourceListItem> _items = [];
    private IListItem[] _cachedItems = [];

    public MediaControlsExtensionPage(
        MediaService mediaService,
        SettingsManager settingsManager,
        YetAnotherHelper yetAnotherHelper,
        bool asBandPage = false)
    {
        ArgumentNullException.ThrowIfNull(mediaService);
        ArgumentNullException.ThrowIfNull(settingsManager);
        ArgumentNullException.ThrowIfNull(yetAnotherHelper);

        this._isBandPage = asBandPage;
        this._settingsManager = settingsManager;
        this._yetAnotherHelper = yetAnotherHelper;
        this._mediaService = mediaService;

        this.Icon = Icons.MainIcon;
        this.Title = Strings.Name!;
        this.Name = Strings.Open!;
        // Keep the original ID on the dock band so existing pins continue to resolve.
        // The ordinary page needs a distinct ID or CmdPal treats it as a duplicate band.
        this.Id = this._isBandPage
            ? "com.dziad.mediacontrolsextension"
            : "com.dziad.mediacontrolsextension.page";
        this.PlaceholderText = Strings.SearchPlaceholder!;

        this._mediaService.Initialized += this.MediaServiceOnInitialized;
        this._mediaService.MediaSourcesChanged += this.MediaServiceOnMediaSourcesChanged;
        this._mediaService.LoadingStatusChanged += this.MediaServiceOnLoadingStatusChanged;
        this._settingsManager.Settings.SettingsChanged += this.SettingsOnSettingsChanged;

        this.EmptyContent = new CommandItem
        {
            Title = Strings.EmptyContent_Title!,
            Subtitle = Strings.EmptyContent_Subtitle!,
            Icon = Icons.MainIcon
        };

        this._playPauseCurrentSessionItem = new NowPlayingListItem(this._mediaService, this._settingsManager, this._yetAnotherHelper, this._isBandPage);
        // DockHeadItem subscribes to CurrentMediaSourceChanged in its constructor.
        // The page's own subscription is added below so DockHeadItem's handler fires first,
        // updating Title/Subtitle before RaiseItemsChanged() reads them.
        this._bandFirstItem = this._isBandPage
            ? new DockHeadItem(this._mediaService, this._settingsManager, this._yetAnotherHelper)
            : null;
        this._nextTrackCurrentSessionItem = new(new MediaCurrentSessionCommand(this._mediaService, MediaSessionOperations.SkipNextTrack, this._yetAnotherHelper)) { Title = Strings.Command_NextTrack, Subtitle = Strings.Command_NextTrack_Subtitle, Icon = Icons.SkipNextTrack };
        this._prevTrackCurrentSessionItem = new(new MediaCurrentSessionCommand(this._mediaService, MediaSessionOperations.SkipPreviousTrack, this._yetAnotherHelper)) { Title = Strings.Command_PreviousTrack, Subtitle = Strings.Command_PreviousTrack_Subtitle, Icon = Icons.SkipPreviousTrack };
        this._muteCommandItem = new(new ToggleMuteMediaInvokableCommand(this._yetAnotherHelper));

        if (this._isBandPage)
        {
            this._playPauseCurrentSessionItem.Title = string.Empty;
            this._playPauseCurrentSessionItem.Subtitle = string.Empty;
            this._nextTrackCurrentSessionItem.Title = string.Empty;
            this._nextTrackCurrentSessionItem.Subtitle = string.Empty;
            this._prevTrackCurrentSessionItem.Title = string.Empty;
            this._prevTrackCurrentSessionItem.Subtitle = string.Empty;
            this._muteCommandItem.Title = string.Empty;
            this._muteCommandItem.Subtitle = string.Empty;
        }

        // Subscribe after items are constructed so DockHeadItem's handler (registered in its
        // constructor above) runs first and Title/Subtitle are current when RaiseItemsChanged fires.
        this._mediaService.CurrentMediaSourceChanged += this.MediaServiceOnCurrentMediaSourceChanged;
        this._mediaService.CurrentMediaPlaybackChanged += this.MediaServiceOnCurrentMediaPlaybackChanged;
    }

    private void MediaServiceOnInitialized(object? sender, EventArgs e)
    {
        if (_disposed) return;
        _isInitialized = true;
        RebuildAndRaiseIfChanged();
    }

    private void MediaServiceOnMediaSourcesChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        List<MediaSourceListItem> newItems = [.. _mediaService.Sources.Select(mediaSource => new MediaSourceListItem(_mediaService, mediaSource, _settingsManager, _yetAnotherHelper, _isBandPage))];
        MediaSourceListItem[] oldItems;
        lock (_refreshLock)
        {
            oldItems = [.. _items];
            _items = newItems;
        }
        RebuildAndRaiseIfChanged();
        _ = Task.Run(() => DisposeItems(oldItems));
    }

    private void MediaServiceOnLoadingStatusChanged(object? sender, EventArgs e)
    {
        if (!_disposed) IsLoading = _mediaService.IsLoading;
    }

    private void MediaServiceOnCurrentMediaSourceChanged(object? sender, MediaSource? e)
    {
        if (!_disposed) UpdateCurrentMediaItems();
    }

    private void MediaServiceOnCurrentMediaPlaybackChanged(object? sender, EventArgs e)
    {
        if (!_disposed) UpdateCurrentMediaItems();
    }

    private static void DisposeItems(IEnumerable<MediaSourceListItem> items)
    {
        foreach (var item in items)
        {
            try { item.Dispose(); }
            catch (Exception ex) { Logger.LogError(ex); }
        }
    }

    private void UpdateCurrentMediaItems()
    {
        if (this._nextTrackCurrentSessionItem?.Command is MediaCurrentSessionCommand nextTrackCommand)
        {
            this._nextTrackCurrentSessionItem.UpdateIcon(nextTrackCommand.CanExecute() ? Icons.SkipNextTrack : Icons.SkipNextTrackDisabled);
        }
        if (this._prevTrackCurrentSessionItem?.Command is MediaCurrentSessionCommand prevTrackCommand)
        {
            this._prevTrackCurrentSessionItem.UpdateIcon(prevTrackCommand.CanExecute() ? Icons.SkipPreviousTrack : Icons.SkipPreviousTrackDisabled);
        }

        this.RebuildAndRaiseIfChanged(forceRaise: this._isBandPage);
    }

    private void SettingsOnSettingsChanged(object sender, Settings args)
    {
        this.RebuildAndRaiseIfChanged();
    }

    /// <summary>
    /// Rebuilds the items list and raises <see cref="RaiseItemsChanged"/> only when
    /// the item composition (identity or order) actually changed.
    /// </summary>
    private void RebuildAndRaiseIfChanged(bool forceRaise = false)
    {
        lock (this._refreshLock)
        {
            var newItems = this.BuildItems();
            if (!forceRaise && ItemsEqual(this._cachedItems, newItems))
            {
                return;
            }

            this._cachedItems = newItems;
        }

        this.RaiseItemsChanged();
    }

    private IListItem[] BuildItems()
    {
        if (this._isBandPage)
        {
            return [.. this.GetBandItems()];
        }

        if (!this._isInitialized)
        {
            this.IsLoading = true;
            return [.. this.GetGlobalCommands()];
        }

        return [.. this.GetGlobalCommands(), .. this._items];
    }

    public override IListItem[] GetItems()
    {
        lock (this._refreshLock)
        {
            return this._cachedItems;
        }
    }

    private List<IListItem> GetGlobalCommands()
    {
        List<IListItem> items = [];

        if (this._playPauseCurrentSessionItem != null)
        {
            items.Add(this._playPauseCurrentSessionItem);
        }

        if (this._settingsManager.ShowSkipCommands)
        {
            items.Add(this._nextTrackCurrentSessionItem!);
            items.Add(this._prevTrackCurrentSessionItem!);
        }

        items.Add(this._muteCommandItem!);

        return items;
    }

    private List<IListItem> GetBandItems()
    {
        if (!this._isBandPage || this._bandFirstItem is null)
        {
            return [];
        }

        List<IListItem> items = [];

        items.Add(this._bandFirstItem);

        if (this._mediaService.CurrentSource is not null)
        {
            if (this._settingsManager.ShowSkipCommandsInDockBand)
            {
                items.Add(this._prevTrackCurrentSessionItem!);
            }
            items.Add(this._playPauseCurrentSessionItem);
            if (this._settingsManager.ShowSkipCommandsInDockBand)
            {
                items.Add(this._nextTrackCurrentSessionItem!);
            }
        }
        return items;
    }

    private static bool ItemsEqual(IListItem[] a, IListItem[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        for (int i = 0; i < a.Length; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
            {
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        this._mediaService.Initialized -= this.MediaServiceOnInitialized;
        this._mediaService.MediaSourcesChanged -= this.MediaServiceOnMediaSourcesChanged;
        this._mediaService.LoadingStatusChanged -= this.MediaServiceOnLoadingStatusChanged;
        this._mediaService.CurrentMediaSourceChanged -= this.MediaServiceOnCurrentMediaSourceChanged;
        this._mediaService.CurrentMediaPlaybackChanged -= this.MediaServiceOnCurrentMediaPlaybackChanged;
        this._settingsManager.Settings.SettingsChanged -= this.SettingsOnSettingsChanged;
        MediaSourceListItem[] items;
        lock (_refreshLock)
        {
            items = [.. _items];
            _items.Clear();
        }
        DisposeItems(items);
        this._playPauseCurrentSessionItem?.Dispose();
        this._bandFirstItem?.Dispose();
    }
}
