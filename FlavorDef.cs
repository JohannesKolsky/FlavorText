using JetBrains.Annotations;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using System.Reflection;
using System.Text;
using System.Collections;
using System.Threading.Tasks;

//TODO: recipe parent hierarchy
//TODO: stuffCategoriesToAllow?

namespace FlavorText
{

    /// <summary>
    /// Effectively recipes, and is a subclass of RecipeDef. These show what combination of ingredients/ingredient categories will be given each particular name.
    /// </summary>
    /// 
    public class FlavorDef : RecipeDef
    {
/*        public List<FlavorIngredientCount> ingredients = new();*/

        public int specificity = -1;
/*        public FlavorDef()
        {
            foreach (FlavorIngredientCount ingredientCount in this.ingredients)  // calculate how many ThingDefs satisfy this flavor name, the fewer the more specific and therefore better the flavor name is
            {
                specificity += ingredientCount.filter.AllowedDefCount;
            }
            *//*Type typ = typeof(FlavorDef);*//*
        }  */     
        
        public FlavorDef()
        {
            foreach (IngredientCount ingredientCount in this.ingredients)  // calculate how many ThingDefs satisfy this flavor name, the fewer the more specific and therefore better the flavor name is
            {
                specificity += ingredientCount.filter.AllowedDefCount;
            }
            /*Type typ = typeof(FlavorDef);*/
        }




        private List<ThingCategoryDef> CategoryChain(ThingDef thingDef)  // make a list from the current ingredient to the Root of ThingCategories
        {
            if (thingDef.thingCategories.Count > 1) // DEBUG: if ingredient has multiple categories make an Error Log of which ones they are
            {
                string allCats = "";
                for (int i = 0; i < thingDef.thingCategories.Count; i++)
                {
                    allCats += thingDef.thingCategories[i].defName;
                    if (i != thingDef.thingCategories.Count) { allCats += ", "; }
                }
                Log.Error("ingredient belongs in multiple categories: " + allCats);
            }

            List<ThingCategoryDef> categoryChain = [];
            ThingCategoryDef currentCat = thingDef.thingCategories.FirstOrDefault();  // (probably will need to change this allow multiple categories)
            while (currentCat != DefDatabase<ThingCategoryDef>.GetNamed("Root"))
            {
                categoryChain.Add(currentCat);
                currentCat = currentCat.parent;
            }
            return categoryChain;
        }

        private List<ThingCategoryDef> CategoryChain(ThingCategoryDef currentCat)  // make a list from the current ingredient to the Root of ThingCategories
        {
            List<ThingCategoryDef> categoryChain = [];
            while (currentCat != DefDatabase<ThingCategoryDef>.GetNamed("Root"))
            {
                categoryChain.Add(currentCat);
                currentCat = currentCat.parent;
            }
            return categoryChain;
        }
    }
}
