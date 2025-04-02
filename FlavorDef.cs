using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using Verse.Noise;

//--TODO: recipe parent hierarchy
//--TODO: spreadsheet descriptions are misaligned

//TODO: blank ingredient option


namespace FlavorText;

/// <summary>
///     Effectively recipes
///     show what combination of ingredients/categories are needed for each particular flavor label
/// </summary>
/// 
public class FlavorDef : RecipeDef
{
    public int specificity;  // about how many possible ingredients could fulfill this FlavorDef?

    public ThingCategoryDef lowestCommonIngredientCategory;  // lowest category that contains all the ingredients in the FlavorDef; used to optimize searches; defaults to flavorRoot


    // about how many possible ingredients could fulfill each FlavorDef? add together all the specificities of all its categories; overlaps in categories will be counted multiple times
    // also calculate the lowest common category containing all ingredients for each FlavorDef
    static public void SetStaticData()
    {
        try
        {
            foreach (FlavorDef flavorDef in DefDatabase<FlavorDef>.AllDefs)
            {
                List<ThingCategoryDef> allAllowedCategories = [];
                foreach (IngredientCount ingredient in flavorDef.ingredients)
                {
                    // add # of allowed ingredient ThingDefs to specificity (more allowed means less specific)
                    var allowedCategories = GetFilterCategories(ingredient.filter, "categories");
                    allAllowedCategories.AddRange(allowedCategories);
                    if (!allowedCategories.NullOrEmpty())
                    {
                        foreach (ThingCategoryDef allowedCategory in allowedCategories)
                        {
                            flavorDef.specificity += allowedCategory.GetModExtension<FlavorCategoryModExtension>().specificity;
                        } 
                    }
                    else { Log.Error("Error: no allowed categories when building FlavorDef static data"); }

                    // subtract # of disallowed ingredient ThingDefs to specificity (more disallowed means more specific)
                    List<ThingCategoryDef> disallowedCategories = GetFilterCategories(ingredient.filter, "disallowedCategories");
                    if (!disallowedCategories.NullOrEmpty())
                    {
                        foreach (ThingCategoryDef disallowedCategory in disallowedCategories)
                        {
                            flavorDef.specificity -= disallowedCategory.GetModExtension<FlavorCategoryModExtension>().specificity;
                        }
                    }
                }

                // calculate the lowest category containing all the ingredients in the FlavorDef
                flavorDef.lowestCommonIngredientCategory = ThingCategoryDefUtilities.flavorRoot;
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
                            flavorDef.lowestCommonIngredientCategory = first[first.Count - 1 - i];
                            continue;
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Error when building database of FlavorDefs, error: {ex}");
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
        Log.Message("filter contains no ThingCategoryDefs");
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
            catch { Log.Error($"Could not examine {name} within the given filter."); return null; }
            return list;
        }
        Log.Message("Filter contains no items or is null");
        return null;
    }
}
