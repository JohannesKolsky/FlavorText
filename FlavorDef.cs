using PipeSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using static FlavorText.CategoryUtility;
using static FlavorText.DietKind;

//--TODO: recipe parent hierarchy
//DONE: spreadsheet descriptions are misaligned
//--TODO: blank ingredient option
//DONE: resolve question of how to deal with twisted/vegetarian/etc meals: they are separate categorizatons so they should be separate fields in each FlavorDef; but what about stuff like [FT_Meat_Twisted, FT_Fungus] vs [FT_Meat_Twisted/FT_Fungus]?
//DONE: candy has meat FoodKind allowed

//TODO: use default disallowed ingredients for SimpleMeal to exclude human meat, insect meat, etc from meals

namespace FlavorText;

internal class DietKind
{
    internal enum Diet { hyperCarnivore, carnivore, omnivore, vegetarian, vegan, fungus, cannibal, insect, twisted }

    internal static readonly Dictionary<Diet, List<FlavorCategoryDef>> dietExcludedCategories = new()
    {
     {Diet.hyperCarnivore, [FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw] },
     {Diet.carnivore, [FlavorCategoryDefOf.FT_PlantFoodRaw]},
     {Diet.omnivore, []},
     {Diet.vegetarian, [FlavorCategoryDefOf.FT_MeatRaw]},
     {Diet.vegan, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw]}
    }; 
    
    internal static readonly Dictionary<Diet, List<FlavorCategoryDef>> dietIncludedCategories = new()
    {
     {Diet.hyperCarnivore, [FlavorCategoryDefOf.FT_MeatRaw]},
     {Diet.carnivore, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw]},
     {Diet.vegetarian, [FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw] },
     {Diet.vegan, [FlavorCategoryDefOf.FT_PlantFoodRaw]},
     {Diet.omnivore, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw]}
    };

    internal static readonly List<FlavorCategoryDef> normalDietCategories = [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw, FlavorCategoryDefOf.FT_FoodRaw, FlavorCategoryDefOf.FT_Foods];

    internal static readonly List<FlavorCategoryDef> sketchyDietCategories = [FlavorCategoryDefOf.FT_Fungus, FlavorCategoryDefOf.FT_Meat_Human, FlavorCategoryDefOf.FT_Meat_Insect, FlavorCategoryDefOf.FT_Meat_Twisted];

    internal static IEnumerable<FlavorCategoryDef> GetExcludedFlavorCategoriesFromDiet(Diet dietTuple)
    {
        return dietExcludedCategories[dietTuple];
    }
}

/// <summary>
///     Effectively recipes
///     show what combination of ingredients/categories are needed for each particular flavor label
/// </summary>
/// 
public class FlavorDef : Def
{
    private static bool tag;  // debug tag

    internal List<int> formattingIndices = [];  // this tells you which ingredient slot matches with which placeholder index for formatting the flavor label and description; this is needed because the ingredient slots are reordered according to specificity during game load

    public float specificity;  // how specific is this FlavorDef: how many ingredient choices are there, does it need to be a certain meal type, etc?

    public FlavorCategoryDef lowestCommonRecipeCategory;  // lowest category that contains all the ingredients in the FlavorDef; used to optimize searches; defaults to flavorRoot

    internal List<Diet> allowedDiets = []; // what types of food are generally allowed by this FlavorDef (vegan, vegetarian, carn)

    internal List<FlavorCategoryDef> requiredSketchyIngredients = []; // whether the FlavorDef requires something like fungus or insect meat

    public List<FlavorCategoryDef> mealKinds = [];  // what types of meals are allowed to have this FlavorDef; empty means all

    public List<FlavorCategoryDef> mealQualities = [];

    public List<FlavorCategoryDef> cookingStations = [];  // which buildings are allowed to cook this FlavorDef; empty means all

    public IntRange hoursOfDay = new(0, 23);  // what hours of the day this FlavorDef can be completed during, defaults to all day (0-23)

    public FloatRange ingredientsHitPointPercentage = new(0, 1); // allowed range of percentage of hit points of each ingredient group (ignoring quantity in group), defaults to all (0-1)

