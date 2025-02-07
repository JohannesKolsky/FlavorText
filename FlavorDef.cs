using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;

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

    public ThingCategoryDef lowestCommonIngredientCategory = ThingCategoryDefUtilities.flavorRoot;  // lowest category that contains all the ingredients in the FlavorDef; used to optimize searches; defaults to flavorRoot


    // about how many possible ingredients could fulfill each FlavorDef? add together all the specificities of all its categories; overlaps in categories will be counted multiple times
    // also calculate the lowest common category containing all ingredients for each FlavorDef
    static public void SetStaticData()
    {
        foreach (FlavorDef flavorDef in DefDatabase<FlavorDef>.AllDefs)
        {
            List<ThingCategoryDef> allCategoriesInDef = [];
            foreach (IngredientCount ingredient in flavorDef.ingredients)
            {
                List<ThingCategoryDef> categories = (from string categoryString in GetFilterCategories(ingredient.filter) select DefDatabase<ThingCategoryDef>.GetNamed(categoryString)).ToList();
                allCategoriesInDef.AddRange(categories);
                foreach (ThingCategoryDef category in categories)
                {
                    flavorDef.specificity += category.GetModExtension<FlavorCategoryModExtension>().specificity;
                }
            }

            // calculate the lowest category containing all the ingredients in the FlavorDef
            var allCategoriesInDefParents = (from ThingCategoryDef category in allCategoriesInDef select category.Parents.ToList()).ToList();
            if (!allCategoriesInDefParents.NullOrEmpty())
            {
                var first = allCategoriesInDefParents[0];
                for (int i = 0; ; i++)
                {
                    // if the current index (going from last to first) has the same value in each list, that's the current lowest common category
                    // if not, you're done searching and the previous stored common category is the ultimate lowest
                    if (allCategoriesInDefParents.All(cat => cat[cat.Count - i] == first[first.Count - i]))
                    {
                        flavorDef.lowestCommonIngredientCategory = first[first.Count - i];
                        continue;
                    }
                    break;
                }
            }
        }
    }

    public static List<string> GetFilterCategories(ThingFilter filter)  // get all ThingCategoryDefs within the given filter
    {
        FieldInfo categories = filter.GetType().GetField("categories", BindingFlags.NonPublic | BindingFlags.Instance);  // what type of field is it
        if (categories != null)
        {
            List<string> categoriesList = (List<string>)categories.GetValue(filter);  // knowing its type, find the field value in filter
            return categoriesList;
        }
        Log.Message("filter contains no ThingCategoryDefs");
        return null;
    }

    public static List<ThingDef> GetFilterThingDefs(ThingFilter filter)  // get all ThingDefs from within the given filter
    {
        FieldInfo thingDefs = filter.GetType().GetField("thingDefs", BindingFlags.NonPublic | BindingFlags.Instance);  // what type of field is it
        if (thingDefs != null)
        {
            List<ThingDef> thingDefsList = [];
            try  // knowing its type, find the field value in filter}
            {
                thingDefsList = (List<ThingDef>)thingDefs.GetValue(filter);
            }
            catch { Log.Error("Could not examine thingDefs using filter."); return null; }
            return thingDefsList;
        }
        Log.Message("Filter contains no ThingDefs or is null.");
        return null;
    }
}
