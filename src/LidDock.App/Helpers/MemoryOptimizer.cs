using System;
using LidDock.Windows.Native;

namespace LidDock.App.Helpers;

public static class memoryOptimizer
{
    public static void trimWorkingSet()
    {
        try
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
            GC.WaitForPendingFinalizers();
            var handle = nativeMethods.getCurrentProcess();
            if (handle != IntPtr.Zero)
            {
                nativeMethods.setProcessWorkingSetSize(handle, (IntPtr)(-1), (IntPtr)(-1));
            }
        }
        catch
        {
        }
    }
}
