using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Memory;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;

namespace DesynthesisTwo; 

public unsafe class DesynthesisWindow : Window {

    private readonly float maxDesynthLevel;
    private delegate void DesynthItemDelegate(AgentInterface* agent, InventoryItem* item, ushort a3 = 0, byte a4 = 0);
    private IntPtr desynthItemPtr;
    private readonly DesynthItemDelegate? desynthItem;
    private readonly Plugin plugin;
    
    public DesynthesisWindow(Plugin plugin, ImGuiWindowFlags flags = ImGuiWindowFlags.None, bool forceMainWindow = false) : base($"{plugin.Name}###desynthesisTwoMainWindow", flags, forceMainWindow) {
        this.plugin = plugin;
        desynthItemPtr = Plugin.SigScanner.ScanText("E8 ?? ?? ?? ?? EB 0D 83 FB FF");
        if (desynthItemPtr != IntPtr.Zero) {
            desynthItem = Marshal.GetDelegateForFunctionPointer<DesynthItemDelegate>(desynthItemPtr);
        }
        
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(600, 200), MaximumSize = new Vector2(6000, 2000)};
        PositionCondition = ImGuiCond.FirstUseEver;
        SizeCondition = ImGuiCond.FirstUseEver;
        
        
        itemSheet = Plugin.Data.Excel.GetSheet<Item>();
        if (itemSheet != null) {
            foreach (var i in itemSheet) {
                if (i.Desynth > 0 && i.LevelItem.Row > maxDesynthLevel) {
                    maxDesynthLevel = i.LevelItem.Row;
                }
            }
        }

    }

    private class ItemEntry {
        public InventoryItem* Slot;
        public Item? Item;
    }
    
    
    private List<ItemEntry> items = new();

    private readonly ExcelSheet<Item>? itemSheet;


    private static Vector2 CentreText(string text) {
        var size = ImGui.CalcTextSize(text);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X / 2 - size.X / 2);
        ImGui.Text(text);
        return size;
    }

    private static void EllipsisText(string text) {
        var space = ImGui.GetContentRegionAvail().X;

        if (ImGui.CalcTextSize(text).X <= space) {
            ImGui.Text(text);
            return;
        }
        
        var shortenedText = text;
        do {
            shortenedText = shortenedText.TrimEnd('.')[..^1] + "...";
        } while (ImGui.CalcTextSize(shortenedText).X > space);

        ImGui.Text(shortenedText);
        if (ImGui.IsItemHovered()) ImGui.SetTooltip($"{text}");
    }

    private byte timeSinceUpdate;

    private const uint Red = 0xFF4444EE;
    private const uint Green = 0xFF00CC00;
    private const uint Yellow = 0xFF00CCCC;
    private const uint Blue = 0xFFDD9955;

    public bool SettingsWindowOpen;
    
    private int hoveredRowIndex = -1;
    
    
    public bool IsDrawing { get; private set; }

    public override bool DrawConditions() {
        return Plugin.ClientState.LocalContentId > 0;
    }

    private float gearSetColumnSize = 50f;
    private float classColumnSize = 50f;
    
    private static uint RarityColour(byte rarity) {
        return rarity switch {
            1 => 0xFFFFFFFF,
            2 => 0xFF99CC99,
            3 => 0xFFDD9955,
            7 => 0xFFAA77DD,
            _ => 0xFFFFFFFF,
        };
    }

    public override void PreDraw() => IsDrawing = false;

    public override void Draw() {
        
        if (itemSheet == null) {
            ImGui.PushStyleColor(ImGuiCol.Text, Red);
            ImGui.Text("Failed to load item sheet.");
            ImGui.PopStyleColor();
            return;
        }

        if (desynthItem == null || desynthItemPtr == IntPtr.Zero) {
            ImGui.PushStyleColor(ImGuiCol.Text, Red);
            CentreText("Failed to setup correctly.");
            ImGui.PopStyleColor();
            return;
        }
        
        IsDrawing = true;
        var windowDrawList = ImGui.GetWindowDrawList();
        
        if (ImGui.Checkbox("Show Armoury     ", ref Plugin.Config.ShowArmoury)) {
            Plugin.SaveConfig();
            UpdateItemList();
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("Hide Gearset Items     ", ref Plugin.Config.HideGearSetItems)) {
            Plugin.SaveConfig();
            UpdateItemList();
        }
        ImGui.SameLine();
        if (ImGui.Checkbox("Hide No Skill Up     ", ref Plugin.Config.HideNoSkillUp)) {
            Plugin.SaveConfig();
            UpdateItemList();
        }

        ImGui.SameLine();
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.SetCursorPosX(ImGui.GetContentRegionMax().X - ImGui.GetTextLineHeightWithSpacing());
        if (ImGui.Button($"{(char)FontAwesomeIcon.Cog}")) {
            SettingsWindowOpen = !SettingsWindowOpen;
            plugin.SettingsWindow.IsOpen = SettingsWindowOpen;
        }
        ImGui.PopFont();
        
        
        ImGui.Separator();

        
        if (timeSinceUpdate++ > 100) UpdateItemList();
        var colCount = 5;
        if (Plugin.Config.HideGearSetItems) colCount--;

        if (items.Count == 0) {
            
            ImGui.Dummy(ImGuiHelpers.ScaledVector2(50, 50));
            ImGui.PushStyleColor(ImGuiCol.Text, Red);
            CentreText("You have no items that match your filters.");
            ImGui.PopStyleColor();
        } else if (ImGui.BeginTable("desynthesisItemTable", colCount, ImGuiTableFlags.SortTristate | ImGuiTableFlags.BordersInner | ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg)) {
            
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Quantity", ImGuiTableColumnFlags.WidthFixed, 65);
            ImGui.TableSetupColumn("Class", ImGuiTableColumnFlags.WidthFixed, classColumnSize);
            ImGui.TableSetupColumn("Skill", ImGuiTableColumnFlags.WidthFixed, 100);
            if (!Plugin.Config.HideGearSetItems) ImGui.TableSetupColumn("Gearset", ImGuiTableColumnFlags.WidthFixed, gearSetColumnSize);
            ImGui.TableSetupScrollFreeze(1, 2);

            for (var i = 0; i < ImGui.TableGetColumnCount(); i++) {
                ImGui.TableNextColumn();
                CentreText(ImGui.TableGetColumnName(i));
            }
            ImGui.TableNextRow();
            ImGui.TableNextRow();
           
            var headerPosition = ImGui.GetCursorScreenPos();
            
            ImGui.SetWindowFontScale(1.2f);

            var newHoveredRowIndex = -1;
            
            for (var i = 0; i < items.Count; i++) {
                
                var slot = items[i].Slot;
                if (slot->ItemID == 0) continue;
                if (slot->Quantity == 0) continue;
                var item = itemSheet.GetRow(slot->ItemID);
                if (item == null) continue;
                if (item.Desynth == 0) continue;
                if (item.ClassJobRepair?.Value == null) continue;
                ImGui.PushID("desynthesisItemTableRow" + i);
                ImGui.TableNextColumn();
                
                if (ImGui.TableGetRowIndex() == hoveredRowIndex) {
                    ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
                }

                
                var rowMin = ImGui.GetCursorScreenPos();
                var categoryIcon = (uint)(item.ItemUICategory?.Value?.Icon ?? 0);
                var icon = Plugin.IconManager.GetIconTexture(Plugin.Config.ActualIcons ? item.Icon : categoryIcon);
                
                if (icon == null || icon.ImGuiHandle == IntPtr.Zero) {
                    ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                } else {
                    ImGui.Image(icon.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                }
                ImGui.SameLine();
                
                ImGui.PushStyleColor(ImGuiCol.Text, RarityColour(item.Rarity));
                EllipsisText($"{item.Name?.RawString ?? $"{item.RowId}"}");
                ImGui.PopStyleColor();
                ImGui.TableNextColumn(); 
                CentreText($"{slot->Quantity}");
                ImGui.TableNextColumn();
                
                var cjIconTexture = Plugin.IconManager.GetIconTexture(62000 + item.ClassJobRepair.Row);

                if (cjIconTexture == null || cjIconTexture.ImGuiHandle == IntPtr.Zero) {
                    ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                } else {
                    ImGui.Image(cjIconTexture.ImGuiHandle, new Vector2(ImGui.GetTextLineHeight(), ImGui.GetTextLineHeight()));
                }
                var classSize = ImGui.GetItemRectSize() + ImGui.GetStyle().ItemSpacing * 2;
                ImGui.SameLine();
                
                ImGui.Text($"{item.ClassJobRepair.Value.Name?.RawString ?? "Something Broke..."}");
                classSize += ImGui.GetItemRectSize() * Vector2.UnitX;
                if (classSize.X > classColumnSize) classColumnSize = classSize.X;
                
                ImGui.TableNextColumn();

                var desynthLevel = UIState.Instance()->PlayerState.GetDesynthesisLevel(item.ClassJobRepair.Row);


                uint c;
                if (desynthLevel >= maxDesynthLevel) {
                    c = Blue;
                } else {
                    if (desynthLevel > item.LevelItem.Row) {
                        c = desynthLevel < item.LevelItem.Row + 50 ? Yellow : Green;
                    } else {
                        c = Red;
                    }
                }
                
                ImGui.PushStyleColor(ImGuiCol.Text, c);
                CentreText($"{(Plugin.Config.ShowDecimals ? desynthLevel : MathF.Floor(desynthLevel))}/{item.LevelItem.Row}");
                ImGui.PopStyleColor();

                if (!Plugin.Config.HideGearSetItems) {
                    ImGui.TableNextColumn();
                    var gearSet = GetGearSetWithItem(slot);
                    if (gearSet != null) {
                        var n = MemoryHelper.ReadStringNullTerminated(new IntPtr(gearSet->Name));
                        var s = CentreText(n) + ImGui.GetStyle().ItemSpacing * 2;
                        if (s.X > gearSetColumnSize) {
                            gearSetColumnSize = s.X;
                        }
                    } else {
                        ImGui.Text(string.Empty);
                    }
                }

                var rowMax = ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetContentRegionAvail().X, -3);

                var mouse = ImGui.GetMousePos();
                
                if (IsFocused && mouse.X > rowMin.X && mouse.X < rowMax.X && mouse.Y > rowMin.Y && mouse.Y < rowMax.Y && rowMin.Y > headerPosition.Y) {
                    newHoveredRowIndex = ImGui.TableGetRowIndex();

                    windowDrawList.AddRectFilled(rowMin, rowMax, ImGui.ColorConvertFloat4ToU32(*ImGui.GetStyleColorVec4(ImGuiCol.Separator)));
                    
                    if (ImGui.IsMouseClicked(ImGuiMouseButton.Left)) {
                        var agent = Framework.Instance()->GetUiModule()->GetAgentModule()->GetAgentByInternalID(124); // Salvage
                        if (agent != null) {
                            desynthItem(agent, slot);
                        } else {
                            desynthItemPtr = IntPtr.Zero;
                        }
                    }
                    
                }
                ImGui.PopID();
            }

            hoveredRowIndex = newHoveredRowIndex;
            ImGui.SetWindowFontScale(1f);
            ImGui.EndTable();
        }
        
        Position = ImGui.GetWindowPos();
        Size = ImGui.GetWindowSize();
    }

    private static RaptureGearsetModule.GearsetEntry* GetGearSetWithItem(InventoryItem* slot) {
        var gearSetModule = RaptureGearsetModule.Instance();
        var itemIdWithHQ = slot->ItemID;
        if ((slot->Flags & InventoryItem.ItemFlags.HQ) > 0) itemIdWithHQ += 1000000;
        for (var gs = 0; gs < 101; gs++) {
            var gearSet = gearSetModule->Gearset[gs];
            if (gearSet->ID != gs) break;
            if (!gearSet->Flags.HasFlag(RaptureGearsetModule.GearsetFlag.Exists)) continue;
            var gearSetItems = (RaptureGearsetModule.GearsetItem*)gearSet->ItemsData;
            for (var j = 0; j < 14; j++) {
                if (gearSetItems[j].ItemID == itemIdWithHQ) {
                    return gearSet;
                }
            }
        }

        return null;
    } 


    private void UpdateItemList() {
        timeSinceUpdate = 0;
        items.Clear();
        if (itemSheet == null) return;
        var inventoryManager = InventoryManager.Instance();

        var searchInventories = new List<InventoryType>() {
            InventoryType.Inventory1, InventoryType.Inventory2, InventoryType.Inventory3, InventoryType.Inventory4
        };

        if (Plugin.Config.ShowArmoury) {
            searchInventories.AddRange(new[] {
                InventoryType.ArmoryMainHand,
                InventoryType.ArmoryOffHand,
                InventoryType.ArmoryHead,
                InventoryType.ArmoryBody,
                InventoryType.ArmoryHands,
                InventoryType.ArmoryLegs,
                InventoryType.ArmoryEar,
                InventoryType.ArmoryFeets,
                InventoryType.ArmoryNeck,
                InventoryType.ArmoryWrist,
                InventoryType.ArmoryRings
            });
        }
        
        // Get all items
        foreach (var inventoryType in searchInventories) {
            var inventory = inventoryManager->GetInventoryContainer(inventoryType);

            for (var i = 0; i < inventory->Size; i++) {
                var slot = inventory->GetInventorySlot(i);
                if (slot->ItemID == 0) continue;
                if (slot->Quantity == 0) continue;
                var item = itemSheet.GetRow(slot->ItemID);
                if (item == null) continue;
                if (item.Desynth == 0) continue;

                if (Plugin.Config.HideNoSkillUp) {
                    var desynthLevel = UIState.Instance()->PlayerState.GetDesynthesisLevel(item.ClassJobRepair.Row);
                    if (desynthLevel >= item.LevelItem.Row + 50 || desynthLevel >= maxDesynthLevel) continue;
                }
                
                if (Plugin.Config.HideGearSetItems && GetGearSetWithItem(slot) != null) continue;
                
                items.Add(new ItemEntry() { Slot = slot, Item = item});
            }
        }
        
        // Apply Sorting
        var sortedItems = items.OrderBy(a => a.Item?.ClassJobRepair?.Row);
        sortedItems = sortedItems.ThenByDescending(a => a.Item?.LevelItem?.Row);
        sortedItems = sortedItems.ThenBy(a => a.Item?.Name?.RawString);
        items = sortedItems.ToList();
    }

    public override void OnOpen() {
        SettingsWindowOpen = false;
        UpdateItemList();
    }
}
