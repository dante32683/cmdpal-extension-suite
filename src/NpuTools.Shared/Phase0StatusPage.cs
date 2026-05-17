using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NpuTools.Common;

internal sealed partial class Phase0StatusPage : ListPage
{
    private readonly ExtensionDescriptor _descriptor;

    public Phase0StatusPage(ExtensionDescriptor descriptor)
    {
        _descriptor = descriptor;
        Id = $"{descriptor.ProviderId}.status";
        Icon = new IconInfo(descriptor.IconGlyph);
        Title = descriptor.DisplayName;
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        var items = new List<IListItem>
        {
            new ListItem(new NoOpCommand()) { Title = "Phase 0 scaffold ready" },
            new ListItem(new NoOpCommand()) { Title = $"Provider: {_descriptor.ProviderId}" },
            new ListItem(new NoOpCommand()) { Title = $"Settings: {NpuPaths.GetSettingsDirectory(_descriptor.SettingsDirectoryName)}" },
        };

        foreach (string feature in _descriptor.PlannedFeatures)
        {
            items.Add(new ListItem(new NoOpCommand()) { Title = feature });
        }

        return items.ToArray();
    }
}
