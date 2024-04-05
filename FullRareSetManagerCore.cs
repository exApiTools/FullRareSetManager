using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using FullRareSetManager.SetParts;
using FullRareSetManager.Utilities;
using ImGuiNET;
using Newtonsoft.Json;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

namespace FullRareSetManager
{
    public class FullRareSetManagerCore : BaseSettingsPlugin<FullRareSetManagerSettings>
    {
        private const int INPUT_DELAY = 15;
        private Inventory _currentOpenedStashTab;
        private CurrentSetInfo _currentSetData;
        private string _drawInfoString = "";
        private DropAllToInventory _inventDrop;
        private BaseSetPart[] _itemSetTypes;
        private StashData _sData;
        private ItemDisplayData[] DisplayData;
        private FRSetManagerPublishInformation FrSetManagerPublishInformation;
        private bool _allowScanTabs = true;
        private readonly Stopwatch _fixStopwatch = new Stopwatch();
        private SyncTask<bool> _routine;

        public override void ReceiveEvent(string eventId, object args)
        {
            if (!Settings.Enable.Value) return;

            if (eventId == "stashie_start_drop_items")
            {
                _fixStopwatch.Restart();
                _allowScanTabs = false;
            }
            else if (eventId == "stashie_stop_drop_items")
            {
                _allowScanTabs = true;
            }
            else if (eventId == "stashie_finish_drop_items_to_stash_tab")
            {
                _fixStopwatch.Restart();
                UpdateStashes();
                UpdatePlayerInventory();
                UpdateItemsSetsInfo();
            }
        }

        public override bool Initialise()
        {
            Input.RegisterKey(Settings.DropToInventoryKey.Value);
            _sData = StashData.Load(this) ?? new StashData();

            _inventDrop = new DropAllToInventory(this);

            DisplayData = new ItemDisplayData[8];

            for (var i = 0; i <= 7; i++)
            {
                DisplayData[i] = new ItemDisplayData();
            }

            UpdateItemsSetsInfo();

            Settings.WeaponTypePriority.SetListValues(new List<string> { "Two handed", "One handed" });

            Settings.CalcByFreeSpace.OnValueChanged += delegate { UpdateItemsSetsInfo(); };

            FrSetManagerPublishInformation = new FRSetManagerPublishInformation();
            //WorldItemsController.OnEntityAdded += args => EntityAdded(args.Entity);
            //WorldItemsController.OnEntityRemoved += args => EntityRemoved(args.Entity);
            //WorldItemsController.OnItemPicked += WorldItemsControllerOnOnItemPicked;
            return true;
        }

        public override void EntityAdded(Entity entity)
        {
            if (!Settings.EnableBorders.Value)
                return;

            if (entity.Type != EntityType.WorldItem)
                return;

            if (!Settings.Enable || GameController.Area.CurrentArea.IsTown ||
                _currentAlerts.ContainsKey(entity))
                return;

            var item = entity?.GetComponent<WorldItem>()?.ItemEntity;

            if (item == null) return;

            var visitResult = ProcessItem(item);

            if (visitResult == null) return;

            if (Settings.IgnoreOneHanded && visitResult.ItemType == StashItemType.OneHanded)
                visitResult = null;

            if (visitResult == null) return;

            if (Settings.SmallWeaponOnly && (visitResult.ItemType == StashItemType.OneHanded ||
                                             visitResult.ItemType == StashItemType.TwoHanded)
                                         && (visitResult.Height > 3 ||
                                             visitResult.Width > 1))
                visitResult = null;

            if (visitResult == null) return;

            var index = (int)visitResult.ItemType;

            if (index > 7)
                index = 0;

            var displData = DisplayData[index];

            _currentAlerts.Add(entity, displData);
        }

        public override void EntityRemoved(Entity entity)
        {
            if (!Settings.EnableBorders.Value)
                return;

            if (entity.Type != EntityType.WorldItem)
                return;

            if (Vector2.Distance(entity.GridPosNum, GameController.Player.GridPosNum) < 10)
            {
                //item picked by player?
                var wi = entity.GetComponent<WorldItem>();
                var filteredItemResult = ProcessItem(wi.ItemEntity);

                if (filteredItemResult == null)
                    return;
                filteredItemResult.BInPlayerInventory = true;
                _sData.PlayerInventory.StashTabItems.Add(filteredItemResult);
                UpdateItemsSetsInfo();
            }

            _currentAlerts.Remove(entity);
            _currentLabels.Remove(entity.Address);
        }

        public override void AreaChange(AreaInstance area)
        {
            _currentLabels.Clear();
            _currentAlerts.Clear();
        }

        public class FRSetManagerPublishInformation
        {
            public int GatheredWeapons { get; set; } = 0;
            public int GatheredHelmets { get; set; } = 0;
            public int GatheredBodyArmors { get; set; } = 0;
            public int GatheredGloves { get; set; } = 0;
            public int GatheredBoots { get; set; } = 0;
            public int GatheredBelts { get; set; } = 0;
            public int GatheredAmulets { get; set; } = 0;
            public int GatheredRings { get; set; } = 0;

