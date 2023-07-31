using System.Numerics;
using System.Threading.Tasks;
using ExileCore;
using ExileCore.Shared;

namespace FullRareSetManager.Utilities
{
    public class Mouse
    {
        public static async SyncTask<bool> SetCursorPosAndLeftClick(Vector2 coords, int extraDelay)
        {
            Input.SetCursorPos(coords);
            await Task.Delay(10 + extraDelay);
            Input.LeftDown();
            await Task.Delay(1);
            Input.LeftUp();
            return true;
        }
    }
}
