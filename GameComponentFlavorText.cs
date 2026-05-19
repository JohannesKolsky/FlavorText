using LudeonTK;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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
//DONE: does this work when there is no map?

namespace FlavorText
{
/*
    public delegate void Notify();*/

    public class GameComponentFlavorText : GameComponent
    {
        /*        public event Notify RecipeDatabaseNotifyMods;

                protected virtual void OnRecipeDatabaseNotifyMods()
                {
                    RecipeDatabaseNotifyMods?.Invoke();
                }*/

        public static GameComponentFlavorText instance;

        public GameComponentFlavorText(Game game)
        {
            instance = this;
        }

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


        public static Dictionary<ThingDef, List<ThingDef>> MealRecipeDatabase = [];  // dictionary of what actual recipes (not FlavorDefs) are used for what meals

        internal static void BuildMealRecipeDatabase()
        {
            Vanilla_BuildMealRecipeDatabase();
/*            if (ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core")) VEF_BuildMealRecipeDatabase();
            if (ModsConfig.IsActive("syrchalis.processor.framework")) SYR_BuildMealRecipeDatabase();*/
        }

        private static void Vanilla_BuildMealRecipeDatabase()
        {
            foreach (var meal in FlavorCategoryDefOf.FT_MealsWithCompFlavor.DescendantThingDefs)
            {
                var allowedThingDefs = AllRecipesWithThisAsProduct(meal);
                if (!allowedThingDefs.Empty()) MealRecipeDatabase.Add(meal, allowedThingDefs);
            }
        }


        //TODO: this doesn't cover processes, but how often is that really needed?
        private static List<ThingDef> AllRecipesWithThisAsProduct(ThingDef def)
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
            List<ThingDef> allowedThingDefs = [.. recipesForThing.SelectMany(recipe => recipe.ingredients.SelectMany(slot => slot.filter.AllowedThingDefs))];
            allowedThingDefs.RemoveDuplicates();
            if (recipesForThing.Count > 0 && allowedThingDefs.Empty()) throw new NullReferenceException($"{def.ToStringSafe()} had {recipesForThing.Count} recipes, but across all those recipes there were no allowedThingDefs to use.");
            else return allowedThingDefs;
        }

        [DebugAction("Flavor Text", null, false, false, false, false, false, 0, false, actionType = DebugActionType.ToolMap, allowedGameStates = AllowedGameStates.PlayingOnMap, name = "Spawn a meal for each FlavorDef")]
        private static void Debug_SpawnMealsWithAllFlavorDefs()
        {
            foreach (FlavorDef flavorDef in FlavorDef.ActiveFlavorDefs)
            {
                Thing meal = ThingMaker.MakeThing(ThingDefOf.MealSimple);
                CompFlavor compFlavor = meal.TryGetComp<CompFlavor>();
                compFlavor.excludedCategories = [];
                CompIngredients compIngredients = meal.TryGetComp<CompIngredients>();
                List<int> slotIndices = [.. Enumerable.Range(0, flavorDef.ingredients.Count)];

                for (int i = 0; i < flavorDef.ingredients.Count; i++)
                {
                    Log.Message($"checking [{flavorDef.ingredients[i].Categories.ToStringSafeEnumerable()}]");
                    compIngredients.RegisterIngredient(compFlavor.GenerateGhostIngredientsManuallyDebug((flavorDef, slotIndices), compIngredients.ingredients, i, flavorDef.ingredients[i]));

                }
                compFlavor.TryGetFlavorText([flavorDef]);
                GenPlace.TryPlaceThing(meal, UI.MouseCell(), Find.CurrentMap, ThingPlaceMode.Near, extraValidator: (IntVec3 c) => c.GetAllItemsStackCount(Find.CurrentMap, meal.def) == 0);
            }
        }