            public int WantedSets { get; set; } = 0;
        }

        public override void Render()
        {
            if (_routine == null)
            {
                FrSetManagerPublishInformation.WantedSets = Settings.MaxSets.Value;
                var rareSetData = _itemSetTypes;
                foreach (var itemDisplayData in rareSetData)
                {
                    switch (itemDisplayData.PartName)
                    {
                        case "Weapons":
                            FrSetManagerPublishInformation.GatheredWeapons = itemDisplayData.TotalSetsCount();
                            break;
                        case "Helmets":
                            FrSetManagerPublishInformation.GatheredHelmets = itemDisplayData.TotalSetsCount();
                            break;
                        case "Body Armors":
                            FrSetManagerPublishInformation.GatheredBodyArmors = itemDisplayData.TotalSetsCount();
                            break;
                        case "Gloves":
                            FrSetManagerPublishInformation.GatheredGloves = itemDisplayData.TotalSetsCount();
                            break;
                        case "Boots":
                            FrSetManagerPublishInformation.GatheredBoots = itemDisplayData.TotalSetsCount();
                            break;
                        case "Belts":
                            FrSetManagerPublishInformation.GatheredBelts = itemDisplayData.TotalSetsCount();
                            break;
                        case "Amulets":
                            FrSetManagerPublishInformation.GatheredAmulets = itemDisplayData.TotalSetsCount();
                            break;
                        case "Rings":
                            FrSetManagerPublishInformation.GatheredRings = itemDisplayData.TotalSetsCount();
                            break;
                    }
                }

                PublishEvent("frsm_display_data", FrSetManagerPublishInformation);
                if (!_allowScanTabs)
                {
                    if (_fixStopwatch.ElapsedMilliseconds > 3000)
                        _allowScanTabs = true; //fix for stashie doesn't send the finish drop items event
                    return;
                }

                UpdatePlayerInventory();
                UpdateStashes();
                UpdateItemsSetsInfo();

                DrawSetsInfo();
                RenderLabels();
            }

            if (Settings.DropToInventoryKey.PressedOnce())
            {
                if (_routine == null)
                    _routine = RunHotkeyRoutine();
                else
                {
                    _routine = null;
                    foreach (var key in new Keys[] { Keys.LControlKey , Keys.Left, Keys.Right})
                    {
                        if (Input.IsKeyDown(key))
                        {
                            Input.KeyUp(key);
                        }
                    }
                }
            }

            if (_routine != null)
            {
                _routine.GetAwaiter().PumpEvents();
                if (_routine.GetAwaiter().IsCompleted)
                {
                    var oldRoutine = _routine;
                    _routine = null;
                    oldRoutine.GetAwaiter().GetResult();
                }
            }
        }

        private async SyncTask<bool> RunHotkeyRoutine()
        {
            await SellSetToVendor();
            if (GameController.IngameState.IngameUi.StashElement.IsVisible &&
                GameController.IngameState.IngameUi.InventoryPanel.IsVisible)
            {
                try
                {
                    await DropAllItems();
                }
                catch
                {
                    LogError("There was an error while moving items.", 5);
                }
                finally
                {
                    UpdatePlayerInventory();
                    UpdateItemsSetsInfo();
                }
            }

            return true;
        }

