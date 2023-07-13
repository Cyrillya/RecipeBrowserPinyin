using System;
using System.Reflection;
using MonoMod.RuntimeDetour.HookGen;
using RecipeBrowser;
using Terraria.ModLoader;

namespace RecipeBrowserPinyin;

public partial class RecipeBrowserPinyin : Mod
{
    public override void Load() {
        #region Remove Search Content Validations

        MonoModHooks.Add(
            typeof(ItemCatalogueUI).GetMethod("ValidateItemFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<ItemCatalogueUI>, ItemCatalogueUI>) ValidateFilterDetourMethod);
        MonoModHooks.Add(
            typeof(RecipeCatalogueUI).GetMethod("ValidateItemFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<RecipeCatalogueUI>, RecipeCatalogueUI>) ValidateFilterDetourMethod);
        MonoModHooks.Add(
            typeof(BestiaryUI).GetMethod("ValidateNPCFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<BestiaryUI>, BestiaryUI>) ValidateFilterDetourMethod);

        #endregion

        #region Override Filter Passes

        MonoModHooks.Add(
            typeof(ItemCatalogueUI).GetMethod("PassItemFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            DetourItemFilters);
        MonoModHooks.Add(
            typeof(RecipeCatalogueUI).GetMethod("PassRecipeFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            DetourRecipeFilters);
        MonoModHooks.Add(
            typeof(BestiaryUI).GetMethod("PassNPCFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            DetourNPCFilters);

        #endregion
    }
}