    // all FlavorDefs that can be used with the current modlist
    private static IEnumerable<FlavorDef> activeFlavorDefs;
    public static IEnumerable<FlavorDef> ActiveFlavorDefs => activeFlavorDefs ??= DefDatabase<FlavorDef>.AllDefs
                    .Where(flavorDef => flavorDef != null)
                        .Where(flavorDef => flavorDef.ingredients
                            .All(ingredientSlot => ingredientSlot.AllowedThingDefs.Any()));

    private readonly string varietyTexture;
    public string VarietyTexture => varietyTexture;

    private static readonly List<FlavorCategoryDef> activeMealKinds = [];

    public List<IngredientSlot> ingredients = [];

    public static void SetStaticData()
    {
        try
        {
            SetAllowedIngredients();
            SetActiveMealKinds();
            SetSpecificities();
            SortSlots();
        }
        catch (Exception ex)
        {
            Log.Error($"Error when setting static data for Flavor Defs, error: {ex}");
        }
    }

    // remove mealKind categories that aren't being used (e.g. FT_MealsSoup if no mods add soup meals)
    // if this means a FlavorDef has no mealKind, add FT_MealsNonSpecial to it, so it can be used by normal meals and survival pack meals
    private static void SetActiveMealKinds()
    {
        List<FlavorCategoryDef> emptyMealKinds = [.. FlavorCategoryDefOf.FT_MealsKinds.ThisAndChildren.Where(cat => cat.DescendantThingDefs.Count() == 0)];
        foreach (var flavorDef in ActiveFlavorDefs)
        {
            flavorDef.mealKinds = [.. flavorDef.mealKinds.Except(emptyMealKinds)];
            flavorDef.mealKinds.ForEach(mealKind => activeMealKinds.AddDistinct(mealKind));
            if (flavorDef.mealKinds.Empty())
            {
                flavorDef.mealKinds.Add(FlavorCategoryDefOf.FT_MealsNonSpecial);
            }
        }
    }

    // register all allowed Defs for each ingredient slot in each Flavor Def
    private static void SetAllowedIngredients()
    {
        foreach (var flavorDef in DefDatabase<FlavorDef>.AllDefs)
        {
            tag = flavorDef.defName == "FlavorText_FarmersSalad";
            if (tag) Log.Warning(flavorDef.defName);
            foreach (var slot in flavorDef.ingredients)
            {
                slot.AddAllowedThingDefsRecursive(slot.categories);
            }
            if (tag) Log.Message($"[{flavorDef.ingredients.Select(slot => $"[{slot.AllowedThingDefs.ToStringSafeEnumerable()}]").ToStringSafeEnumerable()}]");
        }
    }