        public async SyncTask<bool> SellSetToVendor(int callCount = 1)
        {
            try
            {
                // define sell window
                SellWindow npcSellWindow = null;

                // Sell to vendor.
                var gameWindow = GameController.Window.GetWindowRectangleTimeCache.TopLeft.ToVector2Num();
                var latency = GameController.Game.IngameState.ServerData.Latency;

                var npcTradingWindow = GameController.Game.IngameState.IngameUi.SellWindow;
                var npcTradingWindowHideout = GameController.Game.IngameState.IngameUi.SellWindowHideout;

                // check which sell window it could be
                if (npcTradingWindow.IsVisible)
                    npcSellWindow = npcTradingWindow;
                if (npcTradingWindowHideout.IsVisible)
                    npcSellWindow = npcTradingWindowHideout;

                if (npcSellWindow == null || !npcSellWindow.IsVisible)
                {
                    // The vendor sell window is not open, but is in memory (it would've went straigth to catch if that wasn't the case).
                    LogMessage("Error: npcTradingWindow is not visible (opened)!", 5);
                }

                var playerOfferItems = npcTradingWindow.YourOffer;
                const int setItemsCount = 9;
                const int uiButtonsCount = 2;

                LogMessage($"Player has put in {playerOfferItems.ChildCount - uiButtonsCount} in the trading window.", 3);

                if (playerOfferItems.ChildCount < setItemsCount + uiButtonsCount)
                {
                    for (var i = 0; i < 8; i++)
                    {
                        var itemType = _itemSetTypes[i];
                        var items = itemType.GetPreparedItems();

                        if (items.Any(item => !item.BInPlayerInventory))
                            continue;

                        Input.KeyDown(Keys.LControlKey);

                        foreach (var item in items)
                        {
                            var foundItem =
                                GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory]
                                    .VisibleInventoryItems.FirstOrDefault(x => x.InventPosX == item.InventPosX && x.InventPosY == item.InventPosY);

                            if (foundItem == null)
                            {
                                LogError("FoundItem was null.", 3);
                                return true;
                            }

                            await Task.Delay(INPUT_DELAY);

                            await Mouse.SetCursorPosAndLeftClick(foundItem.GetClientRectCache.Center.ToVector2Num() + gameWindow,
                                Settings.ExtraDelay);

                            await Task.Delay(latency + Settings.ExtraDelay);
                        }
                    }

                    Input.KeyUp(Keys.LControlKey);
                }

                await Task.Delay(INPUT_DELAY + Settings.ExtraDelay.Value);
                var npcOfferItems = npcTradingWindow.OtherOffer;

                foreach (var element in npcOfferItems.Children)
                {
                    var item = element.AsObject<NormalInventoryItem>().Item;

                    if (string.IsNullOrEmpty(item.Metadata))
                        continue;

                    var itemName = GameController.Files.BaseItemTypes.Translate(item.Metadata).BaseName;
                    if (itemName == "Chaos Orb" || itemName == "Regal Orb") continue;
                    LogMessage($"Npc offered '{itemName}'", 3);
                    if (callCount >= 5) return true;
                    var delay = INPUT_DELAY + Settings.ExtraDelay.Value;
                    LogMessage($"Trying to sell set again in {delay} ms.", 3);
                    await Task.Delay(delay);

                    //SellSetToVendor(callCount++);

                    return true;
                }

                await Task.Delay(latency + Settings.ExtraDelay);
                var acceptButton = npcTradingWindow.AcceptButton;
                Settings.SetsAmountStatistics++;
                Settings.SetsAmountStatisticsText = $"Total sets sold to vendor: {Settings.SetsAmountStatistics}";

                if (Settings.AutoSell.Value)
                {
                    await Mouse.SetCursorPosAndLeftClick(acceptButton.GetClientRectCache.Center.ToVector2Num() + gameWindow,
                        Settings.ExtraDelay.Value);
                }
                else
                    Input.SetCursorPos(acceptButton.GetClientRectCache.Center.ToVector2Num() + gameWindow);
            }
            catch
            {
                LogMessage("We hit catch!", 3);
                Input.KeyUp(Keys.LControlKey);
                await Task.Delay(INPUT_DELAY);

                // We are not talking to a vendor.
            }

            return true;
        }

        public async SyncTask<bool> DropAllItems()
        {
            var stashPanel = GameController.IngameState.IngameUi.StashElement;
            var gameWindowPos = GameController.Window.GetWindowRectangle();
            var latency = GameController.Game.IngameState.ServerData.Latency + Settings.ExtraDelay;
            var cursorPosPreMoving = Input.ForceMousePositionNum;

            // Iterrate through all the different item types.
            for (var i = 0; i < 8; i++) //Check that we have enough items for any set
            {
                var part = _itemSetTypes[i];
                var items = part.GetPreparedItems();

                Input.KeyDown(Keys.LControlKey);
                await Task.Delay(INPUT_DELAY);

                try
                {
                    foreach (var curPreparedItem in items)
                    {
                        // If items is already in our inventory, move on.
                        if (curPreparedItem.BInPlayerInventory)
                            continue;

                        // Switch to the tab we want to go to.
                        if (!await _inventDrop.SwitchToTab(curPreparedItem.StashIndex, curPreparedItem.NestedStashIndex, Settings))
                        {
                            //throw new Exception("Can't switch to tab");
                            Input.KeyUp(Keys.LControlKey);
                            return false;
                        }

                        await Task.Delay(latency + Settings.ExtraDelay);

                        // Get the current visible stash tab.
                        _currentOpenedStashTab = stashPanel.VisibleStash;

                        var item = curPreparedItem;

                        var foundItem =
                        _currentOpenedStashTab.VisibleInventoryItems.FirstOrDefault(
                        x => x.Item?.GetComponent<Base>()?.Name == item.ItemName
                        );

                        var curItemsCount = _currentOpenedStashTab.VisibleInventoryItems.Count;

                        if (foundItem != null)
                        {
                            // If we found the item.
                            await Mouse.SetCursorPosAndLeftClick(foundItem.GetClientRect().Center.ToVector2Num() + gameWindowPos.TopLeft.ToVector2Num(),
                                Settings.ExtraDelay);

                            item.BInPlayerInventory = true;
                            await Task.Delay(latency + 100 + Settings.ExtraDelay);

                            if (_currentOpenedStashTab.VisibleInventoryItems.Count == curItemsCount)
                            {
                                //LogError("Item was not dropped?? : " + curPreparedItem.ItemName + ", checking again...", 10);
                                await Task.Delay(200);

                                if (_currentOpenedStashTab.VisibleInventoryItems.Count == curItemsCount)
                                {
                                    LogError("Item was not dropped after additional delay: " + curPreparedItem.ItemName,
                                        5);
                                }
                            }
                        }
                        else
                        {
                            LogError("We couldn't find the item we were looking for.\n" +
                                     $"ItemName: {item.ItemName}.\n" +
                                     $"Inventory Position: ({item.InventPosX},{item.InventPosY})", 5);
                        }

                        //Thread.Sleep(200);
                        UpdateStashes();
                    }
                }
                catch (Exception ex)
                {
                    LogError($"Error move items: {ex}", 4);
                }

                Input.KeyUp(Keys.LControlKey);

                //part.RemovePreparedItems();
            }

            UpdatePlayerInventory();
            UpdateItemsSetsInfo();

            Input.SetCursorPos(cursorPosPreMoving);
            return true;
        }

