using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NPinyin;
using RecipeBrowser;
using RecipeBrowser.UIElements;
using Terraria;

namespace RecipeBrowserPinyin;

public partial class RecipeBrowserPinyin
{
    // pinyin search matching method
    private static bool Matches(int originalComparison, string name, string content) {
        // shortcut for removing spaces in a string
        string RemoveSpaces(string s) => s.Replace(" ", "", StringComparison.Ordinal);

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

    // same in all classes so made into a method
    private static void ValidateFilterDetourMethod<T>(Action<T> orig, T self) {
        // deleted validation code because I just don't like it
        self.GetType().GetField("updateNeeded", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(self, true);
    }

    // I definitely don't want to override the whole method, instead of using IL editing, which I actually did previously
    // But that just crashes the recipe browser menu when you reload mods, out of no reason
    private static bool DetourItemFilters(Func<ItemCatalogueUI, UIItemCatalogueItemSlot, bool> orig,
        ItemCatalogueUI self, UIItemCatalogueItemSlot slot) {
        if (RecipeBrowserUI.modIndex != 0) {
            if (slot.item.ModItem == null) {
                return false;
            }

            if (slot.item.ModItem.Mod.Name != RecipeBrowserUI.instance.mods[RecipeBrowserUI.modIndex]) {
                return false;
            }
        }

        if (self.CraftedRadioButton.Selected) {
            if (!self.craftResults[slot.item.type])
                return false;
        }

        if (self.LootRadioButton.Selected) {
            if (!self.isLoot[slot.item.type])
                return false;
        }

        if (self.UnobtainedRadioButton.Selected && RecipeBrowserUI.instance.foundItems != null) {
            if (RecipeBrowserUI.instance.foundItems[slot.item.type])
                return false;
        }

        if (SharedUI.instance.SelectedCategory != null) {
            if (!SharedUI.instance.SelectedCategory.belongs(slot.item) &&
                !SharedUI.instance.SelectedCategory.subCategories.Any(x => x.belongs(slot.item)))
                return false;
        }


        foreach (var filter in SharedUI.instance.availableFilters) {
            if (filter.button.selected) {
                if (!filter.belongs(slot.item))
                    return false;
                if (filter == SharedUI.instance.ObtainableFilter) {
                    bool ableToCraft = false;
                    for (int i = 0; i < Recipe.numRecipes; i++) // Optimize with non-trimmed RecipePath.recipeDictionary
                    {
                        Recipe recipe = Main.recipe[i];
                        if (recipe.createItem.type == slot.item.type) {
                            UIRecipeSlot recipeSlot = RecipeCatalogueUI.instance.recipeSlots[i];
                            recipeSlot.CraftPathNeeded();
                            //recipeSlot.CraftPathsImmediatelyNeeded();
                            if ((recipeSlot.craftPathCalculated || recipeSlot.craftPathsCalculated) &&
                                recipeSlot.craftPaths.Count > 0) {
                                ableToCraft = true;
                                break;
                            }
                        }
                    }

                    if (!ableToCraft)
                        return false;
                }

                if (filter == SharedUI.instance.CraftableFilter) {
                    bool ableToCraft = false;
                    for (int n = 0; n < Main.numAvailableRecipes; n++) {
                        if (Main.recipe[Main.availableRecipe[n]].createItem.type == slot.item.type) {
                            ableToCraft = true;
                            break;
                        }
                    }

                    if (!ableToCraft)
                        return false;
                }
            }
        }

        int originalComparison =
            slot.item.Name.IndexOf(self.itemNameFilter.currentString, StringComparison.OrdinalIgnoreCase);
        if (!Matches(originalComparison, Lang.GetItemNameValue(slot.item.type),
                self.itemNameFilter.currentString))
            return false;

        if (self.itemDescriptionFilter.currentString.Length > 0) {
            if (SharedUI.instance.SelectedCategory.name == ArmorSetFeatureHelper.ArmorSetsHoverTest) {
                if (slot is UIArmorSetCatalogueItemSlot setCatalogueItemSlot)
                    return setCatalogueItemSlot.set.Item4.IndexOf(self.itemDescriptionFilter.currentString,
                        StringComparison.OrdinalIgnoreCase) != -1;
            }

            if ((slot.item.ToolTip != null && self.GetTooltipsAsString(slot.item.ToolTip)
                        .IndexOf(self.itemDescriptionFilter.currentString, StringComparison.OrdinalIgnoreCase) !=
                    -1) /*|| (recipe.createItem.toolTip2 != null && recipe.createItem.toolTip2.ToLower().IndexOf(itemDescriptionFilter.Text, StringComparison.OrdinalIgnoreCase) != -1)*/
               ) {
                return true;
            }
            else {
                return false;
            }
        }

        return true;
    }

    private static bool DetourRecipeFilters(Func<RecipeCatalogueUI, UIRecipeSlot, Recipe, List<int>, bool> orig,
        RecipeCatalogueUI self, UIRecipeSlot slot, Recipe recipe, List<int> groups) {
        #region Before Comparison

        // TODO: Option to filter by source of Recipe rather than by createItem maybe?
        if (RecipeBrowserUI.modIndex != 0) {
            if (recipe.createItem.ModItem == null) {
                return false;
            }

            if (recipe.createItem.ModItem.Mod.Name != RecipeBrowserUI.instance.mods[RecipeBrowserUI.modIndex]) {
                return false;
            }
        }

        if (self.NearbyIngredientsRadioBitton.Selected) {
            if (!self.PassNearbyChestFilter(recipe)) {
                return false;
            }
        }

        // Item Checklist integration
        if (self.ItemChecklistRadioButton.Selected) {
            if (RecipeBrowserUI.instance.foundItems != null) {
                foreach (Item item in recipe.requiredItem) {
                    if (!RecipeBrowserUI.instance.foundItems[item.type]) {
                        return false;
                    }
                }

                // filter out recipes that make things I've already obtained
                if (RecipeBrowserUI.instance.foundItems[recipe.createItem.type]) {
                    return false;
                }
            }
            else {
                Main.NewText("How is this happening??");
            }
        }

        // Filter out recipes that don't use selected Tile
        if (self.Tile > -1) {
            List<int> adjTiles = new List<int>();
            adjTiles.Add(self.Tile);
            if (self.uniqueCheckbox.CurrentState == 0) {
                Terraria.ModLoader.ModTile modTile = Terraria.ModLoader.TileLoader.GetTile(self.Tile);
                if (modTile != null) {
                    adjTiles.AddRange(modTile.AdjTiles);
                }

                if (self.Tile == 302)
                    adjTiles.Add(17);
                if (self.Tile == 77)
                    adjTiles.Add(17);
                if (self.Tile == 133) {
                    adjTiles.Add(17);
                    adjTiles.Add(77);
                }

                if (self.Tile == 134)
                    adjTiles.Add(16);
                if (self.Tile == 354)
                    adjTiles.Add(14);
                if (self.Tile == 469)
                    adjTiles.Add(14);
                if (self.Tile == 355) {
                    adjTiles.Add(13);
                    adjTiles.Add(14);
                }
                // TODO: GlobalTile.AdjTiles support (no player object, reflection needed since private)
            }

            if (!recipe.requiredTile.Any(t => adjTiles.Contains(t))) {
                return false;
            }
        }

        if (!self.queryItem.item.IsAir) {
            int type = self.queryItem.item.type;
            bool inGroup =
                recipe.acceptedGroups.Intersect(groups)
                    .Any(); // Lesion item bug, they have the Wood group but don't have any wood in them

            if (!inGroup) {
                if (!(recipe.createItem.type == type || recipe.requiredItem.Any(ing => ing.type == type))) {
                    return false;
                }
            }
        }

        var SelectedCategory = SharedUI.instance.SelectedCategory;
        if (SelectedCategory != null) {
            if (!SelectedCategory.belongs(recipe.createItem) &&
                !SelectedCategory.subCategories.Any(x => x.belongs(recipe.createItem)))
                return false;
        }

        var availableFilters = SharedUI.instance.availableFilters;
        if (availableFilters != null)
            foreach (var filter in SharedUI.instance.availableFilters) {
                if (!filter.button.selected && filter == SharedUI.instance.DisabledFilter) {
                    if (recipe.Disabled)
                        return false;
                }

                if (filter.button.selected) {
                    // Extended craft problem.
                    if (!filter.belongs(recipe.createItem))
                        return false;
                    if (filter == SharedUI.instance.ObtainableFilter) {
                        slot.CraftPathNeeded();
                        if (!((slot.craftPathCalculated || slot.craftPathsCalculated) && slot.craftPaths.Count > 0))
                            return false;
                    }

                    if (filter == SharedUI.instance.CraftableFilter) {
                        int index = slot.index;
                        bool ableToCraft = false;
                        for (int n = 0; n < Main.numAvailableRecipes; n++) {
                            if (index == Main.availableRecipe[n]) {
                                ableToCraft = true;
                                break;
                            }
                        }

                        if (!ableToCraft)
                            return false;
                    }
                }
            }

        #endregion

        int originalComparison = recipe.createItem.Name.ToLower()
            .IndexOf(self.itemNameFilter.currentString, StringComparison.OrdinalIgnoreCase);
        if (!Matches(originalComparison, Lang.GetItemNameValue(recipe.createItem.type),
                self.itemNameFilter.currentString))
            return false;

        if (self.itemDescriptionFilter.currentString.Length > 0) {
            if ((recipe.createItem.ToolTip != null && self.GetTooltipsAsString(recipe.createItem.ToolTip)
                        .IndexOf(self.itemDescriptionFilter.currentString, StringComparison.OrdinalIgnoreCase) !=
                    -1) /*|| (recipe.createItem.toolTip2 != null && recipe.createItem.toolTip2.ToLower().IndexOf(itemDescriptionFilter.Text, StringComparison.OrdinalIgnoreCase) != -1)*/
               ) {
                return true;
            }
            else {
                return false;
            }
        }

        return true;
    }

    private static bool DetourNPCFilters(Func<BestiaryUI, UINPCSlot, bool> orig, BestiaryUI self, UINPCSlot slot) {
        if (self.EncounteredRadioButton.Selected) {
            if (!RecipePath.NPCUnlocked(slot.npc.netID)) {
                return false;
            }
        }

        if (self.HasLootRadioButton.Selected) {
            // Slow, AnyDrops or Cache results.
            if (slot.GetDrops().Count == 0) {
                return false;
            }
        }

        if (self.NewLootOnlyRadioButton.Selected) {
            // Item Checklist integration
            if (RecipeBrowserUI.instance.foundItems != null) {
                bool hasNewItem = false;
                var drops = slot.GetDrops();
                foreach (var item in drops) {
                    if (!RecipeBrowserUI.instance.foundItems[item]) {
                        hasNewItem = true;
                        break;
                    }
                }

                if (!hasNewItem) return false;
            }
            else {
                Main.NewText("How is this happening?");
            }
        }

        if (RecipeBrowserUI.modIndex != 0) {
            if (slot.npc.ModNPC == null) {
                return false;
            }

            if (slot.npc.ModNPC.Mod.Name != RecipeBrowserUI.instance.mods[RecipeBrowserUI.modIndex]) {
                return false;
            }
        }

        if (!self.queryItem.item.IsAir) {
            if (!slot.GetDrops().Contains(self.queryItem.item.type))
                return false;
        }

        int originalComparison = Lang.GetNPCNameValue(slot.npcType)
            .IndexOf(self.npcNameFilter.currentString, StringComparison.OrdinalIgnoreCase);
        if (!Matches(originalComparison, Lang.GetNPCNameValue(slot.npcType),
                self.npcNameFilter.currentString)) return false;

        return true;
    }
}