        /*       // add VEF process products to database of meal recipes
               public static void VEF_BuildMealRecipeDatabase()
               {
                   Log.Warning("VEF_BuildMealRecipeDatabase");
                   foreach (var meal in FlavorCategoryDefOf.FT_MealsWithCompFlavor.DescendantThingDefs)
                   {
                       var newAllowedThingDefs = VEF_AllProcessesWithThisAsProduct(meal);
                       if (!newAllowedThingDefs.Empty())
                       {
                           Log.Message($"adding [{newAllowedThingDefs.ToStringSafeEnumerable()}] to {meal.ToStringSafe()}");
                           if (MealRecipeDatabase.TryGetValue(meal, out var currentAllowedThingDefs))
                           {
                               currentAllowedThingDefs.AddRangeUnique(newAllowedThingDefs);
                               MealRecipeDatabase[meal] = currentAllowedThingDefs;
                           }
                           else
                           {
                               MealRecipeDatabase.Add(meal, newAllowedThingDefs);
                           }
                       }
                   }
               }

               static List<ThingDef> VEF_AllProcessesWithThisAsProduct(ThingDef def)
               {
                   List<PipeSystem.ProcessDef> processesForThing = [];
                   List<PipeSystem.ProcessDef> allDefsListForReading = DefDatabase<PipeSystem.ProcessDef>.AllDefsListForReading;
                   for (int j = 0; j < allDefsListForReading.Count; j++)
                   {
                       if (allDefsListForReading[j].results != null && allDefsListForReading[j].results.Any(result => result.thing == def))
                       {
                           Log.Message($"adding {allDefsListForReading[j].ToStringSafe()} for {def.ToStringSafe()}");
                           processesForThing.Add(allDefsListForReading[j]);
                       }
                   }
                   List<ThingDef> allowedThingDefs = [.. processesForThing.SelectMany(process => process.ingredients.Select(ing => ing.thing))];
                   allowedThingDefs.RemoveDuplicates();
                   if (processesForThing.Count > 0 && allowedThingDefs.Empty()) throw new NullReferenceException($"{def.ToStringSafe()} had {processesForThing.Count} processes, but across all those processes there were no allowedThingDefs to use.");
                   else return allowedThingDefs;
               }




               // add SYR process products to database of meal recipes
               public static void SYR_BuildMealRecipeDatabase()
               {
                   Log.Warning($"SYR_BuildMealRecipeDatabase");
                   foreach (var meal in FlavorCategoryDefOf.FT_MealsWithCompFlavor.DescendantThingDefs)
                   {
                       var newAllowedThingDefs = SYR_AllProcessesWithThisAsProduct(meal);
                       if (!newAllowedThingDefs.Empty())
                       {
                           if (MealRecipeDatabase.TryGetValue(meal, out var currentAllowedThingDefs))
                           {
                               currentAllowedThingDefs.AddRangeUnique(newAllowedThingDefs);
                               MealRecipeDatabase[meal] = currentAllowedThingDefs;
                           }
                           else
                           {
                               MealRecipeDatabase.Add(meal, newAllowedThingDefs);
                           }
                       }
                   }
               }


               static List<ThingDef> SYR_AllProcessesWithThisAsProduct(ThingDef def)
               {
                   List<ProcessorFramework.ProcessDef> processesForThing = [];
                   List<ProcessorFramework.ProcessDef> allDefsListForReading = DefDatabase<ProcessorFramework.ProcessDef>.AllDefsListForReading;
                   for (int j = 0; j < allDefsListForReading.Count; j++)
                   {
                       if (allDefsListForReading[j].thingDef != null && allDefsListForReading[j].thingDef == def)
                       {
                           processesForThing.Add(allDefsListForReading[j]);
                       }
                   }
                   List<ThingDef> allowedThingDefs = [.. processesForThing.SelectMany(process => process.ingredientFilter.AllowedThingDefs)];
                   allowedThingDefs.RemoveDuplicates();
                   if (processesForThing.Count > 0 && allowedThingDefs.Empty()) throw new NullReferenceException($"{def.ToStringSafe()} had {processesForThing.Count} processes, but across all those processes there were no allowedThingDefs to use.");
                   else return allowedThingDefs;
               }
       */
    }

/*    public class FlavorData
    {
        public int? iteration;
        public int? hourOfDay;
        public int? tickCreated;
        public string cookID;
        public ThingDef cookingStation;
        public List<string> mealTags;

        public FlavorData() { }

        public FlavorData(CompFlavor compFlavor)
        {
            iteration = compFlavor.Iteration;
            hourOfDay = compFlavor.HourOfDay;
            tickCreated = compFlavor.TickCreated;
            cookingStation = compFlavor.CookingStation;
            cookID = compFlavor.CookID;
            mealTags = compFlavor.MealTags;

        }

*//*        public void ExposeData()
        {
            Scribe_Values.Look(ref iteration, "iteration");
            Scribe_Values.Look(ref cookID, "cookID");
            Scribe_Collections.Look(ref mealTags, "tags", LookMode.Undefined);
        }*//*
    }*/
}
