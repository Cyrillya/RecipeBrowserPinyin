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

        HookEndpointManager.Add(
            typeof(ItemCatalogueUI).GetMethod("ValidateItemFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<ItemCatalogueUI>, ItemCatalogueUI>) ValidateFilterDetourMethod);
        HookEndpointManager.Add(
            typeof(RecipeCatalogueUI).GetMethod("ValidateItemFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<RecipeCatalogueUI>, RecipeCatalogueUI>) ValidateFilterDetourMethod);
        HookEndpointManager.Add(
            typeof(BestiaryUI).GetMethod("ValidateNPCFilter", BindingFlags.NonPublic | BindingFlags.Instance),
            (Action<Action<BestiaryUI>, BestiaryUI>) ValidateFilterDetourMethod);

        #endregion

        #region Override Filter Passes

        HookEndpointManager.Add(
            typeof(ItemCatalogueUI).GetMethod("PassItemFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            DetourItemFilters);
        HookEndpointManager.Add(
            typeof(RecipeCatalogueUI).GetMethod("PassRecipeFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            DetourRecipeFilters);
        HookEndpointManager.Add(
            typeof(BestiaryUI).GetMethod("PassNPCFilters", BindingFlags.NonPublic | BindingFlags.Instance),
            DetourNPCFilters);

        #endregion
    }
}