    private static void SetSpecificities()
    {

        // get all FlavorDefs, excluding those which have an ingredient slot which has no allowed ThingDefs (i.e. no ThingDefs in the current modlist fit that slot)
        int totalCookingStations = FlavorCategoryDefOf.FT_CookingStations.DescendantThingDefs.Count();
        int totalMealTypes = FlavorCategoryDefOf.FT_MealsWithCompFlavor.DescendantThingDefs.Count();

        foreach (FlavorDef flavorDef in ActiveFlavorDefs)
        {
            if (flavorDef.mealKinds.NullOrEmpty())
            {
                Log.Error($"The FlavorDef {flavorDef.defName} did not have any MealKinds, it will never appear in-game. Please report.");
            }


            float restrictions = flavorDef.ingredients.Sum(ing => Mathf.Sqrt(ing.AllowedThingDefs.Count()));  //sqrt to reduce impact of high ingredient counts

            // more specific if it has a required meal type, weighted to half-impact
            restrictions = restrictions * ((flavorDef.mealKinds.Sum(mealCategory => (float)mealCategory.DescendantThingDefs.Count()) / totalMealTypes) + 1) / 2;

            // more specific if it has a required cooking station, weighted to half-impact
            if (!flavorDef.cookingStations.NullOrEmpty())
            {
                restrictions = ((restrictions * flavorDef.cookingStations.Sum(station => (float)station.DescendantThingDefs.Count()) / totalCookingStations) + 1) / 2;
            }
            // more specific if it has a required cooking time of day, weighted to half-impact
            if (flavorDef.hoursOfDay != new IntRange(0, 23))
            {
                int timeLength = flavorDef.hoursOfDay.max - flavorDef.hoursOfDay.min;
                timeLength = (timeLength % 24) + 1;
                restrictions *= (((float)timeLength / 24) + 1) / 2;
            }

            if (flavorDef.ingredientsHitPointPercentage != new FloatRange(0, 1))
            {
                restrictions *= flavorDef.ingredientsHitPointPercentage.Span;
            }

            // higher restrictions: more broad (more ingredients, more cooking stations, etc)
            // higher specificity: more narrow
            if (restrictions > 0) flavorDef.specificity = 100 / restrictions;


            // get each category and its parents
            List<FlavorCategoryDef> allCategoriesInDef = [.. flavorDef.ingredients
                    .SelectMany(slot => slot.categories)
                    .Distinct()];

            flavorDef.lowestCommonRecipeCategory = FindLowestCommonCategory(allCategoriesInDef);

            // {Meat, Egg, Grain} => [[Meat], [Animal], [Plant]] => [omnivore]
            // {Egg, Grain/Fungus} => [[Animal], [Fungus, Plant]] => [fungus, vegetarian]
            // {Egg, Fungus} => [[Animal], [Fungus]] => [fungus]
            // {Egg, Fungus/Twisted} => [[Animal], [Fungus, Twisted]] => [fungus, twisted]

            // (fungus, twisted meat) meal => [omnivore, twisted, fungus]

            // calculate if the FlavorDef could match a meat/vegan/vegetarian meal (can have multiple)
            if (flavorDef.ingredients.Empty()) continue;
            List<List<FlavorCategoryDef>> slotDiets = [.. Enumerable.Repeat(new List<FlavorCategoryDef>(), flavorDef.ingredients.Count())];
            List<List<FlavorCategoryDef>> slotDietsSketchy = [.. Enumerable.Repeat(new List<FlavorCategoryDef>(), flavorDef.ingredients.Count())];

            for (int i = 0; i < flavorDef.ingredients.Count; i++)
            {
                IngredientSlot slot = flavorDef.ingredients[i];
                foreach (var cat in slot.categories)
                {
                    // do normal categories like FT_MeatRaw, FT_Foods
                    foreach (FlavorCategoryDef dietCat in normalDietCategories)
                    {
                        if (cat.ThisAndParents.Contains(dietCat) || cat.ThisAndChildren.Contains(dietCat))
                        {
                            //TODO: when you use List.Add, this adds the dietCat to EACH sublist, why??
                            slotDiets[i] = [.. slotDiets[i], dietCat];
                        }
                    }

                    // do sketchy categories like FT_Fungus, FT_Meat_Insect
                    foreach (FlavorCategoryDef sketchyCat in sketchyDietCategories)
                    {
                        if (cat.ThisAndParents.Contains(sketchyCat))
                        {
                            slotDietsSketchy[i] = [.. slotDietsSketchy[i], sketchyCat];
                        }
                    }
                }
            }

            foreach (var sketchy in sketchyDietCategories)
            {
                if (slotDietsSketchy.Any(diet => diet.Contains(sketchy))) flavorDef.requiredSketchyIngredients.Add(sketchy);
            }


            if (slotDiets.All(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw))) flavorDef.allowedDiets.Add(Diet.hyperCarnivore);
            if (slotDiets.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw)) && slotDiets.All(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw) || diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw))) flavorDef.allowedDiets.Add(Diet.carnivore);
            if (slotDiets.Count() > 1 && slotDiets.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw)) && slotDiets.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw)) && slotDiets.All(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw) || diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw) || diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw))) flavorDef.allowedDiets.Add(Diet.omnivore);
            if (slotDiets.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw)) && slotDiets.All(diet => diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw) || diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw))) flavorDef.allowedDiets.Add(Diet.vegetarian);
            if (slotDiets.All(diet => diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw))) flavorDef.allowedDiets.Add(Diet.vegan);

            if (tag) Log.Warning($"{flavorDef.defName.ToStringSafe()} had allowedDietKinds [{flavorDef.allowedDiets.ToStringSafeEnumerable()}] and slotDiets [{slotDiets.Select(slot => $"[{slot.ToStringSafeEnumerable()}]").ToStringSafeEnumerable()}]");
        }
    }

    // sort the ingredient slots according to specificity, which is necessary for ingredient matching
    private static void SortSlots()
    {
        foreach (var flavorDef in ActiveFlavorDefs)
        {
            IEnumerable<(IngredientSlot value, int index)> slotsSorted = flavorDef.ingredients.Select((value, index) => (value, index))
                .OrderBy(item => item.value.AllowedThingDefs.Count());
            flavorDef.ingredients = [.. slotsSorted.Select(slot => slot.value)];
            flavorDef.formattingIndices = [.. slotsSorted.Select(slot => slot.index)];
        }
    }

    // all FinalFlavorDefs that fit the given meal type, quality, and extra parameters
    // can be restricted to a given list of flavorDefsToSearch, which is used when loading a saved meal that already has flavor text to reduce search time
    public static IEnumerable<FlavorDef> ValidFlavorDefs(ThingWithComps meal, IEnumerable<FlavorDef> flavorDefsToSearch = null)
    {
        var compFlavor = meal.TryGetComp<CompFlavor>();
        List<FlavorCategoryDef> mealThingParentCategories = [];
        foreach (var cat in ThingCategories[meal.def])
        {
            var temp = cat.ThisAndParents.FirstOrDefault(activeMealKinds.Contains);
            if (temp is not null) mealThingParentCategories.Add(temp);
        }
        flavorDefsToSearch ??= ActiveFlavorDefs;
        return flavorDefsToSearch
        .Where(flavorDef =>
            (flavorDef.mealKinds.Any(mealKind => mealThingParentCategories.Contains(mealKind)) || (FlavorTextSettings.laxRecipeMatching && flavorDef.mealKinds.Any(mealKind => mealThingParentCategories.Contains(FlavorCategoryDefOf.FT_MealsCooked))))
            && (flavorDef.mealQualities.NullOrEmpty() || flavorDef.mealQualities.Any(mealQuality => mealQuality.ContainedInThisOrDescendant(meal.def)))
            && (flavorDef.cookingStations.NullOrEmpty() || flavorDef.cookingStations.Any(cat =>
                cat.ContainedInThisOrDescendant(compFlavor.CookingStation)))
            && flavorDef.hoursOfDay.min <= compFlavor.HourOfDay &&
                    compFlavor.HourOfDay <= flavorDef.hoursOfDay.max
                    /*&& flavorDef.ingredientsHitPointPercentage.Includes(
                        (float)compFlavor.IngredientsHitPointPercentage!)*/);

    }
}

public class IngredientSlot : IExposable
{
    public List<FlavorCategoryDef> categories = [];
    public List<FlavorCategoryDef> disallowedCategories = [];
    private HashSet<ThingDef> allowedThingDefs = [];
    private HashSet<FlavorCategoryDef> allowedCategories = [];  // all allowed categories
    public IEnumerable<ThingDef> AllowedThingDefs => allowedThingDefs;
    public IEnumerable<FlavorCategoryDef> AllowedCategories => allowedCategories;

    internal void AddAllowedThingDefsRecursive(IEnumerable<FlavorCategoryDef> cats)
    {
        foreach (var cat in cats)
        {
            if (disallowedCategories.Contains(cat)) continue;
            allowedCategories.Add(cat);
            allowedThingDefs.AddRange(cat.childThingDefs);
            AddAllowedThingDefsRecursive(cat.childCategories);
        }
    }

    public virtual void ExposeData()
    {
        Scribe_Collections.Look(ref allowedThingDefs, "allowedThingDefs");
    }
}

