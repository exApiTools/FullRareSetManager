using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Helpers;
using FullRareSetManager.Utilities;
using SharpDX;

namespace FullRareSetManager
{
    public class DropAllToInventory
    {
        private const int WHILE_DELAY = 5;
        private readonly FullRareSetManagerCore _plugin;

        public DropAllToInventory(FullRareSetManagerCore plugin)
        {
            _plugin = plugin;
        }

        private GameController GameController => _plugin.GameController;

        public async SyncTask<bool> SwitchToTab(int tabIndex, int? nestedStashIndex, FullRareSetManagerSettings settings)
        {
            var stashElement = GameController.Game.IngameState.IngameUi.StashElement;
            try
            {
                var stashTabToGoTo = stashElement.GetStashInventoryByIndex(tabIndex);

                if (stashTabToGoTo.IsVisible && stashTabToGoTo.NestedVisibleInventoryIndex == nestedStashIndex)
                    return true;
            }
            catch
            {
                // Nothing to see here officer.
            }

            await SwitchToTabViaArrowKeys(tabIndex);
            var visibleStashIndex = stashElement.IndexVisibleStash;
            if (visibleStashIndex != tabIndex)
            {
                DebugWindow.LogError($"Unable to switch tabs: index after switching does not match (expected {tabIndex}, got {visibleStashIndex})");
                return false;
            }

            var clickWindowOffset = GameController.Window.GetWindowRectangleTimeCache.TopLeft.ToVector2Num();
            if (stashElement.VisibleStash.NestedVisibleInventoryIndex is { } currentNestedIndex)
            {
                if (nestedStashIndex is not { } desiredIndex)
                {
                    DebugWindow.LogError("Unable to switch subtabs: desired subtab unknown");
                    return false;
                }

                if (desiredIndex == currentNestedIndex)
                {
                    return true;
                }

                var buttons = stashElement.VisibleStash.NestedTabSwitchBar?.SwitchButtons.OrderBy(x => x.X).ToList();
                if (buttons == null || buttons.Count <= desiredIndex)
                {
                    DebugWindow.LogError($"Unable to switch subtabs: buttons?.Count is '{buttons?.Count}'");
                    return false;
                }

                var clickLocation = buttons[desiredIndex].GetClientRectCache.ClickRandomNum();
                await Mouse.SetCursorPosAndLeftClick(clickLocation + clickWindowOffset, settings.ExtraDelay);
                var indexAfterSwitch = stashElement.VisibleStash.NestedVisibleInventoryIndex;
                if (indexAfterSwitch != desiredIndex)
                {
                    DebugWindow.LogError($"Unable to switch subtabs: index after switching does not match (expected {desiredIndex}, got {indexAfterSwitch})");
                    return false;
                }
            }

            return true;
            // TODO Fix tab switching via mouse
            var latency = (int)GameController.Game.IngameState.ServerData.Latency;

            // We want to maximum wait 20 times the Current Latency before giving up in our while loops.
            var maxNumberOfTries = latency * 20 > 2000 ? latency * 20 / WHILE_DELAY : 2000 / WHILE_DELAY;

            if (tabIndex > 30)
                return await SwitchToTabViaArrowKeys(tabIndex);

            var stashPanel = stashElement;

            try
            {
                var viewAllTabsButton = stashElement.ViewAllStashButton;

                if (stashPanel.IsVisible && !viewAllTabsButton.IsVisible)
                {
                    // The user doesn't have a view all tabs button, eg. 4 tabs.
                    return await SwitchToTabViaArrowKeys(tabIndex);
                }

                var dropDownTabElements = stashElement.ViewAllStashPanel;

                if (!dropDownTabElements.IsVisible)
                {
                    var pos = viewAllTabsButton.GetClientRect();
                    await Mouse.SetCursorPosAndLeftClick(pos.Center.ToVector2Num() + clickWindowOffset, settings.ExtraDelay);

                    //Thread.Sleep(200);
                    await Task.Delay(latency + settings.ExtraDelay);
                    var brCounter = 0;

                    //while (1 == 2 && !dropDownTabElements.IsVisible)
                    //{
                    //    Thread.Sleep(WHILE_DELAY);

                    //    if (brCounter++ <= maxNumberOfTries)
                    //        continue;

                    //    BasePlugin.LogMessage($"1. Error in SwitchToTab: {tabIndex}.", 5);
                    //    return false;
                    //}

                    if (stashElement.TotalStashes > 30)
                    {
                        // TODO:Zafaar implemented something that allows us to get in contact with the ScrollBar.
                        Input.VerticalScroll(true, 5);
                        await Task.Delay(latency + settings.ExtraDelay);
                    }
                }

                // Dropdown menu have the following children: 0, 1, 2.
                // Where:
                // 0 is the icon (fx. chaos orb).
                // 1 is the name of the tab.
                // 2 is the slider.
                var totalStashes = stashElement.TotalStashes;
                var slider = false; // dropDownTabElements.Children[1].ChildCount == totalStashes;
                var noSlider = true; // dropDownTabElements.Children[2].ChildCount == totalStashes;
                RectangleF tabPos;

                if (slider)
                    tabPos = dropDownTabElements.GetChildAtIndex(1).GetChildAtIndex(tabIndex).GetClientRect();
                else if (noSlider)
                    tabPos = dropDownTabElements.GetChildAtIndex(2).GetChildAtIndex(tabIndex).GetClientRect();
                else
                {
                    //BasePlugin.LogError("Couldn't detect slider/non-slider, contact Preaches [Stashie]", 3);
                    return false;
                }

                await Mouse.SetCursorPosAndLeftClick(tabPos.Center.ToVector2Num() + clickWindowOffset, settings.ExtraDelay);
                await Task.Delay(latency + settings.ExtraDelay);
            }
            catch (Exception e)
            {
                DebugWindow.LogError($"Error in GoToTab {tabIndex}: {e}", 5);
                return false;
            }

            Inventory stash;

            var counter = 0;

            do
            {
                await Task.Delay(WHILE_DELAY);
                stash = stashPanel.VisibleStash;

                if (counter++ <= maxNumberOfTries)
                    continue;

                //BasePlugin.LogMessage("2. Error opening stash: " + tabIndex, 5);
                return true;
            } while (stash?.VisibleInventoryItems == null);

            return true;
        }

        private async SyncTask<bool> SwitchToTabViaArrowKeys(int tabIndex)
        {
            var latency = GameController.Game.IngameState.ServerData.Latency;
            var indexOfCurrentVisibleTab = GameController.Game.IngameState.IngameUi.StashElement.IndexVisibleStash; // GetIndexOfCurrentVisibleTab();
            var difference = tabIndex - indexOfCurrentVisibleTab;
            var negative = difference < 0;

            for (var i = 0; i < Math.Abs(difference); i++)
            {
                var key = negative ? Keys.Left : Keys.Right;
                Input.KeyDown(key);
                await Task.Delay(5);
                Input.KeyUp(key);
                await Task.Delay(latency);
            }

            return true;
        }
    }
}