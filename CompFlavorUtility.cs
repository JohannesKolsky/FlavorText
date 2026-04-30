using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

//DONE: CompFlavorData constructor not found error on load of save
//DONE: error on adding FlavorText while meal is processing, then taking it out when finished
//DONE: saving mid-processing crashes RW
//DONE: processor destroyed or uninstalled mid-process
//--TODO: ruined soup  // disappears
//DONE: LookMode.Reference errror for compFlavor
//DONE: process doesn't get removed when map is destroyed
//DONE: multi-map: need separate CompFlavorUtilities
//DONE: iterations seems to be resetting to 0

//TODO: does this work when there is no map?

namespace FlavorText
{
    public class CompFlavorUtility(Map map) : MapComponent(map)
    {
        private static int iterations = 0;
        public static int Iterations { get => iterations; private set => iterations = value; }

        internal static int Iterate()
        {
            Iterations++;
            return Iterations;
        }

/*        private Dictionary<string, CompFlavorData> activeProcesses = [];
        public Dictionary<string, CompFlavorData> ActiveProcesses { get => activeProcesses; }*/



        public override void ExposeData()
        {
            Scribe_Values.Look(ref iterations, "iterations");
            //Scribe_Collections.Look(ref activeProcesses, "activeProcesses", LookMode.Value, LookMode.Deep);
        }


        internal static Dictionary<ThingDef, List<ThingDef>> MealRecipeDatabase = [];  // dictionary of what actual recipes (not FlavorDefs) are used for what meals

        internal static void BuildMealRecipeDatabase()
        {
            foreach (var meal in FlavorCategoryDefOf.FT_MealsWithCompFlavor.DescendantThingDefs)
            {
                List<RecipeDef> recipesForMeal = AllRecipesWithThisAsProduct(meal);
                List<ThingDef> allowedThingDefs = [.. recipesForMeal.SelectMany(recipe => recipe.ingredients.SelectMany(slot => slot.filter.AllowedThingDefs))];
                allowedThingDefs.RemoveDuplicates();
                if (recipesForMeal.Count > 0 && allowedThingDefs.Empty()) throw new NullReferenceException($"{meal.ToStringSafe()} had {recipesForMeal.Count} recipes, but across all those recipes there were no allowedThingDefs to use.");
                else MealRecipeDatabase.Add(meal, allowedThingDefs);
            }
        }


        //TODO: this doesn't cover processes, but how often is that really needed?
        private static List<RecipeDef> AllRecipesWithThisAsProduct(ThingDef def)
        {
            List<RecipeDef> recipesForThing = [];
            List<RecipeDef> allDefsListForReading = DefDatabase<RecipeDef>.AllDefsListForReading;
            for (int j = 0; j < allDefsListForReading.Count; j++)
            {
                if (allDefsListForReading[j].products != null && allDefsListForReading[j].products.Any(product => product.thingDef == def))
                {
                    recipesForThing.Add(allDefsListForReading[j]);
                }
            }
            return recipesForThing;
        }
    }

/*    public class CompFlavorData : IExposable
    {
    public int? iteration;
    public string cookID;
    public List<string> mealTags;

        public CompFlavorData() { }

        public CompFlavorData(CompFlavor compFlavor)
        {
            iteration = compFlavor.Iteration;
            cookID = compFlavor.CookID;
            mealTags = compFlavor.MealTags;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref iteration, "iteration");
            Scribe_Values.Look(ref cookID, "cookID");
            Scribe_Collections.Look(ref mealTags, "tags", LookMode.Undefined);
        }
    }*/
}
