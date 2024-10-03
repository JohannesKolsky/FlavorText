using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;

//--TODO: make flavor entries a class
//--TODO: eggs + eggs makes weird names like omlette w/eggs
//--TODO: move databases to xml
//--TODO: dynamically build flavor database from all foodstuffs
//--TODO: organize flavors tightly: category-->category-->...-->item defName
//--TODO: flavor label does not appear while meal is being carried directly from stove to stockpile
//--TODO: function to remove duplicate ingredients
//--TODO: default to vanilla name on null
//--TODO: formatted strings  //--TODO: track which ingredients will go into a placeholder
//--TODO: store indices and everything else directly in fields of CompFlavor


//TODO: merging stacks doesn't change the meal name
//TODO: options to prevent merging meals
//TODO: big-ass null check error randomly
//TODO: flavor descriptions
//TODO: handle arbitrary # ingredients: give each ingredient ranked-choice of what it matches with; assign most accurate matches first; if ingredients remain run again with remainder; join all final labels together
//TODO: put tags for empty flavor descriptions

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
    public class MatchingFlavor(FlavorDef flavorDefArg, List<int> indicesArg, List<bool> placeholdersArg)  // a FlavorDef and some info on how it matches with the meal's ingredients
    {
        public FlavorDef def = flavorDefArg;
        public List<int> indices = indicesArg;
        public List<bool> placeholders = placeholdersArg;
    }

    public class CompFlavor : ThingComp
    {

        public CompProperties_Flavor Props => (CompProperties_Flavor)props;

        List<ThingDef> ingredients;  // ingredient list of the meal

        public MatchingFlavor bestFlavor;  // best-matching FlavorDef and some info about how it matches
        public string finalFlavorLabel; //final human-readable label for the meal

        private const int MaxNumIngredientsFlavor = CompIngredients.MaxNumIngredients;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite

        public static readonly List<FlavorDef> flavorDefList = (List<FlavorDef>)DefDatabase<FlavorDef>.AllDefs;  // compile a list of all FlavorDefs

        public void AcquireFlavor(List<ThingDef> ingredientsToSearchFor, List<FlavorDef> flavorDefList)  // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
        {
            if (ingredientsToSearchFor == null)
            {
                Log.Error("Ingredients to search for are null");
                return;
            }
            else if (flavorDefList == null)
            {
                Log.Error("List of Flavor Defs is null");
                return;
            }
            else if (flavorDefList.Count == 0)
            {
                Log.Error("List of Flavor Defs is empty");
                return;
            }

            //see which FlavorDefs match with the ingredients in the meal
            List<MatchingFlavor> matchingFlavors = [];
            foreach (FlavorDef flavorDef in flavorDefList)
            {

                MatchingFlavor matchingFlavor = IsMatchingFlavor(ingredientsToSearchFor, flavorDef);
                if (matchingFlavor != null && matchingFlavor.indices.Count == ingredientsToSearchFor.Count)
                {
                    matchingFlavors.Add(matchingFlavor);
                }
            }

            // pick the most specific matching FlavorDef

            if (matchingFlavors.Count > 0)
            {

                ChooseBestFlavor(matchingFlavors);
            }

            // fill in placeholder names in the label
            if (bestFlavor.def != null)
            {
                return;
            }
            else { Log.Error("No matching FlavorDefs found."); return; }
        }

        private MatchingFlavor IsMatchingFlavor(List<ThingDef> ingredientsToSearchFor, FlavorDef flavorDef)  // check if all the ingredients match the given FlavorDef
        {
            MatchingFlavor matchingFlavor = new(flavorDef, [], []) { def = flavorDef };

            if (ingredientsToSearchFor.Count == matchingFlavor.def.ingredients.Count)  // FlavorDef recipe must be same # ingredients as meal
            {

                for (int i = 0; i < ingredientsToSearchFor.Count; i++) // check each ingredient you're searching for with the FlavorDef
                {


                    matchingFlavor = BestIngredientMatch(ingredientsToSearchFor[i], matchingFlavor);  // see if the ingredient fits and if it does, find the most specific match
                }
                if (!matchingFlavor.indices.Contains(-1))
                {
                    return matchingFlavor; // the FlavorDef matches completely
                }
            }
            return null;
        }

        private MatchingFlavor BestIngredientMatch(ThingDef ingredient, MatchingFlavor matchingFlavor)  // find the best match for the current single ingredient in the current FlavorDef
        {
            int lowestIndex = -1;
            bool needsPlaceholder = true;
            // (PlantFoodRaw, Milk, FoodRaw)
            // (1, 2, 0)
            // (false, true, true)
            // (Milk, MeatThrumbo, Berries)

            for (int j = 0; j < matchingFlavor.def.ingredients.Count; j++)  // compare the given ingredient to the FlavorDef's ingredients to see which it matches best with
            {
                if (matchingFlavor.indices.Contains(j)) { continue; }  // if this FlavorDef ingredient was already matched with, skip it
                if (matchingFlavor.def.ingredients[j].filter.Allows(ingredient))
                {
                    // if you matched with a fixed ingredient, that's the best
                    if (matchingFlavor.def.ingredients[j].IsFixedIngredient)
                    {
                        lowestIndex = j;
                        needsPlaceholder = false;  // this ingredient won't need a placeholder in the flavor label
                        break;
                    }
                    else if (lowestIndex != -1)
                    {
                        // if the current FlavorDef ingredient is the most specific so far, mark its index
                        if (matchingFlavor.def.ingredients[j].filter.AllowedDefCount < matchingFlavor.def.ingredients[lowestIndex].filter.AllowedDefCount)
                        {
                            lowestIndex = j;
                        }
                    }
                    else { lowestIndex = j; }  // if this is the first match, mark the index
                }
            }


            matchingFlavor.indices.Add(lowestIndex);

            matchingFlavor.placeholders.Add(needsPlaceholder);
            return matchingFlavor;
        }

        private void ChooseBestFlavor(List<MatchingFlavor> matchingFlavors)  // rank valid flavor defs and choose the best one
        {
            if (!matchingFlavors.NullOrEmpty())
            {
                matchingFlavors.SortByDescending(entry => entry.def.specificity);
                foreach (MatchingFlavor matchingFlavor in matchingFlavors)
                {
                }
                bestFlavor = new MatchingFlavor(matchingFlavors.Last().def, matchingFlavors.Last().indices, matchingFlavors.Last().placeholders);
                return;
            }
            Log.Error("no valid flavorDefs to choose from");
        }

        private List<string> CleanupIngredientLabels(List<ThingDef> ingredients)  // remove unnecessary bits of the ingredient labels, like "meat"
        {
            List<string> ingredientLabels = [];
            for (int i = 0; i < ingredients.Count; i++)
            {
                string cleanLabel = ingredients[i].label;
                cleanLabel = Regex.Replace(cleanLabel, "(?i)\bmeat$", "");  // remove "meat" endings
                cleanLabel = Regex.Replace(cleanLabel, "(?i)\baw\b", "");  // remove "raw"
                foreach (ThingCategoryDef thingCategoryDef in ingredients[i].thingCategories)  // all eggs -> "egg"
                {
                    if (thingCategoryDef.label == "EggsUnfertilized" || thingCategoryDef.label == "EggsFertilized")
                    {
                        cleanLabel = "egg";
                    }
                }
                switch (ingredients[i].defName)  // specific replacements for special meats
                {
                    case "Meat_Twisted":
                        cleanLabel = "twisted meat";
                        break;

                    case "Meat_Human":
                        cleanLabel = "long pork";
                        break;

                    case "Meat_Megaspider":
                        cleanLabel = "bug";
                        break;


                }
                ingredientLabels.Add(cleanLabel);
            }
            return ingredientLabels;
        }

        private void FillInCategories(List<string> ingredientLabels)  // replace placeholder categories with the corresponding ingredient names
        {
            finalFlavorLabel = bestFlavor.def.label;
            List<string> placeholderLabels = [];
            for (int j = 0; j < ingredients.Count; j++)
            {
                // (PlantFoodRaw, Milk, FoodRaw)
                // (0, 1, 2)
                // (true, false, true)
                // (Berries, Milk, MeatThrumbo)
                if (bestFlavor.placeholders[j] == true)
                {
                    placeholderLabels.Add(ingredientLabels[j]);
                }
            }
            Log.Message("done with placeholders");
            if (placeholderLabels != null)
            {
                finalFlavorLabel = string.Format(finalFlavorLabel, placeholderLabels.ToArray());  // fill in placeholders; error if labels < placeholders
            }
        }

        //TODO: currently this changes the entire ThingDef label

        private string CleanupFlavorLabel(string flavorLabel)  //TODO: use this to clean up the label based on context of the other words
        {
            return flavorLabel;
        }

        public void SortFlavors()  // sort CompFlavor's ingredients and bestFlavor's fields according to the bestFlavor's recipe
        {
            if (this != null)
            {
                ingredients = [.. ingredients.OrderBy(ing => { return bestFlavor.indices[ingredients.IndexOf(ing)]; })];  // sort ingredients by indices
                bestFlavor.placeholders = [.. bestFlavor.placeholders.OrderBy(place => { return bestFlavor.indices[bestFlavor.placeholders.IndexOf(place)]; })];  // sort placeholders by indices
                bestFlavor.indices.Sort();  // sort indices
            }
        }


        //find a flavorLabel and apply it to the parent meal
        public override string TransformLabel(string label)
        {

            if (finalFlavorLabel == null)  // if no flavor label, find one
            {
                ingredients = parent.GetComp<CompIngredients>().ingredients;  // get a list of ingredients from the parent meal

                AcquireFlavor(ingredients, flavorDefList);  // get single best Flavor Def from all possible matching Flavor Defs

                if (bestFlavor.def != null)
                {
                    SortFlavors();
                    List<string> ingredientLabels = CleanupIngredientLabels(ingredients);  // change ingredient labels to fit better in the upcoming flavorLabel
                    FillInCategories(ingredientLabels);  // replace placeholders in the flavor label with the corresponding ingredient in the meal
                    finalFlavorLabel = GenText.CapitalizeAsTitle(finalFlavorLabel);
                }
                else { Log.Error("no suitable FlavorDef found"); }
            }
            return finalFlavorLabel;
        }

        public override string CompInspectStringExtra()  // if you've successfully created a new flavor label, move the original name down
        {
            if (bestFlavor != null)
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
            if (bestFlavor.def == null) { return null; }
            return bestFlavor.def.description;
        }

        public override void PostExposeData()  // include the flavor label in game save files
        {
            base.PostExposeData();
            Scribe_Values.Look(ref bestFlavor, "bestFlavor");
        }
    }

    public class CompProperties_Flavor : CompProperties
    {

        public CompProperties_Flavor()
        {
            compClass = typeof(CompFlavor);
        }

        public CompProperties_Flavor(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}