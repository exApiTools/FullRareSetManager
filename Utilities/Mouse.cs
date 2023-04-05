using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;

namespace FullRareSetManager.Utilities
{
    public class Mouse
    {
        public const int MOUSEEVENTF_LEFTDOWN = 0x02;
        public const int MOUSEEVENTF_LEFTUP = 0x04;
        public const int MOUSE_EVENT_WHEEL = 0x800;
        private const int MOVEMENT_DELAY = 10;
        private const int CLICK_DELAY = 1;

        [DllImport("user32.dll")]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        
        public static void SetCursorPosAndLeftClick(Vector2 coords, int extraDelay)
        {
            var posX = (int) coords.X;
            var posY = (int) coords.Y;
            SetCursorPos(posX, posY);
            Thread.Sleep(MOVEMENT_DELAY + extraDelay);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            Thread.Sleep(CLICK_DELAY);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

        public static void VerticalScroll(bool forward, int clicks)
        {
            if (forward)
                mouse_event(MOUSE_EVENT_WHEEL, 0, 0, clicks * 120, 0);
            else
                mouse_event(MOUSE_EVENT_WHEEL, 0, 0, -(clicks * 120), 0);
        }
    }
}
