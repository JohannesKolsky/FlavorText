using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
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
//--TODO: put tags for empty flavor descriptions
//--TODO: handle arbitrary # ingredients: give each ingredient ranked-choice of what it matches with; assign most accurate matches first; if ingredients remain run again with remainder; join all final labels together

//TODO: merging stacks doesn't change the meal name
//TODO: options to prevent merging meals
//TODO: flavor descriptions
//TODO: swapping an ingredient with another of the same category should result in the same dish; sort by categories, not hash codes

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

        private string finalFlavorLabel = null; //final human-readable label for the meal
        private string finalFlavorDescription = null;

        private const int MaxNumIngredientsFlavor = CompIngredients.MaxNumIngredients;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite

        public static readonly List<FlavorDef> flavorDefList = (List<FlavorDef>)DefDatabase<FlavorDef>.AllDefs;  // compile a list of all FlavorDefs

        public MatchingFlavor AcquireFlavor(List<ThingDef> ingredientsToSearchFor)  // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
        {
            if (ingredientsToSearchFor == null)
            {
                Log.Error("Ingredients to search for are null");
                return null;
            }
            else if (flavorDefList == null)
            {
                Log.Error("List of Flavor Defs is null");
                return null;
            }
            else if (flavorDefList.Count == 0)
            {
                Log.Error("List of Flavor Defs is empty");
                return null;
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

            MatchingFlavor flavor = null;
            if (matchingFlavors.Count > 0)
            {

                flavor = ChooseBestFlavor(matchingFlavors);
            }

            // return
            if (flavor.def != null)
            {
                return flavor;
            }
            else { Log.Error("No matching FlavorDefs found."); return null; }
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
            bool mayNeedPlaceholder = false;

            for (int j = 0; j < matchingFlavor.def.ingredients.Count; j++)  // compare the given ingredient to the FlavorDef's ingredients to see which it matches best with
            {
                if (matchingFlavor.indices.Contains(j)) { continue; }  // if this FlavorDef ingredient was already matched with, skip it
                if (matchingFlavor.def.ingredients[j].filter.Allows(ingredient))
                {
                    // if you matched with a fixed ingredient, that's the best
                    if (matchingFlavor.def.ingredients[j].IsFixedIngredient)
                    {
                        lowestIndex = j;
                        mayNeedPlaceholder = false;
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
                    else { lowestIndex = j; mayNeedPlaceholder = true; }  // if this is the first match, mark the index, and it may need a placeholder
                }
            }


            matchingFlavor.indices.Add(lowestIndex);
            matchingFlavor.placeholders.Add(mayNeedPlaceholder);
            return matchingFlavor;
        }

        private MatchingFlavor ChooseBestFlavor(List<MatchingFlavor> matchingFlavors)  // rank valid flavor defs and choose the best one
        {
            if (!matchingFlavors.NullOrEmpty())
            {
                matchingFlavors.SortByDescending(entry => entry.def.specificity);
                foreach (MatchingFlavor matchingFlavor in matchingFlavors)
                {
                }
                MatchingFlavor bestFlavor = new(matchingFlavors.Last().def, matchingFlavors.Last().indices, matchingFlavors.Last().placeholders);
                return bestFlavor;
            }
            Log.Error("no valid flavorDefs to choose from");
            return null;
        }

        private List<string> CleanupIngredientLabels(MatchingFlavor flavor, List<ThingDef> ingredients)  // remove unnecessary bits of the ingredient labels, like "meat" and "raw"
        {
            List<string> ingredientLabels = [];
            for (int i = 0; i < ingredients.Count; i++)
            {
                string cleanLabel = ingredients[i].label;
                cleanLabel = Regex.Replace(cleanLabel, @"(?i)([\b\- ]meat)|(meat[\b\- ])", "");  // remove "meat"
                cleanLabel = Regex.Replace(cleanLabel, @"(?i)([\b\- ]raw)|(raw[\b\- ])", "");  // remove "raw"
                cleanLabel = Regex.Replace(cleanLabel, @"(?i)([\b\- ]fruit)|(fruit[\b\- ])", ""); // remove "fruit"
                foreach (ThingCategoryDef thingCategoryDef in ingredients[i].thingCategories)  // all eggs -> "egg"
                {
                    if (thingCategoryDef.defName == "EggsUnfertilized" || thingCategoryDef.defName == "EggsFertilized")
                    {
                        cleanLabel = "egg";
                    }
                }
                switch (ingredients[i].defName)  // specific replacements for special meats
                {
                    case "Meat_Twisted":
                        if (!Regex.IsMatch(flavor.def.label, @"(?i)\{" + ingredients.IndexOf(ingredients[i]) + @"\}$")) { cleanLabel = "twisted"; }  // if the ingredient isn't the last word in the flavor label
                        else { cleanLabel = "twisted meat"; }
                        break;

                    case "Meat_Human":
                        if (!Regex.IsMatch(flavor.def.label, @"(?i)\{" + ingredients.IndexOf(ingredients[i]) + @"\}$")) { cleanLabel = "cannibal"; }
                        else { cleanLabel = "long pork"; }
                        break;

                    case "Meat_Megaspider":
                        cleanLabel = "bug";
                        break;
                }
                ingredientLabels.Add(cleanLabel);
            }
            return ingredientLabels;
        }

        private string FillInCategories(MatchingFlavor flavor, List<string> ingredientLabels)  // replace placeholder categories with the corresponding ingredient names
        {
            List<string> placeholderLabels = [];
            for (int j = 0; j < ingredientLabels.Count; j++)
            {
                if (flavor.placeholders[j] == true)
                {
                    placeholderLabels.Add(ingredientLabels[j]);
                }
            }
            if (placeholderLabels != null)
            {
                string filledString = string.Format(flavor.def.label, placeholderLabels.ToArray());  // fill in placeholders; error if labels < placeholders
                return filledString;
            }
            Log.Error("List of labels to fill in ingredient category placeholders was null.");
            return null;
        }

        //TODO: currently this changes the entire ThingDef label

        private string CleanupFlavorLabel(string flavorLabel)  //TODO: use this to clean up the label based on context of the other words
        {
            return flavorLabel;
        }


        private List<ThingDef> SortIngredientsAndFlavor(MatchingFlavor flavor, List<ThingDef> ingredientGroup)  // sort ingredients by the flavor indices, then the placeholders, then the indices themselves
        {
            // (egg, thrumbo, berries)
            // (true, true, false)
            // (2, 0, 1)

            // (thrumbo, berries, egg)

            // (0, 1, 2)

            ingredientGroup = [.. ingredientGroup.OrderBy(ing => flavor.indices[ingredientGroup.IndexOf(ing)])];  // sort ingredients by indices

            // sort placeholder bools by indices
            List<bool> newPlaceholders = [];
            for (int i = 0; i < flavor.placeholders.Count; i++)
            {
                newPlaceholders.Add(flavor.placeholders[flavor.indices.IndexOf(i)]);
            }
            flavor.placeholders = newPlaceholders;

            flavor.indices.Sort();  // sort indices 0-n

            List<(ThingDef, bool, int)> meat = [];
            for (int i = 0;  i < ingredientGroup.Count; i++)
            {
                if (ingredientGroup[i].thingCategories.Contains(DefDatabase<ThingCategoryDef>.GetNamed("MeatRaw")))
                {
                    meat.Add((ingredientGroup[i], flavor.placeholders[i], i));
                }
            }

            // if there's more than one piece of meat, put all the meat in the best grammatical order by swapping their positions
            if (meat.Count > 1)
            {
                meat = [.. meat.OrderBy(m => m.Item1, new MeatComparer())];  // assign special meats a ranking

                // rearrange the meats in the current ingredientGroup to match the new ranking
                for (int k = 0; k < meat.Count; k++)
                {
                    ingredientGroup[k] = meat[k].Item1;
                    flavor.placeholders[k] = meat[k].Item2;
                }
            }
            return ingredientGroup;
        }

        // 
        // 

        public class IngredientComparer : IComparer<ThingDef>
        {
            public int Compare(ThingDef a, ThingDef b)
            {
                // null is considered lowest
                if (a.thingCategories == null && b.thingCategories == null) { return 0; }
                else if (a.thingCategories == null) { return  -1; }
                else if (b.thingCategories == null) { return 1; }

                foreach (ThingCategoryDef cat in a.thingCategories)
                {
                    // if the ingredients share a category, group them and sort them by shortHash
                    if (b.thingCategories.Contains(cat))
                    {
                        return a.shortHash.CompareTo(b.shortHash);
                    }
                    return a.defName.CompareTo(b.defName);
                }
                Log.Error("unable to compare some ingredients, missing defName or bad thingCategories");
                return 0;
            }
        }

        public class MeatComparer : IComparer<ThingDef>
        {
            public int Compare(ThingDef meat1, ThingDef meat2)
            {
                if (meat1.thingCategories == null && meat2.thingCategories == null) { return 0; }
                else if (meat1.thingCategories == null) { return -1; }
                else if (meat2.thingCategories == null) { return 1; }

                List<int> ranking = [meat1.defName switch { "Meat_Twisted" => 0, "Meat_Human" => 3, _ => 12, }, meat2.defName switch { "Meat_Twisted" => 0, "Meat_Human" => 3, _ => 12, }];
                int difference = ranking[0] - ranking[1];
                return difference;
            }
        }



        //find a flavorLabel and apply it to the parent meal
        public override string TransformLabel(string label)
        {

            if (finalFlavorLabel == null)  // if no flavor label, find one
            {
                parent.GetComp<CompIngredients>().ingredients.OrderBy(ing => ing, new IngredientComparer()); // sort the ingredients by thingCategories, then by shortHashes
                ingredients = parent.GetComp<CompIngredients>().ingredients;  // get a copy of the list of ingredients from the parent meal

                // divide the ingredients into groups
                List<List<ThingDef>> ingredientsSplit = [];
                System.Random randy = new(0);
                while (ingredients.Count > 0)
                {
                    List<ThingDef> ingredientGroup = [];
                    for (int j = 0; j < MaxNumIngredientsFlavor; j++)
                    {
                        int indexPseudoRand = (int)Math.Floor((float)randy.Next() % ingredients.Count);  // get a pseudorandom index that isn't bigger than the ingredients list
                        ingredientGroup.Add(ingredients[indexPseudoRand]); 
                        ingredients.Remove(ingredients[indexPseudoRand]);
                        if (ingredients.Count == 0) { break; }  // if you removed the last ingredient but haven't finished the current ingredient group, you're done
                    }
                    ingredientsSplit.Add(ingredientGroup);  // add the group of 3 ingredients (default) to a list of ingredient groups
                }

                 List<MatchingFlavor> bestFlavors = [];
                foreach (List<ThingDef> ingredientGroup in ingredientsSplit)
                {
                    MatchingFlavor bestFlavor = AcquireFlavor(ingredientGroup);  // get best Flavor Def from all possible matching Flavor Defs
                    bestFlavors.Add(bestFlavor);
                }
                 

                for (int i = 0; i <  bestFlavors.Count; i++)
                {
                    if (bestFlavors[i].def != null)  // sort things by category, then sort meat in a specific order for grammar's sake
                    {
                        List<ThingDef> ingredientGroupSorted = SortIngredientsAndFlavor(bestFlavors[i], ingredientsSplit[i]);

                        List<string> ingredientLabels = CleanupIngredientLabels(bestFlavors[i], ingredientGroupSorted);  // change ingredient labels to fit better in the upcoming flavorLabel

                        string flavorLabel = FillInCategories(bestFlavors[i], ingredientLabels);  // replace placeholders in the flavor label with the corresponding ingredient in the meal
                        flavorLabel = GenText.CapitalizeAsTitle(flavorLabel);
                        if (finalFlavorLabel == null) { finalFlavorLabel = flavorLabel; }
                        else { finalFlavorLabel = finalFlavorLabel + " with " + flavorLabel; }

                        string flavorDescription = FillInCategories(bestFlavors[i], ingredientLabels);  // replace placeholders in the flavor description with the corresponding ingredient in the meal
                        flavorDescription = GenText.EndWithPeriod(flavorDescription);
                        if (finalFlavorDescription == null) { finalFlavorDescription = GenText.CapitalizeSentences(flavorDescription); }
                        else { finalFlavorDescription = finalFlavorDescription + " This dish also comes with " + GenText.UncapitalizeFirst(flavorDescription); }
                    }

                    else { Log.Error("no suitable FlavorDef found"); }
                }
            }
            return finalFlavorLabel;
        }


        public override string CompInspectStringExtra()  // if you've successfully created a new flavor label, move the original name down
        {
            if (finalFlavorLabel != null)
            {
                StringBuilder stringBuilder = new();
                string typeLabel = GenText.CapitalizeAsTitle(parent.def.label);  // what kind of meal it is
                stringBuilder.AppendLine(typeLabel);
                return stringBuilder.ToString().TrimEndNewlines();
            }
            else { return null; }
        }

        public override string GetDescriptionPart()  // display the FlavorDef description
        {
            if (finalFlavorLabel == null) { Log.Error("Could not get description because label is null"); return null; }
            if (finalFlavorDescription == null) { Log.Error("Could not get description because description is null"); return null; }
            string description = finalFlavorDescription;
            return description;
        }

        public override void PostExposeData()  // include the flavor label in game save files
        {
            base.PostExposeData();
            Scribe_Values.Look(ref finalFlavorLabel, "finalFlavorLabel");
            Scribe_Values.Look(ref finalFlavorDescription, "finalFlavorDescription"); 
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