        private void DrawSetsInfo()
        {
            var stash = GameController.IngameState.IngameUi.StashElement;
            var leftPanelOpened = stash.IsVisible;

            if (leftPanelOpened)
            {
                // TODO:2
                if (_currentSetData.BSetIsReady && _currentOpenedStashTab != null)
                {
                    var visibleInventoryItems = _currentOpenedStashTab?.VisibleInventoryItems;

                    if (visibleInventoryItems != null)
                    {
                        var stashTabRect = _currentOpenedStashTab.InventoryUIElement.GetClientRect();

                        var setItemsListRect = new RectangleF(stashTabRect.Right, stashTabRect.Bottom, 270, 240);
                        Graphics.DrawBox(setItemsListRect, new Color(0, 0, 0, 200));
                        Graphics.DrawFrame(setItemsListRect, Color.White, 2);

                        var drawPosX = setItemsListRect.X + 10;
                        var drawPosY = setItemsListRect.Y + 10;

                        var setType = _currentSetData.SetType == 1 ? "Chaos" : "Regal";
                        Graphics.DrawText($"Current {setType} set:", new Vector2(drawPosX, drawPosY),
                            Color.White, 15);

                        drawPosY += 25;

                        var currentStash = (stash.IndexVisibleStash, _currentOpenedStashTab?.NestedVisibleInventoryIndex);
                        foreach (var part in _itemSetTypes)
                        {
                            var items = part.GetPreparedItems();

                            foreach (var curPreparedItem in items)
                            {
                                var inInventory = _sData.PlayerInventory.StashTabItems.Contains(curPreparedItem);
                                var curStashOpened = (curPreparedItem.StashIndex, curPreparedItem.NestedStashIndex) == currentStash;
                                var color = Color.Gray;

                                if (inInventory)
                                    color = Color.Green;
                                else if (curStashOpened)
                                    color = Color.Yellow;

                                if (!inInventory && curStashOpened)
                                {
                                        var item = curPreparedItem;
                                        // Enhanced check for item name alongside position
                                        var foundItem = visibleInventoryItems.FirstOrDefault(x =>
                                            x.Item?.GetComponent<Base>()?.Name == item.ItemName &&
                                            x.InventPosX == item.InventPosX && 
                                            x.InventPosY == item.InventPosY);
                                        if (foundItem != null)
                                        
                                            // Highlight the item frame in the stash
                                            Graphics.DrawFrame(foundItem.GetClientRect(), Color.Yellow, 2);
                                        }

                                var levelDesignation = curPreparedItem.LowLvl ? "L" : "H";
                                Graphics.DrawText($"{curPreparedItem.StashName} ({curPreparedItem.ItemName}) {levelDesignation}",
                                    new Vector2(drawPosX, drawPosY), color, 15);
                                drawPosY += 20;
                            }
                        }
                    }
                }
            }

            if (Settings.ShowOnlyWithInventory)
            {
                if (!GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible)
                    return;
            }

            if (Settings.HideWhenLeftPanelOpened)
            {
                if (leftPanelOpened)
                    return;
            }

            var posX = Settings.PositionX.Value;
            var posY = Settings.PositionY.Value;

            var rect = new RectangleF(posX, posY, 230 * Settings.WidthMultiplier, 280 * Settings.HeightMultiplier);
            Graphics.DrawBox(rect, new Color(0, 0, 0, 200));
            Graphics.DrawFrame(rect, Color.White, 2);

            posX += 10;
            posY += 10;
            Graphics.DrawText(_drawInfoString, new Vector2(posX, posY), Color.White, 15);
        }

