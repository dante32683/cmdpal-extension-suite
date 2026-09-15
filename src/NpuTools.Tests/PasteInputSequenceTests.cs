using NpuTools.Clipboard.Interop;
using NpuTools.Clipboard.Services;
using Xunit;

namespace NpuTools.Tests;

public sealed class PasteInputSequenceTests
{
    [Fact]
    public void Create_ReleasesChordModifiersBeforeSendingControlV()
    {
        User32.INPUT[] inputs = PasteInputSequence.Create();

        Assert.Equal(12, inputs.Length);
        Assert.All(inputs.Take(8), input => Assert.Equal(User32.KEYEVENTF_KEYUP, input.U.ki.dwFlags));
        Assert.Equal(User32.VK_CONTROL, inputs[8].U.ki.wVk);
        Assert.Equal(User32.VK_V, inputs[9].U.ki.wVk);
        Assert.Equal(User32.VK_V, inputs[10].U.ki.wVk);
        Assert.Equal(User32.KEYEVENTF_KEYUP, inputs[10].U.ki.dwFlags);
        Assert.Equal(User32.VK_CONTROL, inputs[11].U.ki.wVk);
        Assert.Equal(User32.KEYEVENTF_KEYUP, inputs[11].U.ki.dwFlags);
    }

    [Fact]
    public void Create_MarksEveryInputForThePowerToysKeyboardHook()
    {
        User32.INPUT[] inputs = PasteInputSequence.Create();

        Assert.All(inputs, input => Assert.Equal(PasteInputSequence.PowerToysInjectedInputMarker, input.U.ki.dwExtraInfo));
    }
}
