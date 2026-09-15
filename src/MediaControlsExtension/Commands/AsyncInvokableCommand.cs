// ------------------------------------------------------------
// 
// Copyright (c) Jiří Polášek. All rights reserved.
// 
// ------------------------------------------------------------

namespace JPSoftworks.MediaControlsExtension.Commands;

internal abstract class AsyncInvokableCommand : InvokableCommand
{
    protected virtual ICommandResult Result { get; set; } = CommandResult.Dismiss();

    public override ICommandResult Invoke()
    {
        Logger.LogDebug("Invoking async command " + this.GetType().FullName);
        _ = Task.Run(this.SafeInvokeAsync);
        return this.Result;
    }

    private async Task<ICommandResult> SafeInvokeAsync()
    {
        try
        {
            return await this.InvokeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex);
            return CommandResult.KeepOpen();
        }
    }

    protected abstract Task<ICommandResult> InvokeAsync();
}