        private void UpdateItemsSetsInfo()
        {
            _currentSetData = new CurrentSetInfo();

            _itemSetTypes = new BaseSetPart[8];
            _itemSetTypes[0] = new WeaponItemsSetPart("Weapons") { ItemCellsSize = 8 };
            _itemSetTypes[1] = new SingleItemSetPart("Helmets") { ItemCellsSize = 4 };
            _itemSetTypes[2] = new SingleItemSetPart("Body Armors") { ItemCellsSize = 6 };
            _itemSetTypes[3] = new SingleItemSetPart("Gloves") { ItemCellsSize = 4 };
            _itemSetTypes[4] = new SingleItemSetPart("Boots") { ItemCellsSize = 4 };
            _itemSetTypes[5] = new SingleItemSetPart("Belts") { ItemCellsSize = 2 };
            _itemSetTypes[6] = new SingleItemSetPart("Amulets") { ItemCellsSize = 1 };
            _itemSetTypes[7] = new RingItemsSetPart("Rings") { ItemCellsSize = 1 };

            for (var i = 0; i <= 7; i++)
            {
                DisplayData[i].BaseData = _itemSetTypes[i];
            }

            foreach (var item in _sData.PlayerInventory.StashTabItems)
            {
                var index = (int)item.ItemType;

                if (index > 7)
                    index = 0; // Switch One/TwoHanded to 0(weapon)

                var setPart = _itemSetTypes[index];
                item.BInPlayerInventory = true;
                setPart.AddItem(item);
            }

            const int StashCellsCount = 12 * 12;

            foreach (var stash in _sData.StashTabs.Values)
            {
                var stashTabItems = stash.StashTabItems;

                foreach (var item in stashTabItems)
                {
                    var index = (int)item.ItemType;

                    if (index > 7)
                        index = 0; // Switch One/TwoHanded to 0(weapon)

                    var setPart = _itemSetTypes[index];
                    item.BInPlayerInventory = false;
                    setPart.AddItem(item);
                    setPart.StashTabItemsCount = stashTabItems.Count;
                }
            }

            //Calculate sets:
            _drawInfoString = "";
            var chaosSetMaxCount = 0;

            var regalSetMaxCount = int.MaxValue;
            var minItemsCount = int.MaxValue;
            var maxItemsCount = 0;

            for (var i = 0; i <= 7; i++) //Check that we have enough items for any set
            {
                var setPart = _itemSetTypes[i];

                var low = setPart.LowSetsCount();
                var high = setPart.HighSetsCount();
                var total = setPart.TotalSetsCount();

                if (minItemsCount > total)
                    minItemsCount = total;

                if (maxItemsCount < total)
                    maxItemsCount = total;

                if (regalSetMaxCount > high)
                    regalSetMaxCount = high;

                chaosSetMaxCount += low;
                _drawInfoString += setPart.GetInfoString() + "\r\n";

                var drawInfo = DisplayData[i];
                drawInfo.TotalCount = total;

                if (Settings.CalcByFreeSpace.Value)
                {
                    var totalPossibleStashItemsCount = StashCellsCount / setPart.ItemCellsSize;

                    drawInfo.FreeSpaceCount = totalPossibleStashItemsCount -
                                              (setPart.StashTabItemsCount + setPart.PlayerInventItemsCount());

                    if (drawInfo.FreeSpaceCount < 0)
                        drawInfo.FreeSpaceCount = 0;

                    drawInfo.PriorityPercent = (float)drawInfo.FreeSpaceCount / totalPossibleStashItemsCount;

                    if (drawInfo.PriorityPercent > 1)
                        drawInfo.PriorityPercent = 1;

                    drawInfo.PriorityPercent = 1 - drawInfo.PriorityPercent;
                }
            }

            if (!Settings.CalcByFreeSpace.Value)
            {
                var maxSets = maxItemsCount;

                if (Settings.MaxSets.Value > 0)
                    maxSets = Settings.MaxSets.Value;

                for (var i = 0; i <= 7; i++)
                {
                    var drawInfo = DisplayData[i];

                    if (drawInfo.TotalCount == 0)
                        drawInfo.PriorityPercent = 0;
                    else
                    {
                        drawInfo.PriorityPercent = (float)drawInfo.TotalCount / maxSets;

                        if (drawInfo.PriorityPercent > 1)
                            drawInfo.PriorityPercent = 1;
                    }
                }
            }

            _drawInfoString += "\r\n";

            var chaosSets = Math.Min(minItemsCount, chaosSetMaxCount);

            _drawInfoString += "Chaos sets ready: " + chaosSets;

            if (Settings.ShowRegalSets.Value)
            {
                _drawInfoString += "\r\n";
                _drawInfoString += "Regal sets ready: " + regalSetMaxCount;
            }

            if (chaosSets <= 0 && regalSetMaxCount <= 0)
                return;
            if (chaosSets <= 0 && Settings.OptimizeChaosSets.Value)
                return;

            {
                var maxAvailableReplaceCount = 0;
                var replaceIndex = -1;

                var isLowSet = false;

                for (var i = 0; i < 8; i++) //Check that we have enough items for any set
                {
                    var part = _itemSetTypes[i];
                    var prepareResult = part.PrepareItemForSet(Settings);

                    isLowSet = isLowSet || prepareResult.LowSet;

                    if (maxAvailableReplaceCount >= prepareResult.AllowedReplacesCount || prepareResult.BInPlayerInvent)
                        continue;

                    maxAvailableReplaceCount = prepareResult.AllowedReplacesCount;
                    replaceIndex = i;
                }

                if (!isLowSet)
                {
                    if (Settings.ShowRegalSets)
                    {
                        _currentSetData.BSetIsReady = true;
                        _currentSetData.SetType = 2;
                        return;
                    }

                    if (maxAvailableReplaceCount == 0)
                    {
                        //LogMessage("You want to make a regal set anyway? Ok.", 2);
                        _currentSetData.BSetIsReady = true;
                        _currentSetData.SetType = 2;
                        return;
                    }

                    if (replaceIndex != -1)
                    {
                        _itemSetTypes[replaceIndex].DoLowItemReplace();
                        _currentSetData.SetType = 1;
                        _currentSetData.BSetIsReady = true;
                    }
                    else
                    {
                        _currentSetData.BSetIsReady = true;
                        _currentSetData.SetType = 1;
                    }
                }
                else
                {
                    _currentSetData.BSetIsReady = true;
                    _currentSetData.SetType = 1;
                }
            }
        }

