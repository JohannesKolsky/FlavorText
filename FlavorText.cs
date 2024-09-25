using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using static System.Text.RegularExpressions.Regex;

//--TODO: make flavor entries a class
//--TODO: eggs + eggs makes weird names like omlette w/eggs
//--TODO: move databases to xml
//--TODO: dynamically build flavor database from all foodstuffs
//--TODO: organize flavors tightly: category-->category-->...-->item defName
//--TODO: flavor label does not appear while meal is being carried directly from stove to stockpile
//--TODO: function to remove duplicate ingredients
//--TODO: default to vanilla name on null


//TODO: merging stacks doesn't change the meal name
//TODO: options to prevent merging meals
//TODO: big-ass null check error randomly
//TOTO: flavor descriptions

//fixedIngredientFilter: which items are allowed
//defaultIngredientFilter: default assignment of fixedIngredientFilter
//fixedIngredient: used if fixedIngredientFilter.Count == 1


namespace FlavorText
{
    /*    [StaticConstructorOnStartup]
        public static class HarmonyPatches
        {
            private static readonly Type patchType = typeof(HarmonyPatches);

            static HarmonyPatches()
            {
                var harmony = new Harmony(id: "rimworld.hekmo.flavortext");
                Harmony.DEBUG = true;

                harmony.Patch(AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.categories)), postfix: new HarmonyMethod(patchType, nameof(ThingFilter.customSummary)));
            }
        }*/

    public class CompFlavor : ThingComp
    {

        public CompProperties_Flavor Props => (CompProperties_Flavor)this.props;

        public FlavorDef bestFlavorDef;  // best-matching FlavorDef
        public string flavorLabel;  // best label
        List<List<ThingDef>> thingDefs;  // thingDefs of bestFlavorDef recipe
        List<List<string>> categories;  // categories of bestFlavorDef recipe

        private const int MaxNumIngredientsFlavor = CompIngredients.MaxNumIngredients;  // max number of ingredients used to find flavors

        public static readonly List<FlavorDef> flavorDefList = (List<FlavorDef>)DefDatabase<FlavorDef>.AllDefs;  // compile a list of all FlavorDefs

        public (FlavorDef, List<int>) GetFlavor(List<ThingDef> ingredientsToSearchFor, List<FlavorDef> flavorDefList)  // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
        {
            if (ingredientsToSearchFor == null)
            {
                Log.Error("Ingredients to search for are null");
                return (null, null);
            }
            else if (flavorDefList == null)
            {
                Log.Error("List of Flavor Defs is null");
                return (null, null);
            }
            else if (flavorDefList.Count == 0)
            {
                Log.Error("List of Flavor Defs is empty");
                return (null, null);
            }

            //see which FlavorDefs match with the ingredients in the meal, and make a dictionary of them all with a note on the order of FlavorDef recipe ingredients they matched with
            List<(FlavorDef, List<int>)> matchingFlavorDefs = [];
            foreach (FlavorDef flavorDef in flavorDefList)
            {
                List<int> matches = IsMatchingFlavorDef(ingredientsToSearchFor, flavorDef);
                 if (matches != null && matches.Count == ingredientsToSearchFor.Count)
                {
                    matchingFlavorDefs.Add((flavorDef, matches));
                }
            }

            (FlavorDef, List<int>) bestFlavorDef = (null, null);

            // pick the most specific matching FlavorDef
            if (matchingFlavorDefs.Count > 0)
            {
                bestFlavorDef = ChooseBestFlavorDef(matchingFlavorDefs);
            }

            // fill in placeholder names in the label
            if (bestFlavorDef != (null, null))
            {
                return bestFlavorDef;
            }
            else { Log.Error("No matching FlavorDefs found."); return (null, null); }
        }

        private List<int> IsMatchingFlavorDef(List<ThingDef> ingredientsToSearchFor, FlavorDef flavorDef)  // check if all the ingredients match the given FlavorDef
        {
            if (ingredientsToSearchFor.Count == flavorDef.ingredients.Count)  // FlavorDef can't have more ingredients than the meal has
            {
                List<int> matches = new(ingredientsToSearchFor.Count);
                for (int i = 0; i < ingredientsToSearchFor.Count; i++) // check each ingredient you're searching for with the FlavorDef
                {
                     matches = BestIngredientMatch(ingredientsToSearchFor[i], flavorDef, matches);  // see if the ingredient fits and if it does, find the most specific match
                }
                if (!matches.Contains(-1))
                {
                    return matches; // the FlavorDef matches completely
                }
            }
            return null;  // the FlavorDef doesn't match completely
        }

