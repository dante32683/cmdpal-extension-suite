// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NPUToolsExtension;

internal sealed partial class NPUToolsExtensionPage : ListPage
{
    public NPUToolsExtensionPage()
    {
        Id = "com.local.nputools.status";
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "NPU Tools";
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        return [
            new ListItem(new NoOpCommand()) { Title = "Original NPU Tools scaffold retained as reference" }
        ];
    }
}
