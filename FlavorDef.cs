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
///     show what combination of ingredients/categories are needed for each particular name
/// </summary>
/// 
public class FlavorDef : RecipeDef
{
    public int specificity = 0;


    // set the specificity by seeing how many possible different ingredients could fit into the FlavorDef (fewer is more specific); overlaps are counted
    public static void SetSpecificities()
    {
        foreach (FlavorDef flavorDef in DefDatabase<FlavorDef>.AllDefs)
        {
            foreach (IngredientCount ingredient in flavorDef.ingredients)
            {
                List<string> categories = FlavorDef.GetFilterCategories(ingredient.filter);
                if (categories != null)
                {
                    foreach (string categoryString in categories)
                    {
                        ThingCategoryDef thingCategoryDef = DefDatabase<ThingCategoryDef>.GetNamed(categoryString);
                        List<ThingDef> allDefs = thingCategoryDef.DescendantThingDefs.ToList();
                        allDefs.RemoveDuplicates();
                        flavorDef.specificity += allDefs.Count();
                    }

                }
            }

            flavorDef.defName = Remove.RemoveDiacritics(flavorDef.defName);
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