        private void UpdateStashes()
        {
            var stashPanel = GameController.IngameState.IngameUi.StashElement;

            if (stashPanel == null)
            {
                LogMessage("ServerData.StashPanel is null", 3);
                return;
            }

            if (!stashPanel.IsVisible)
            {
                return;
            }
            
            _currentOpenedStashTab = stashPanel.VisibleStash;
            var visibleStashIndex = stashPanel.IndexVisibleStash;
            var openedStashTabName = stashPanel.GetStashName(visibleStashIndex);

            if (_currentOpenedStashTab == null) return;

            if (Settings.OnlyAllowedStashTabs.Value)
            {
                if (!Settings.AllowedStashTabs.Contains(visibleStashIndex)) return;
            }

            var stash = _currentOpenedStashTab;
            var visibleInventoryItems = stash?.VisibleInventoryItems;

            if (visibleInventoryItems == null) return;

            var nestedStashIndex = stash.NestedVisibleInventoryIndex;
            var items = new List<StashItem>();

            foreach (var invItem in visibleInventoryItems)
            {
                var item = invItem.Item;
                var newStashItem = ProcessItem(item);

                if (newStashItem == null)
                {
                    if (Settings.ShowRedRectangleAroundIgnoredItems)
                        Graphics.DrawFrame(invItem.GetClientRect(), Color.Red, 2);

                    continue;
                }

                newStashItem.StashName = openedStashTabName;
                newStashItem.StashIndex = visibleStashIndex;
                newStashItem.NestedStashIndex = nestedStashIndex;
                newStashItem.InventPosX = invItem.InventPosX;
                newStashItem.InventPosY = invItem.InventPosY;
                newStashItem.BInPlayerInventory = false;
                items.Add(newStashItem);
            }

            if (!_sData.StashTabs.TryGetValue((visibleStashIndex, nestedStashIndex), out var curStashData))
            {
                _sData.StashTabs[(visibleStashIndex, nestedStashIndex)] = curStashData = new StashTabData();
            }

            curStashData.StashTabItems = items;
        }

        private void UpdatePlayerInventory()
        {
            //    if (!GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible)
            //        return false;

            var inventory = GameController.Game.IngameState.ServerData.PlayerInventories[0].Inventory;

            if (_sData?.PlayerInventory == null) return;

            _sData.PlayerInventory = new StashTabData();

            var invItems = inventory;

            if (invItems == null) return;

            foreach (var invItem in invItems.InventorySlotItems)
            {
                var item = invItem;
                var newAddedItem = ProcessItem(item.Item);

                if (newAddedItem == null) continue;
                newAddedItem.InventPosX = (int)invItem.InventoryPosition.X;
                newAddedItem.InventPosY = (int)invItem.InventoryPosition.Y;
                newAddedItem.BInPlayerInventory = true;
                _sData.PlayerInventory.StashTabItems.Add(newAddedItem);
            }
        }

