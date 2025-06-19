using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Verse;
using static FlavorText.CategoryUtility;

//--TODO: recipe parent hierarchy
//DONE: spreadsheet descriptions are misaligned
//xxTODO: blank ingredient option


namespace FlavorText;
/// <summary>
///     Effectively recipes
///     show what combination of ingredients/categories are needed for each particular flavor label
/// </summary>
/// 
public class FlavorDef : Def
{
    private bool tag;  // debug tag
    
    public float specificity;  // how specific is this FlavorDef: how many ingredient choices are there, does it need to be a certain meal type, etc?

    public FlavorCategoryDef lowestCommonIngredientCategory;  // lowest category that contains all the ingredients in the FlavorDef; used to optimize searches; defaults to flavorRoot

    public List<FlavorCategoryDef> mealKinds = [];  // what types of meals are allowed to have this FlavorDef; empty means all

    public List<FlavorCategoryDef> mealQualities = [];

    public List<FlavorCategoryDef> cookingStations = [];  // which buildings are allowed to cook this FlavorDef; empty means all

    public IntRange hoursOfDay = new(0, 23);  // what hours of the day this FlavorDef can be completed during, defaults to all day (0-23)

    public FloatRange ingredientsHitPointPercentage = new (0, 1); // allowed range of percentage of hit points of each ingredient group (ignoring quantity in group), defaults to all (0-1)

    // all FlavorDefs that can be used with the current modlist
    public static IEnumerable<FlavorDef> ActiveFlavorDefs => DefDatabase<FlavorDef>.AllDefs
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
        List<FlavorCategoryDef> emptyMealKinds = [.. FlavorCategoryDefOf.FT_MealsKinds.ThisAndChildCategoryDefs.Where(cat => cat.DescendantThingDefs.Count() == 0)];
        foreach (var flavorDef in ActiveFlavorDefs)
        {
            flavorDef.mealKinds = [.. flavorDef.mealKinds.Except(emptyMealKinds)];
            flavorDef.mealKinds.ForEach(mealKind => activeMealKinds.AddDistinct(mealKind));
            if (flavorDef.mealKinds.Empty() || (FlavorTextSettings.laxRecipeMatching && flavorDef.mealKinds.Any(kind => kind.ThisAndParents.Contains(FlavorCategoryDefOf.FT_MealsCooked))))
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
            foreach (var slot in flavorDef.ingredients)
            {
                slot.AddAllowedThingDefsRecursive(slot.categories);
            }
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

            foreach (var slot in flavorDef.ingredients)
            {
                flavorDef.specificity += slot.AllowedThingDefs.Count();
            }

            // more specific if it has a required meal type, weighted to half-impact
            flavorDef.specificity = (flavorDef.specificity * (flavorDef.mealKinds.Sum(mealCategory => (float)mealCategory.DescendantThingDefs.Count()) / totalMealTypes + 1) / 2);

            // more specific if it has a required cooking station, weighted to half-impact
            if (!flavorDef.cookingStations.NullOrEmpty())
            {
                flavorDef.specificity = ((flavorDef.specificity * flavorDef.cookingStations.Sum(station => (float)station.DescendantThingDefs.Count()) / totalCookingStations + 1) / 2);
            }
            // more specific if it has a required cooking time of day, weighted to half-impact
            if (flavorDef.hoursOfDay != new IntRange(0, 23))
            {
                int timeLength = flavorDef.hoursOfDay.max - flavorDef.hoursOfDay.min;
                timeLength = timeLength % 24 + 1;
                flavorDef.specificity *= ((float)timeLength / 24 + 1) / 2;
            }

            if (flavorDef.ingredientsHitPointPercentage != new FloatRange(0, 1))
            {
                flavorDef.specificity *= flavorDef.ingredientsHitPointPercentage.Span;
            }

            // calculate the lowest category containing all the ingredients in the FlavorDef
            // no need to include disallowed categories b/c those should always be a subcategory of a valid category
            flavorDef.lowestCommonIngredientCategory = FlavorCategoryDefOf.FT_Foods;
            // get each category and its parents
            List<List<FlavorCategoryDef>> allCategoriesInDefAndParents = [.. flavorDef.ingredients
                    .SelectMany(slot => slot.categories)
                    .Distinct()
                    .Select(cat => cat.ThisAndParents.ToList())];

            // compare the corresponding elements of each parent list, going from last to first
            // if they are no longer equal, then the previous element was the lowest common category
            if (!allCategoriesInDefAndParents.NullOrEmpty())
            {
                int min = (from List<FlavorCategoryDef> parents in allCategoriesInDefAndParents select parents.Count).Min();
                var first = allCategoriesInDefAndParents[0].ToList();
                for (int i = 0; i < min; i++)
                {
                    if (allCategoriesInDefAndParents.All(cat => cat[cat.Count - 1 - i] == first[first.Count - 1 - i]))
                    {
                        flavorDef.lowestCommonIngredientCategory = first[first.Count - 1 - i];
                        continue;
                    }
                    break;
                }
            }

        }
    }



    // all FinalFlavorDefs that fit the given meal type, quality, and extra parameters
    // can be restricted to a given list of flavorDefsToSearch, which is used when loading a saved meal that already has flavor text to reduce search time
    public static IEnumerable<FlavorDef> ValidFlavorDefs(ThingWithComps meal, IEnumerable<FlavorDef> flavorDefsToSearch = null)
    {
        var compFlavor = meal.TryGetComp<CompFlavor>();
        List<FlavorCategoryDef> thisMealParents = [];
        foreach (var cat in ThingCategories[meal.def])
        {
            var temp = cat.ThisAndParents.FirstOrDefault(activeMealKinds.Contains);
            if (temp is not null) thisMealParents.Add(temp);
        }
        flavorDefsToSearch ??= ActiveFlavorDefs;
        return flavorDefsToSearch
        .Where(flavorDef =>
            flavorDef.mealKinds.Any(mealKind => thisMealParents.Contains(mealKind))
            && (flavorDef.mealQualities.NullOrEmpty() || flavorDef.mealQualities.Any(mealQuality => mealQuality.ContainedInThisOrDescendant(meal.def)))
            && (flavorDef.cookingStations.NullOrEmpty() || flavorDef.cookingStations.Any(cat =>
                cat.ContainedInThisOrDescendant(compFlavor.CookingStation)))
            && ((flavorDef.hoursOfDay.min <= compFlavor.HourOfDay &&
                    compFlavor.HourOfDay <= flavorDef.hoursOfDay.max))
            && (flavorDef.ingredientsHitPointPercentage.Includes(
                (float)compFlavor.IngredientsHitPointPercentage!)));

    }
}

public class IngredientSlot : IExposable
{
    public List<FlavorCategoryDef> categories = [];
    public List<FlavorCategoryDef> disallowedCategories = [];
    private HashSet<ThingDef> allowedThingDefs = [];
    public IEnumerable<ThingDef> AllowedThingDefs => allowedThingDefs;

    internal void AddAllowedThingDefsRecursive(IEnumerable<FlavorCategoryDef> cats)
    {
        foreach (var cat in cats)
        {
            if (disallowedCategories.Contains(cat)) continue;
            allowedThingDefs.AddRange(cat.childThingDefs);
            AddAllowedThingDefsRecursive(cat.childCategories);
        }
    }

    public virtual void ExposeData()
    {
        Scribe_Collections.Look(ref allowedThingDefs, "allowedThingDefs");
    }
}