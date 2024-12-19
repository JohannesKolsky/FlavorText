using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using Verse;

//--TODO: recipe parent hierarchy
//--TODO: spreadsheet descriptions are misaligned
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
                List<string> categories = flavorDef.GetFilterCategories(ingredient.filter);
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

    public List<string> GetFilterCategories(ThingFilter filter)  // get all ThingCategoryDefs within the given filter
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

    public List<ThingDef> GetFilterIngredients(ThingFilter filter)  // get all ThingDefs from within the given filter
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

public static class Remove  //TODO: add more special chars, like ø
{
    public static string RemoveDiacritics(string stIn)
    {
        string stFormD = stIn.Normalize(NormalizationForm.FormD);
        StringBuilder sb = new StringBuilder();

        for (int ich = 0; ich < stFormD.Length; ich++)
        {
            UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(stFormD[ich]);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(stFormD[ich]);
            }
        }

        return (sb.ToString().Normalize(NormalizationForm.FormC));
    }
}