        private StashItem ProcessItem(Entity item)
        {
            try
            {
                if (item == null) return null;

                var mods = item?.GetComponent<Mods>();

                if (mods?.ItemRarity != ItemRarity.Rare)
                    return null;

                var bIdentified = mods.Identified;

                if (bIdentified && !Settings.AllowIdentified)
                    return null;

                if (mods.ItemLevel < 60)
                    return null;

                var newItem = new StashItem
                {
                    LowLvl = mods.ItemLevel < 75
                };

                if (string.IsNullOrEmpty(item.Metadata))
                {
                    LogError("Item metadata is empty. Can be fixed by restarting the game", 10);
                    return null;
                }

                if (Settings.IgnoreElderShaper.Value)
                {
                    var baseComp = item.GetComponent<Base>();

                    if (baseComp.isElder || baseComp.isShaper)
                        return null;
                }

                var bit = GameController.Files.BaseItemTypes.Translate(item.Metadata);

                if (bit == null)
                    return null;

                newItem.ItemClass = bit.ClassName;
                newItem.ItemName = bit.BaseName;
                newItem.ItemType = GetStashItemTypeByClassName(newItem.ItemClass);
                newItem.Width = bit.Width;
                newItem.Height = bit.Height;

                if (newItem.ItemType != StashItemType.Undefined)
                    return newItem;
            }
            catch (Exception e)
            {
                LogError($"Error in \"ProcessItem\": {e}", 10);
                return null;
            }

            return null;
        }

        private StashItemType GetStashItemTypeByClassName(string className)
        {
            if (className.StartsWith("Two Hand"))
                return StashItemType.TwoHanded;

            if (className.StartsWith("One Hand") || className.StartsWith("Thrusting One Hand"))
                return StashItemType.OneHanded;

            switch (className)
            {
                case "Wand": return StashItemType.OneHanded;
                case "Dagger": return StashItemType.OneHanded;
                case "Rune Dagger": return StashItemType.OneHanded;
                case "Sceptre": return StashItemType.OneHanded;
                case "Claw": return StashItemType.OneHanded;
                case "Shield": return StashItemType.OneHanded;
                case "Bow": return StashItemType.TwoHanded;
                case "Staff": return StashItemType.TwoHanded;
                case "Warstaff": return StashItemType.TwoHanded;

                case "Ring": return StashItemType.Ring;
                case "Amulet": return StashItemType.Amulet;
                case "Belt": return StashItemType.Belt;

                case "Helmet": return StashItemType.Helmet;
                case "Body Armour": return StashItemType.Body;
                case "Boots": return StashItemType.Boots;
                case "Gloves": return StashItemType.Gloves;

                default:
                    return StashItemType.Undefined;
            }
        }

        public override void DrawSettings()
        {
            base.DrawSettings();
            var stashPanel = GameController.Game.IngameState.IngameUi.StashElement;
            var realNames = stashPanel.AllStashNames;

            var uniqId = 0;

            if (ImGui.Button($"Add##{uniqId++}"))
            {
                Settings.AllowedStashTabs.Add(-1);
            }

            for (var i = 0; i < Settings.AllowedStashTabs.Count; i++)
            {
                var value = Settings.AllowedStashTabs[i];

                if (ImGui.Combo(value < realNames.Count && value >= 0 ? realNames[value] : "??", ref value, realNames.ToArray(), realNames.Count))
                {
                    Settings.AllowedStashTabs[i] = value;
                }

                ImGui.SameLine();

                if (ImGui.Button($"Remove##{uniqId++}"))
                {
                    Settings.AllowedStashTabs.RemoveAt(i);
                    i--;
                }
            }
        }

        public override void OnClose()
        {
            if (_sData != null)
                StashData.Save(this, _sData);
        }

        private struct CurrentSetInfo
        {
            public bool BSetIsReady;
            public int SetType; // 1 - chaos set, 2 - regal set
        }

        public class ItemDisplayData
        {
            public BaseSetPart BaseData;
            public int FreeSpaceCount;
            public float PriorityPercent;
            public int TotalCount;
        }

        #region Draw labels

        private readonly Dictionary<Entity, ItemDisplayData> _currentAlerts =
            new Dictionary<Entity, ItemDisplayData>();

        private Dictionary<long, LabelOnGround> _currentLabels =
            new Dictionary<long, LabelOnGround>();

