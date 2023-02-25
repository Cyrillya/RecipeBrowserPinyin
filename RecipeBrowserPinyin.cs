using System;
using System.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour.HookGen;
using NPinyin;
using RecipeBrowser;
using RecipeBrowser.UIElements;
using Terraria;
using Terraria.ModLoader;

namespace RecipeBrowserPinyin;

public class RecipeBrowserPinyin : Mod
{
    private static Item _itemSlotItem;
    private static Item _recipeSlotItem;
    private static UINPCSlot _npcSlot;

    public override void Load() {
        if (Main.dedServ) return;

        // same in all classes so made into a method
        void FilterDetourMethod<T>(Action<T> orig, T self) {
            // deleted validation code because I just don't like it
            self.GetType().GetField("updateNeeded", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(self, true);
        }

        // same in all classes so made into a method
        ILCursor PassingFiltersILFinding(ILContext il) {
            var c = new ILCursor(il);

            // IL_02a0: ldarg.0
            // IL_02a1: ldfld class RecipeBrowser.NewUITextBox RecipeBrowser.ItemCatalogueUI::itemNameFilter
            // IL_02a6: ldfld string RecipeBrowser.NewUITextBox::currentString
            // IL_02ab: ldc.i4.5
            // IL_02ac: callvirt instance int32 [System.Runtime]System.String::IndexOf(string, valuetype [System.Runtime]System.StringComparison)
            // IL_02b1: ldc.i4.m1
            // IL_02b2: bne.un.s IL_02b6
            c.GotoNext(MoveType.After,
                i => i.Match(OpCodes.Ldarg_0),
                i => i.Match(OpCodes.Ldfld),
                i => i.Match(OpCodes.Ldfld),
                i => i.Match(OpCodes.Ldc_I4_5),
                i => i.Match(OpCodes.Callvirt));
            return c;
        }

        // shortcut for removing spaces in a string
        string RemoveSpaces(string s) => s.Replace(" ", "", StringComparison.Ordinal);

        // pinyin search matching method
        bool Matches(int originalComparison, string name, string content) {
            if (originalComparison != -1) {
                return true;
            }

            string searchContent = RemoveSpaces(content.ToLower());
            string nameFixed = RemoveSpaces(name.ToLower());
            string pinyin = RemoveSpaces(Pinyin.GetPinyin(nameFixed));
            string pinyinInitials = Pinyin.GetInitials(nameFixed).ToLower();
            if (pinyin.Contains(searchContent) || pinyinInitials.Contains(searchContent)) {
                return true;
            }

            return false;
        }

        #region Remove Search Content Validations

        HookEndpointManager.Add(
            typeof(ItemCatalogueUI).GetMethod("ValidateItemFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<ItemCatalogueUI>, ItemCatalogueUI>) FilterDetourMethod);

        HookEndpointManager.Add(
            typeof(RecipeCatalogueUI).GetMethod("ValidateItemFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<RecipeCatalogueUI>, RecipeCatalogueUI>) FilterDetourMethod);

        HookEndpointManager.Add(
            typeof(BestiaryUI).GetMethod("ValidateNPCFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<BestiaryUI>, BestiaryUI>) FilterDetourMethod);

        #endregion

        #region Getting Slot Instances by Detouring
        
        // out of no reason we can't get slots in IL editing by emitting ldloc.0, so we add detours here
        HookEndpointManager.Add(
            typeof(ItemCatalogueUI).GetMethod("PassItemFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            (Func<ItemCatalogueUI, UIItemCatalogueItemSlot, bool> orig, ItemCatalogueUI self,
                UIItemCatalogueItemSlot slot) => {
                _itemSlotItem = slot.item;
                return orig(self, slot);
            });

        HookEndpointManager.Add(
            typeof(RecipeCatalogueUI).GetMethod("PassRecipeFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            (Func<RecipeCatalogueUI, UIRecipeSlot, Recipe, List<int>, bool> orig, RecipeCatalogueUI self,
                UIRecipeSlot slot, Recipe recipe, List<int> groups) => {
                _recipeSlotItem = slot.item;
                return orig(self, slot, recipe, groups);
            });

        HookEndpointManager.Add(
            typeof(BestiaryUI).GetMethod("PassNPCFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            (Func<BestiaryUI, UINPCSlot, bool> orig, BestiaryUI self,
                UINPCSlot slot) => {
                _npcSlot = slot;
                return orig(self, slot);
            });

        #endregion

        #region Actual Modifications to Searching

        HookEndpointManager.Modify(
            typeof(ItemCatalogueUI).GetMethod("PassItemFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            (ILContext il) => {
                var c = PassingFiltersILFinding(il);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, ItemCatalogueUI, int>>((originalComparison, self) => {
                    if (Matches(originalComparison, Lang.GetItemNameValue(_itemSlotItem.type),
                            self.itemNameFilter.currentString))
                        return 0;
                    return -1;
                });
            });

        HookEndpointManager.Modify(
            typeof(RecipeCatalogueUI).GetMethod("PassRecipeFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            (ILContext il) => {
                var c = PassingFiltersILFinding(il);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, RecipeCatalogueUI, int>>((originalComparison, self) => {
                    if (Matches(originalComparison, Lang.GetItemNameValue(_recipeSlotItem.type),
                            self.itemNameFilter.currentString))
                        return 0;
                    return -1;
                });
            });

        HookEndpointManager.Modify(
            typeof(BestiaryUI).GetMethod("PassNPCFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            (ILContext il) => {
                var c = PassingFiltersILFinding(il);
                c.Emit(OpCodes.Ldarg_0);
                c.EmitDelegate<Func<int, BestiaryUI, int>>((originalComparison, self) => {
                    if (Matches(originalComparison, Lang.GetNPCNameValue(_npcSlot.npcType),
                            self.npcNameFilter.currentString))
                        return 0;
                    return -1;
                });
            });

        #endregion
    }
}