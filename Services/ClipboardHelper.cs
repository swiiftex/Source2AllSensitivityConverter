using System.Runtime.InteropServices;
using System.Text;

namespace Source2AllSensitivityConverter.Services;

/// <summary>
/// Sets clipboard text via the Win32 API directly. WPF's <c>Clipboard.SetText</c> retries internally
/// for ~1 second on the UI thread when another process (clipboard managers, Parsec/RDP, etc.) holds
/// the clipboard open — which freezes the app and often still throws after the data has landed. This
/// makes a few quick attempts and fails fast instead, so the UI never hangs.
/// </summary>
public static class ClipboardHelper
{
    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    [DllImport("user32.dll", SetLastError = true)] private static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool CloseClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EmptyClipboard();
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr GlobalFree(IntPtr hMem);

    public static bool TrySetText(string text)
    {
        text ??= string.Empty;

        for (var attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                var hGlobal = IntPtr.Zero;
                try
                {
                    EmptyClipboard();

                    var bytes = Encoding.Unicode.GetBytes(text + '\0');
                    hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes.Length);
                    if (hGlobal == IntPtr.Zero) return false;

                    var ptr = GlobalLock(hGlobal);
                    if (ptr == IntPtr.Zero) return false;
                    Marshal.Copy(bytes, 0, ptr, bytes.Length);
                    GlobalUnlock(hGlobal);

                    if (SetClipboardData(CF_UNICODETEXT, hGlobal) != IntPtr.Zero)
                    {
                        hGlobal = IntPtr.Zero; // the system owns the memory now — don't free it
                        return true;
                    }
                    return false;
                }
                finally
                {
                    if (hGlobal != IntPtr.Zero) GlobalFree(hGlobal);
                    CloseClipboard();
                }
            }
            Thread.Sleep(10); // clipboard briefly locked by another app; retry shortly
        }
        return false;
    }
}