        private void RenderLabels()
        {
            if (!Settings.EnableBorders.Value)
                return;

            var shouldUpdate = false;

            var tempCopy = new Dictionary<Entity, ItemDisplayData>(_currentAlerts);

            var keyValuePairs = tempCopy.AsParallel().Where(x => x.Key != null && x.Key.Address != 0 && x.Key.IsValid)
                .ToList();

            foreach (var kv in keyValuePairs)
            {
                if (DrawBorder(kv.Key.Address, kv.Value) && !shouldUpdate)
                    shouldUpdate = true;
            }

            if (shouldUpdate)
            {
                _currentLabels = GameController.Game.IngameState.IngameUi.ItemsOnGroundLabels
                    .Where(y => y?.ItemOnGround != null).GroupBy(y => y.ItemOnGround.Address).ToDictionary(y => y.Key, y => y.First());
            }

            if (!Settings.InventBorders.Value)
                return;

            if (!GameController.Game.IngameState.IngameUi.InventoryPanel.IsVisible)
                return;

            var playerInv = GameController.Game.IngameState.IngameUi.InventoryPanel[InventoryIndex.PlayerInventory];
            var visibleInventoryItems = playerInv.VisibleInventoryItems ?? null;

            if (visibleInventoryItems == null || visibleInventoryItems.Count == 0)
                return;

            if (playerInv.HoverItem != null && !Settings.LablesWhileHovered)
                return;

            foreach (var inventItem in visibleInventoryItems)
            {
                var item = inventItem.Item;

                if (item == null)
                    continue;

                var visitResult = ProcessItem(item);

                if (visitResult == null) continue;
                var index = (int)visitResult.ItemType;

                if (index > 7)
                    index = 0;

                var data = DisplayData[index];
                var rect = inventItem.GetClientRect();

                var borderColor = Color.Lerp(Color.Red, Color.Green, data.PriorityPercent);

                rect.X += 2;
                rect.Y += 2;

                rect.Width -= 4;
                rect.Height -= 4;

                var testRect = new RectangleF(rect.X + 3, rect.Y + 3, 40, 20);

                Graphics.DrawBox(testRect, new Color(10, 10, 10, 230));
                Graphics.DrawFrame(rect, borderColor, 2);

                Graphics.DrawText(
                    Settings.CalcByFreeSpace.Value ? $"{data.FreeSpaceCount}" : $"{data.PriorityPercent:p0}", testRect.TopLeft,
                    Color.White,
                    Settings.TextSize.Value);
            }
        }

        private bool DrawBorder(long entityAddress, ItemDisplayData data)
        {
            if (GameController.Game.IngameState.IngameUi.Atlas.IsVisible)
                return false;

            if (GameController.Game.IngameState.IngameUi.BetrayalWindow.IsVisible)
                return false;

            if (GameController.Game.IngameState.IngameUi.CraftBench.IsVisible)
                return false;

            if (GameController.Game.IngameState.IngameUi.DelveWindow.IsVisible)
                return false;

            if (GameController.Game.IngameState.IngameUi.IncursionWindow.IsVisible)
                return false;

            /*
            if (GameController.Game.IngameState.IngameUi.MetamorphWindow.IsVisible)
                return false;
            */

            if (GameController.Game.IngameState.IngameUi.TreePanel.IsVisible)
                return false;

            if (GameController.Game.IngameState.IngameUi.UnveilWindow.IsVisible)
                return false;

            if (GameController.Game.IngameState.IngameUi.ZanaMissionChoice.IsVisible)
                return false;

            if (Settings.Ignore1 && data.PriorityPercent == 1 &&
                !(data.BaseData.PartName == "Amulets" || data.BaseData.PartName == "Rings")) return false;

            var ui = GameController.Game.IngameState.IngameUi;

            if (!_currentLabels.TryGetValue(entityAddress, out var entityLabel))
                return true;
            if (!entityLabel.IsVisible) return false;

            var rect = entityLabel.Label.GetClientRect();

            if (ui.OpenLeftPanel.IsVisible && ui.OpenLeftPanel.GetClientRect().Intersects(rect) ||
                ui.OpenRightPanel.IsVisible && ui.OpenRightPanel.GetClientRect().Intersects(rect))
                return false;

            var incrSize = Settings.BorderOversize.Value;

            if (Settings.BorderAutoResize.Value)
                incrSize = (int)Lerp(incrSize, 1, data.PriorityPercent);

            rect.X -= incrSize;
            rect.Y -= incrSize;

            rect.Width += incrSize * 2;
            rect.Height += incrSize * 2;

            var borderColor = Color.Lerp(Color.Red, Color.Green, data.PriorityPercent);

            var borderWidth = Settings.BorderWidth.Value;

            if (Settings.BorderAutoResize.Value)
                borderWidth = (int)Lerp(borderWidth, 1, data.PriorityPercent);

            Graphics.DrawFrame(rect, borderColor, borderWidth);

            if (Settings.TextSize.Value == 0) return false;

            if (Settings.TextOffsetX < 0)
                rect.X += Settings.TextOffsetX;
            else
                rect.X += rect.Width * (Settings.TextOffsetX.Value / 10);

            if (Settings.TextOffsetY < 0)
                rect.Y += Settings.TextOffsetY;
            else
                rect.Y += rect.Height * (Settings.TextOffsetY.Value / 10);

            Graphics.DrawText(
                Settings.CalcByFreeSpace.Value ? $"{data.FreeSpaceCount}" : $"{data.PriorityPercent:p0}",
                rect.TopLeft.ToVector2Num(),
                Color.White,
                Settings.TextSize.Value
            );
            return false;
        }

        private float Lerp(float a, float b, float f)
        {
            return a + f * (b - a);
        }

        #endregion
    }
}
