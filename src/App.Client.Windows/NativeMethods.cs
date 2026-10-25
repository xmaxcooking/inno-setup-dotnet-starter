using System.Runtime.InteropServices;

namespace App.Client.Windows;

internal static partial class NativeMethods
{
    private const int GwlpHwndParent = -8;

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr setWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static void SetOwner(IntPtr childHwnd, IntPtr ownerHwnd) => setWindowLongPtr(childHwnd, GwlpHwndParent, ownerHwnd);
}