using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Shmuelie.WinRTServer.CsWinRT;

namespace TimeDateDockExtension;

public class Program
{
    [MTAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            global::Shmuelie.WinRTServer.ComServer server = new();

            ManualResetEvent extensionDisposedEvent = new(false);
            TimeDateDockExtension extensionInstance = new(extensionDisposedEvent);
            server.RegisterClass<TimeDateDockExtension, IExtension>(() => extensionInstance);
            server.Start();
            extensionDisposedEvent.WaitOne();
            server.Stop();
            server.UnsafeDispose();
        }
        else
        {
            Console.WriteLine("Not being launched as an extension... exiting.");
        }
    }
}
