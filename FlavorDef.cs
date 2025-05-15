using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

//--TODO: recipe parent hierarchy
//--TODO: spreadsheet descriptions are misaligned
//xxTODO: blank ingredient option


namespace FlavorText;

/// <summary>
///     Effectively recipes
///     show what combination of ingredients/categories are needed for each particular flavor label
/// </summary>
/// 
public class FlavorDef : RecipeDef
{
    public float Specificity;  // about how many possible ingredients could fulfill this FlavorDef?

    public ThingCategoryDef LowestCommonIngredientCategory;  // lowest category that contains all the ingredients in the FlavorDef; used to optimize searches; defaults to flavorRoot

    // ReSharper disable once UnusedMember.Global
    public string VarietyTexture;  // texture to use from Food Texture Variety

    public List<ThingCategoryDef> MealCategories = [];  // what types of meals are allowed to have this FlavorDef; defaults to all

    public List<ThingCategoryDef> CookingStations = [];  // which stations are allowed to cook this FlavorDef; defaults to all

    public IntRange HoursOfDay = new(0, 23);  // what hours of the day this FlavorDef can be completed during, defaults to all day (0-23)

    public FloatRange IngredientsHitPointPercentage = new (0, 1); // allowed range of percentage of hit points of each ingredient group (ignoring quantity in group), defaults to all (0-1)

    public static IEnumerable<FlavorDef> ActiveFlavorDefs;  // all FlavorDefs that can be used with the current modlist

    // about how many possible ingredients could fulfill each FlavorDef? add together all the specificities of all its categories; overlaps in categories will be counted multiple times
    // also calculate the lowest common category containing all ingredients for each FlavorDef
    public static void SetCategoryData()
    {
        try
        {
            // get all FlavorDefs, excluding those which have an ingredient slot which only has FT_ThingCategoryDefs which are empty (i.e. no ThingDefs in the current modlist fit that slot)
            ActiveFlavorDefs = DefDatabase<FlavorDef>.AllDefs
                .Where(flavorDef => flavorDef != null)
                    .Where(flavorDef => flavorDef.ingredients
                        .All(ingredientSlot => ingredientSlot.filter.AllowedDefCount > 0));

            int totalCookingStations = ThingCategoryDef.Named("FT_CookingStations").DescendantThingDefs.Count();
            int totalMealTypes = ThingCategoryDef.Named("FT_MealsFlavor").DescendantThingDefs.Count();
            
            foreach (FlavorDef flavorDef in ActiveFlavorDefs)
            {
                if (flavorDef.MealCategories.NullOrEmpty())
                {
                    Log.Error($"The FlavorDef {flavorDef.defName} did not have any MealCategories, it will never appear in-game. Please report."); 
                }

                foreach (IngredientCount ingredient in flavorDef.ingredients)
                {
                    flavorDef.Specificity += ingredient.filter.AllowedDefCount;
                }

                // more specific if it has a required meal type, weighted to half-impact
                flavorDef.Specificity = (flavorDef.Specificity * (flavorDef.MealCategories.Sum(mealCategory => (float)mealCategory.DescendantThingDefs.Count()) / totalMealTypes + 1) / 2);

                // more specific if it has a required cooking station, weighted to half-impact
                if (!flavorDef.CookingStations.NullOrEmpty())
                {
                    flavorDef.Specificity = ((flavorDef.Specificity * flavorDef.CookingStations.Sum(station => (float)station.DescendantThingDefs.Count()) / totalCookingStations + 1) / 2);
                }
                // more specific if it has a required cooking time of day, weighted to half-impact
                if (flavorDef.HoursOfDay != new IntRange(0, 23))
                {
                    int timeLength = flavorDef.HoursOfDay.max - flavorDef.HoursOfDay.min;
                    timeLength = timeLength % 24 + 1;
                    flavorDef.Specificity = (flavorDef.Specificity * ((float)timeLength / 24 + 1) / 2);
                }

                if (flavorDef.IngredientsHitPointPercentage != new FloatRange(0, 1))
                {
                    flavorDef.Specificity = (flavorDef.Specificity * flavorDef.IngredientsHitPointPercentage.Span);
                }

                // calculate the lowest category containing all the ingredients in the FlavorDef
                flavorDef.LowestCommonIngredientCategory = ThingCategoryDefUtility.FlavorRoot;
                // get each category and its parents
                List<List<ThingCategoryDef>> allCategoriesInDefAndParents = flavorDef.ingredients
                    .SelectMany(slot => GetFilterCategories(slot.filter, "categories"))
                    .Distinct()
                    .Select(cat => cat.Parents.Prepend(cat).ToList())
                    .ToList();

                // compare the corresponding elements of each parent list, going from last to first
                // if they are no longer equal, then the previous element was the lowest common category
                if (!allCategoriesInDefAndParents.NullOrEmpty())
                {
                    int min = (from List<ThingCategoryDef> parents in allCategoriesInDefAndParents select parents.Count).Min();
                    var first = allCategoriesInDefAndParents[0].ToList();
                    for (int i = 0; i < min; i++)
                    {
                        if (allCategoriesInDefAndParents.All(cat => cat[cat.Count - 1 - i] == first[first.Count - 1 - i]))
                        {
                            flavorDef.LowestCommonIngredientCategory = first[first.Count - 1 - i];
                            continue;
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error when building database of FinalFlavorDefs, error: {ex}");
        }
    }

    public static List<ThingCategoryDef> GetFilterCategories(ThingFilter filter, string name)
    {
        FieldInfo field = filter.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field != null)
        {
            List<ThingCategoryDef> list = [];
            try
            {
                if (field.GetValue(filter) != null)
                {
                    list = (from string categoryString in (List<string>)field.GetValue(filter) select DefDatabase<ThingCategoryDef>.GetNamed(categoryString)).ToList();
                }
            }
            catch 
            { 
                throw new Exception($"Could not examine {name} within the given filter.");
            }
            return list;
        }
        if (Prefs.DevMode) Log.Message("Filter contains no items or is null");
        return null;
    }


    // all FinalFlavorDefs that fit the given meal type and extra parameters
    public static IEnumerable<FlavorDef> ValidFlavorDefs(Thing meal)
    {
        var compFlavor = meal.TryGetComp<CompFlavor>();
        return ActiveFlavorDefs
            .Where(flavorDef =>
                flavorDef.MealCategories.Any(mealCategory => mealCategory.DescendantThingDefs.Contains(meal.def))
                && (compFlavor.CookingStation == null || flavorDef.CookingStations.NullOrEmpty() || flavorDef.CookingStations.Any(cat => cat.ContainedInThisOrDescendant(compFlavor.CookingStation)))
                && flavorDef.HoursOfDay.min <= compFlavor.HourOfDay && compFlavor.HourOfDay <= flavorDef.HoursOfDay.max
                && flavorDef.IngredientsHitPointPercentage.Includes(compFlavor.IngredientsHitPointPercentage));
        
    }
}
