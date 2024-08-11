using System.Collections.Generic;
using System.Reflection;
using Verse;
using HarmonyLib;
using RimWorld;
using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace FlavorText
{

    public class FlavorDef : RecipeDef
    {
        public int totalDepth;  // total score of how deep matching ThingCategoryDefs are in their tree, used to pick best FlavorDef

/*        public List<string> flavorCategories;

        private List<string> GetFlavorCategories()
        {
            List<string> categoriesList = [];
            foreach (IngredientCount ing in ingredients)
            {
                for (int f = 0; f < ing.filter.AllowedDefCount; f++)
                {
                    // find the categories of the FlavorDef and make them into a new list
                    FieldInfo categoriesField = AccessTools.Field(typeof(ThingFilter), "categories");
                    categoriesList = (List<string>)categoriesField.GetValue(categoriesField);
                }
            }
            return categoriesList;

        }

        public FlavorDef()
        {
            flavorCategories = this.GetFlavorCategories();
        }*/
    }
}
