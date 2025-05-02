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
    public int Specificity;  // about how many possible ingredients could fulfill this FlavorDef?

    public ThingCategoryDef LowestCommonIngredientCategory;  // lowest category that contains all the ingredients in the FlavorDef; used to optimize searches; defaults to flavorRoot

    // ReSharper disable once UnusedMember.Global
    public string VarietyTexture;  // texture to use from Food Texture Variety

    public List<ThingCategoryDef> MealCategories = [];  // what types of meals are allowed to have this FlavorDef; empty means basic meals (simple, fine, lavish)

    public List<ThingCategoryDef> CookingStations = [];  // which stations are allowed to cook this FlavorDef

    public IntRange HoursOfDay = new(0, 24);  // what hours of the day this FlavorDef can be completed during


    // about how many possible ingredients could fulfill each FlavorDef? add together all the specificities of all its categories; overlaps in categories will be counted multiple times
    // also calculate the lowest common category containing all ingredients for each FlavorDef
    public static void SetStaticData()
    {
        try
        {
            foreach (FlavorDef flavorDef in DefDatabase<FlavorDef>.AllDefs)
            {
                List<ThingCategoryDef> allAllowedCategories = [];
                foreach (IngredientCount ingredient in flavorDef.ingredients)
                {
                    // add together specificities of all categories (no overlap unless you wrote overlapping categories)
                    var allowedCategories = GetFilterCategories(ingredient.filter, "categories");
                    allAllowedCategories.AddRange(allowedCategories);
                    if (!allowedCategories.NullOrEmpty())
                    {
                        // specificities of categories
                        foreach (ThingCategoryDef allowedCategory in allowedCategories)
                        {
                            flavorDef.Specificity += allowedCategory.GetModExtension<FlavorCategoryModExtension>().Specificity;
                        }
                        // more specific if it has a required cooking station
                        if (!flavorDef.CookingStations.NullOrEmpty())
                        {
                            flavorDef.Specificity -= 1;
                        }
                        // more specific if it has a required cooking time of day
                        if (flavorDef.HoursOfDay != new IntRange(0, 24))
                        {
                            flavorDef.Specificity -= 1;
                        }
                    }
                    else 
                    { 
                        throw new Exception("No allowed categories when building FlavorDef static data");
                    }

                    // subtract # of disallowed ingredient ThingDefs to specificity (more disallowed means more specific)
                    List<ThingCategoryDef> disallowedCategories = GetFilterCategories(ingredient.filter, "disallowedCategories");
                    if (!disallowedCategories.NullOrEmpty())
                    {
                        foreach (ThingCategoryDef disallowedCategory in disallowedCategories)
                        {
                            flavorDef.Specificity -= disallowedCategory.GetModExtension<FlavorCategoryModExtension>().Specificity;
                        }
                    }
                }

                // calculate the lowest category containing all the ingredients in the FlavorDef
                flavorDef.LowestCommonIngredientCategory = ThingCategoryDefUtility.flavorRoot;
                var allCategoriesInDefParents = (from ThingCategoryDef category in allAllowedCategories select category.Parents.ToList()).ToList();
                if (!allCategoriesInDefParents.NullOrEmpty())
                {
                    int min = (from List<ThingCategoryDef> parents in allCategoriesInDefParents select parents.Count).Min();
                    var first = allCategoriesInDefParents[0];
                    for (int i = 0; i < min; i++)
                    {
                        // if the current index (going from last to first) has the same value in each list, that's the current lowest common category
                        // if not, you're done searching and the previous stored common category is the absolute lowest
                        if (allCategoriesInDefParents.All(cat => cat[cat.Count - 1 - i] == first[first.Count - 1 - i]))
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

    public static List<ThingCategoryDef> GetFilterCategories(ThingFilter filter)  // get all ThingCategoryDefs within the given filter
    {
        FieldInfo categories = filter.GetType().GetField("categories", BindingFlags.NonPublic | BindingFlags.Instance);  // what type of field is it
        if (categories != null)
        {
            List<ThingCategoryDef> categoriesList = (from string categoryString in (List<string>)categories.GetValue(filter) select DefDatabase<ThingCategoryDef>.GetNamed(categoryString)).ToList();
            return categoriesList;
        }
        if (Prefs.DevMode) Log.Message("filter contains no ThingCategoryDefs");
        return null;
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
}
