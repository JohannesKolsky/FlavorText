using System.Collections.Generic;
using System.Reflection;
using Verse;
using RimWorld;
using System.Globalization;
using System.Text;
using System.Linq;

//TODO: recipe parent hierarchy
//TODO: stuffCategoriesToAllow?
//TODO: spreadsheet descriptions are misaligned


namespace FlavorText
{
    /// <summary>
    /// Effectively recipes, and is a subclass of RecipeDef. These show what combination of ingredients/categories will be given each particular name
    /// </summary>
    /// 
    public class FlavorDef : RecipeDef
    {
        public int specificity = 0;
        public override void ResolveReferences()
        {
            base.ResolveReferences();
            foreach (IngredientCount ingredient in ingredients)
            {
                List<string> categories = GetFilterCategories(ingredient.filter);
                if (categories != null)
                {
                    foreach (string categoryString in categories)
                    {
                        ThingCategoryDef thingCategoryDef = DefDatabase<ThingCategoryDef>.GetNamed(categoryString);
                        while (true)
                        {
                            specificity += 1;
                            if (thingCategoryDef.childCategories.NullOrEmpty() || thingCategoryDef.childCategories.Count == 0) { break; }
                            thingCategoryDef = thingCategoryDef.childCategories[0];
                        }
                    }

                }
            }

            defName = Remove.RemoveDiacritics(defName);

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
                List<ThingDef> thingDefsList = (List<ThingDef>)thingDefs.GetValue(filter);  // knowing its type, find the field value in filter
                return thingDefsList;
            }
            Log.Message("filter contains no ThingDefs");
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
}
