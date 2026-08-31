using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AlMustaqbal
{
    internal sealed partial class MainForm
    {
        private IntPtr FindLegacyMainWindow(int pid)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = 0;
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if (p != (uint)pid || !IsWindowVisible(h)) return true;
                string title = GetText(h).Trim();
                if (title.Length == 0 || ContainsDemoText(title) || IsLegacyDatabaseDialog(title)) return true;
                RECT r;
                if (!GetWindowRect(h, out r)) return true;
                int w = Math.Max(0, r.Right - r.Left);
                int ht = Math.Max(0, r.Bottom - r.Top);
                long area = (long)w * ht;
                if (w < 760 || ht < 480 || area < 420000) return true;
                if (area > bestArea) { bestArea = area; best = h; }
                return true;
            }, IntPtr.Zero);
            return best;
        }

        private void HideLegacySplash(int pid)
        {
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if (p != (uint)pid || !IsWindowVisible(h)) return true;
                string title = GetText(h).Trim();
                if (title.Length != 0) return true;
                RECT r;
                if (!GetWindowRect(h, out r)) return true;
                int w = r.Right - r.Left, ht = r.Bottom - r.Top;
                if (w >= 760 && ht >= 420) ShowWindow(h, SW_HIDE);
                return true;
            }, IntPtr.Zero);
        }

        private void AutoAcceptLegacyDemo(int pid)
        {
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if (p != (uint)pid || !IsWindowVisible(h)) return true;
                EnumChildWindows(h, delegate(IntPtr c, IntPtr lp)
                {
                    string text = GetText(c);
                    if (ContainsDemoText(text) && IsWindowEnabled(c)) SendMessage(c, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
        }

        private void AutoConnectLegacyDatabase(int pid)
        {
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if (p != (uint)pid || !IsWindowVisible(h)) return true;
                string title = GetText(h);
                if (!IsLegacyDatabaseDialog(title)) return true;
                SetWindowText(h, "الاتصال ببيانات المستقبل");
                EnumChildWindows(h, delegate(IntPtr c, IntPtr lp)
                {
                    string text = GetText(c).Trim();
                    if (IsWindowEnabled(c) &&
                        (string.Equals(text, "اتصال", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(text, "Connect", StringComparison.OrdinalIgnoreCase)))
                        SendMessage(c, BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }, IntPtr.Zero);
                return true;
            }, IntPtr.Zero);
        }

        private void RetitleProcessWindows(int pid)
        {
            EnumWindows(delegate(IntPtr h, IntPtr l)
            {
                uint p;
                GetWindowThreadProcessId(h, out p);
                if (p != (uint)pid || !IsWindowVisible(h)) return true;
                string t = GetText(h);
                if (IsLegacyDatabaseDialog(t)) SetWindowText(h, "الاتصال ببيانات المستقبل");
                else if (!ContainsDemoText(t))
                {
                    if (t.IndexOf("Mizan", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        t.IndexOf("ميزان", StringComparison.OrdinalIgnoreCase) >= 0)
                        SetWindowText(h, "المستقبل - نظام إدارة الأعمال");
                    SendMessage(h, WM_SETICON, new IntPtr(ICON_SMALL), Icon.Handle);
                    SendMessage(h, WM_SETICON, new IntPtr(ICON_BIG), Icon.Handle);
                }
                return true;
            }, IntPtr.Zero);
        }

        private static bool IsLegacyDatabaseDialog(string title)
        {
            title = title ?? "";
            return title.IndexOf("اتصال قاعدة بيانات", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf("اتصال بقاعدة", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   title.IndexOf("Database", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static bool ContainsDemoText(string text)
        {
            text = text ?? "";
            return text.IndexOf("نسخة عرض", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("تشغيل كنسخة", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Demo", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private static string GetText(IntPtr h)
        {
            int n = GetWindowTextLength(h);
            StringBuilder b = new StringBuilder(Math.Max(2, n + 2));
            GetWindowText(h, b, b.Capacity);
            return b.ToString();
        }

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        private const int GWL_STYLE = -16;
        private const long WS_CAPTION = 0x00C00000L;
        private const long WS_THICKFRAME = 0x00040000L;
        private const long WS_MINIMIZEBOX = 0x00020000L;
        private const long WS_MAXIMIZEBOX = 0x00010000L;
        private const long WS_POPUP = 0x80000000L;
        private const long WS_CHILD = 0x40000000L;
        private const long WS_VISIBLE = 0x10000000L;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;
        private const int SW_HIDE = 0;
        private static readonly IntPtr HWND_TOP = IntPtr.Zero;
        private static readonly IntPtr HWND_BOTTOM = new IntPtr(1);
        private const int WM_SETICON = 0x0080;
        private const int ICON_SMALL = 0;
        private const int ICON_BIG = 1;
        private const int BM_CLICK = 0x00F5;

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeInformation(string rootPathName, StringBuilder volumeNameBuffer, int volumeNameSize,
            out uint volumeSerialNumber, out uint maximumComponentLength, out uint fileSystemFlags,
            StringBuilder fileSystemNameBuffer, int nFileSystemNameSize);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowEnabled(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        [DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SetWindowText(IntPtr hWnd, string lpString);
        [DllImport("user32.dll")]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint flags);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }
        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }
        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    }
}