        private List<int> BestIngredientMatch(ThingDef ingredient, FlavorDef flavorDef, List<int> matches)  // find the best match for the current single ingredient in the current FlavorDef
        {
            int bestIndex = -1;

            for (int index = 0; index < flavorDef.ingredients.Count; index++)  // compare the given ingredient to the FlavorDef's ingredients to see which it matches best with
            {
                if (matches.Contains(index)) { continue; }  // if this FlavorDef ingredient was already matched with, skip it
                if (flavorDef.ingredients[index].filter.Allows(ingredient))
                {
                    // if you matched with a fixed ingredient, that's the best
                    if (flavorDef.ingredients[index].IsFixedIngredient)
                    {
                        bestIndex = index;
                        break;
                    }
                    else if (bestIndex != -1)
                    {
                        // if the current FlavorDef ingredient is the most specific so far, mark its index
                        if (flavorDef.ingredients[index].filter.AllowedDefCount < flavorDef.ingredients[bestIndex].filter.AllowedDefCount)
                        {
                            bestIndex = index;
                        }
                    }
                    else { bestIndex = index; }  // if this is the first match, mark the index
                }
            }
            matches.Add(bestIndex);
            return matches;
        }

        private (FlavorDef, List<int>) ChooseBestFlavorDef(List<(FlavorDef, List<int>)> matchingFlavorDefs)  // rank valid flavor defs to choose the best
        {
            if (matchingFlavorDefs != null && matchingFlavorDefs.Count != 0)
            {
                matchingFlavorDefs.SortByDescending(entry => entry.Item1.specificity);
                return matchingFlavorDefs.Last();
            }
            Log.Error("no valid flavorDefs to choose from");
            return (null, null);
        }

        private string FillInCategories(FlavorDef bestFlavorDef, List<int> indices, List<ThingDef> ingredients)  // replace placeholder categories with the corresponding ingredient names
        {
            string flavor = bestFlavorDef.label;
            for (int i = 0; i < ingredients.Count; i++)
            {
                // (1, 2, 0)
                // (PlantFoodRaw, Milk, FoodRaw)
                // (Milk, MeatThrumbo, Berries)
                int index = indices[i];  // which recipe filter the ingredient matches with
                List<string> categories = bestFlavorDef.GetRecipeCategories(bestFlavorDef.ingredients[index].filter);
                if (categories != null)
                {
                    flavor = flavor.Formatted(ingredients[i].Named(categories[0]));  // TODO: extend to allow multiple categories //TODO: this doesn't actually replace atm, but looks like internally it's working
                }
            }
            return flavor;
        }

        //TODO: currently this changes the entire ThingDef label
        private List<ThingDef> CleanupIngredientList(List<ThingDef> ingredients)  // remove unnecessary bits of the ingredient labels, like "meat"
        {
            foreach (ThingDef ingredient in ingredients)
            {
                ingredient.label = Regex.Replace(ingredient.label, " meat$","");  // remove "meat" endings
                foreach (ThingCategoryDef thingCategoryDef in ingredient.thingCategories)  // label all eggs as "egg"
                {
                    if (thingCategoryDef.label == "EggsUnfertilized" || thingCategoryDef.label == "EggsFertilized")
                    {
                        ingredient.label = "egg";
                    }
                }
                switch (ingredient.defName)
                {
                    case "Meat_Twisted":
                        ingredient.label = "twisted meat";
                        break;

                    case "Meat_Human":
                        ingredient.label = "long pork";
                        break;

                    case "Meat_Megaspider":
                        ingredient.label = "bug";
                        break;


                }
            }
            return ingredients; 
        }

        private string CleanupFlavorLabel(string flavorLabel)  //TODO: use this to clean up the label based on context of the other words
        {
            return flavorLabel;
        }


        //find a flavorLabel and apply it to the parent meal
        public override string TransformLabel(string label)
        {
            if (flavorLabel == null)  // if no flavorLabel, find one
            {
                List<ThingDef> ingredientList = parent.GetComp<CompIngredients>().ingredients;  // get a list of ingredients from the parent meal
                (FlavorDef bestFlavorDef, List<int> indices) = GetFlavor(ingredientList, flavorDefList);  // get single best Flavor Def from all possible matching Flavor Defs
                if (bestFlavorDef != null)
                {
                    ingredientList = CleanupIngredientList(ingredientList);  // change ingredient labels to fit better in the upcoming flavorLabel
                    string bestFlavor = FillInCategories(bestFlavorDef, indices, ingredientList);  // replace category placeholder names with the corresponding ingredient in the meal
                    flavorLabel = GenText.CapitalizeAsTitle(bestFlavor);
                }
                else { Log.Error("no suitable FlavorDef found"); }
            }
            return flavorLabel;
        }

        public override string CompInspectStringExtra()  // if you've successfully created a new flavor label, move the original name down
        {
            if (bestFlavorDef != null)
            {
                StringBuilder stringBuilder = new();
                string typeLabel = GenText.CapitalizeAsTitle(parent.def.label);  // what kind of meal it is
                stringBuilder.AppendLine(typeLabel);
                return stringBuilder.ToString().TrimEndNewlines();
            }
            else { return null; }
        }

        public override string GetDescriptionPart()
        {
            if (bestFlavorDef == null) { return null; }
            return bestFlavorDef.description;
        }

        public override void PostExposeData()  // include the flavor label in game save files
        {
            base.PostExposeData();
            Scribe_Values.Look(ref flavorLabel, "flavorLabel");
        }
    }

    public class CompProperties_Flavor : CompProperties
    {

        public CompProperties_Flavor()
        {
            this.compClass = typeof(CompFlavor);
        }

        public CompProperties_Flavor(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}