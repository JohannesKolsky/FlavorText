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
//--TODO: formatted strings
//--TODO: track which ingredients will go into a placeholder
//--TODO: store indices and everything else directly in fields of CompFlavor
//--TODO: put tags for empty flavor descriptions
//--TODO: handle arbitrary # ingredients: give each ingredient ranked-choice of what it matches with; assign most accurate matches first; if ingredients remain run again with remainder; join all final labels together
//--TODO: swapping an ingredient with another of the same category should result in the same dish; sort by categories, not hash codes
//--TODO: check if contains parent category
//-TODO: fish?
//--TODO: flavor descriptions

//TODO: merging stacks doesn't change the meal name
//TODO: options to prevent merging meals
//TODO: VegetableGarden: Garden Meats
//TODO: Vanilla Expanded compat: canned meat -> canned (special case), gourmet meals (condiment is an ingredient), desserts (derived from ResourceBase), etc
//TODO: baby food is derived from OrganicProductBase
//TODO: noun, plural, adj form of each resource (if none given, use regular label); this can then replace all the rearranging with meats and such; you can use the defName stripped of Raw and stuff as a stand-in for singular; cannibal and twisted appear in descriptions
//TODO: \n not working in descriptions?
//TODO: buttered "and" is disappearing again X(
//TODO: learn RulePacks  // nested rules: sausage --> [sausage] --> [meat] // 3-ingredients -> 2[1]
//TODO: generalize meat substitution
//TODO: different eggs don't merge
//TODO: specific meat type overrides (and overrides in general)

//fixedIngredientFilter: which items are allowed
//defaultIngredientFilter: default assignment of fixedIngredientFilter
//fixedIngredient: used if fixedIngredientFilter.Count == 1

namespace FlavorText
{
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

        public string finalFlavorLabel = ""; //final human-readable label for the meal
        public string finalFlavorDescription = "";
        public List<FlavorDef> finalFlavorDefs = [];  //final chosen FlavorDef for the meal

        private const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite

        public static readonly int worldSeed = GenText.StableStringHash(Find.World.info.seedString);

        public static readonly List<FlavorDef> flavorDefList = (List<FlavorDef>)DefDatabase<FlavorDef>.AllDefs;  // compile a list of all FlavorDefs

        private static readonly List<string> sideDishConjunctions =
            [
            ". This entree also comes with ",
            ". As a side dish you find ",
            ". Accompanying it is ",
            ". Served on the side you find ",
            ". As an appetizer ",
            ". It is accompanied by ",
            ". As a palate cleanser one is presented with ",
            ". This entree is topped with ",
            ". It is covered with ",
            ". This dish is served on a bed of ",
            ". Arranged in a circle around it is ",
            ". In a presentation of fusion cuisine this dish is mixed together with ",
            ". The center of the dish has been removed in order to contain ",
            ". Alongside the entree sits ",
            ". Next to it is a smaller serving of ",
            ". This entree comes with a healthy helping of "
            ];

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
            if (flavor != null)
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
                if (!matchingFlavor.indices.Contains(-1)) // the FlavorDef matches completely return it
                {
                    return matchingFlavor;
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
                MatchingFlavor bestFlavor = new(matchingFlavors.Last().def, matchingFlavors.Last().indices, matchingFlavors.Last().placeholders);
                return bestFlavor;
            }
            Log.Error("no valid flavorDefs to choose from");
            return null;
        }

        private List<string> CleanupIngredientLabels(MatchingFlavor flavor, List<ThingDef> ingredients, string flag)  // remove unnecessary bits of the ingredient labels, like "meat" and "raw"; may edit flavor label and description strings
        {
            string unfilledString = flavor.def.GetType().GetField(flag).GetValue(flavor.def).ToString();
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
                        {
                            if (flag == "description") { cleanLabel = "twisted flesh"; }  // use full name in description
                            else if (Regex.IsMatch(unfilledString, @"(?i)\{" + i + @"\}($| with[^a-zA-Z]|[^a-zA-Z ])")) { cleanLabel = "twisted meat"; }  // if the placeholder isn't in a good adjective spot (end of line, before conjunction, before punctuation)
                            else
                            {
                                unfilledString = Regex.Replace(unfilledString, @"(?i)(\{" + i + @"\}) and ", "$1 ");  // remove any "and" following the placeholder
                                flavor.def.GetType().GetField(flag).SetValue(flavor.def, unfilledString);
                                cleanLabel = "twisted";
                            }
                            break;
                        }

                    case "Meat_Human":
                        {
                            if (flag == "description") { cleanLabel = "human"; }
                            else if (Regex.IsMatch(flavor.def.label, @"(?i)\{" + i + @"\}($| with[^a-zA-Z]|[^a-zA-Z ])")) { cleanLabel = "long pork"; }
                            else
                            {
                                unfilledString = Regex.Replace(unfilledString, @"(?i)(\{" + i + @"\}) and ", "$1 ");
                                flavor.def.GetType().GetField(flag).SetValue(flavor.def, unfilledString);
                                cleanLabel = "cannibal";
                            }
                            break;
                        }

                    case "Meat_Megaspider":
                        {
                            if (flag == "description") { cleanLabel = "bug guts"; }
                            else if (Regex.IsMatch(flavor.def.label, @"(?i)\{" + i + @"\}($| with[^a-zA-Z]|[^a-zA-Z ])")) { cleanLabel = "bug guts"; }
                            else
                            {
                                unfilledString = Regex.Replace(unfilledString, @"(?i)(\{" + i + @"\}) and ", "$1 ");
                                flavor.def.GetType().GetField(flag).SetValue(flavor.def, unfilledString);
                                cleanLabel = "bug";
                            }
                            break;
                        }
                    default: break;
                }
                ingredientLabels.Add(cleanLabel);
            }
            return ingredientLabels;
        }

        private string FillInCategories(MatchingFlavor flavor, List<string> ingredientLabels, string flag)  // replace placeholder categories with the corresponding ingredient names
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
                string unfilledString = flavor.def.GetType().GetField(flag).GetValue(flavor.def).ToString();
                string filledString = string.Format(unfilledString, placeholderLabels.ToArray());  // fill in placeholders; error if labels < placeholders
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
            // sort everything by flavor indices (ascending)
            ingredientGroup = [.. ingredientGroup.OrderBy(ing => flavor.indices[ingredientGroup.IndexOf(ing)])];  // sort ingredients by indices
            List<bool> newPlaceholders = []; // sort placeholder bools by indices
            for (int i = 0; i < flavor.placeholders.Count; i++)
            {
                newPlaceholders.Add(flavor.placeholders[flavor.indices.IndexOf(i)]);
            }
            flavor.placeholders = newPlaceholders;
            flavor.indices.Sort();  // sort indices 0-n

            List<ThingDef> meat = [];
            List<bool> meatPlaceholders = [];
            List<int> meatIndices = [];


            for (int i = 0; i < ingredientGroup.Count; i++)
            {
                if (DefDatabase<ThingCategoryDef>.GetNamed("MeatRaw").ContainedInThisOrDescendant(ingredientGroup[i]))
                {
                    meat.Add(ingredientGroup[i]);
                    meatPlaceholders.Add(flavor.placeholders[i]);
                    meatIndices.Add(flavor.indices[i]);
                }
            }
            // if there's more than one piece of meat, put all the meat in the best grammatical order by swapping their positions
            if (meat.Count > 1)
            {
                meat = [.. meat.OrderBy(m => m, new MeatComparer())];  // assign special meats a ranking
                meatIndices.Sort();  // meat indices in numerical order
            }
            for (int i = 0; i < meat.Count; i++)
            {
                int sortedMeatIndex = meatIndices[i];
                ingredientGroup[sortedMeatIndex] = meat[i];
                flavor.placeholders[sortedMeatIndex] = meatPlaceholders[i];
            }
            return ingredientGroup;
        }


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
            try
            {
                if (finalFlavorLabel == "FAIL") { return label; }  // if you've failed to find a label before, don't even try
                else if (finalFlavorLabel == "")  // if you haven't tried to find a label before, do so
                {
                    ingredients = parent.GetComp<CompIngredients>().ingredients;  // get a copy of the list of ingredients from the parent meal
                    if (ingredients.Count == 0) { Log.Warning("Meal has no ingredients, returning original label"); finalFlavorLabel = "FAIL";  return label; }  // if there's no ingredients, keep the original label
                    ingredients = [.. ingredients.OrderBy(ing => ing, new IngredientComparer())];  // sort the ingredients by category

                    // divide the ingredients into groups by pseudorandomly pulling ingredients from the ingredient list
                    List<List<ThingDef>> ingredientsSplit = [];
                    if (ingredients.Count > 0)
                    {
                        int pseudo = GenText.StableStringHash(ingredients[0].defName);
                        while (ingredients.Count > 0)
                        {
                            // form groups of size 3 (default)
                            List<ThingDef> ingredientGroup = [];
                            for (int j = 0; j < MaxNumIngredientsFlavor; j++)
                            {
                                pseudo = new Random(worldSeed + pseudo).Next(ingredients.Count); // get a pseudorandom index that isn't bigger than the ingredients list count
                                int indexPseudoRand = (int)Math.Floor((float)pseudo);
                                ingredientGroup.Add(ingredients[indexPseudoRand]);
                                ingredients.Remove(ingredients[indexPseudoRand]);
                                if (ingredients.Count == 0) { break; }  // if you removed the last ingredient but haven't finished the current ingredient group, you're done
                            }
                            ingredientsSplit.Add(ingredientGroup);  // add the group of 3 ingredients (default) to a list of ingredient groups
                        }
                    }

                    List<MatchingFlavor> bestFlavors = [];
                    foreach (List<ThingDef> ingredientGroup in ingredientsSplit)
                    {
                        MatchingFlavor bestFlavor = AcquireFlavor(ingredientGroup);  // get best Flavor Def from all possible matching Flavor Defs
                        if (bestFlavor == null) { finalFlavorLabel = "FAIL"; return label; }
                        bestFlavors.Add(bestFlavor);
                    }


                    for (int i = 0; i < bestFlavors.Count; i++)  // assemble all the flavor labels chosen into one big label that looks nice
                    {
                        if (bestFlavors[i] != null)  // sort things by category, then sort meat in a specific order for grammar's sake
                        {
                            finalFlavorDefs.Add(bestFlavors[i].def);
                            List<ThingDef> ingredientGroupSorted = SortIngredientsAndFlavor(bestFlavors[i], ingredientsSplit[i]);

                            List<string> ingredientLabelsForLabel = CleanupIngredientLabels(bestFlavors[i], ingredientGroupSorted, "label");  // change ingredient labels to fit better in the upcoming flavorLabel
                            string flavorLabel = FillInCategories(bestFlavors[i], ingredientLabelsForLabel, "label");  // replace placeholders in the flavor label with the corresponding ingredient in the meal
                            string flavorLabelCap = GenText.CapitalizeAsTitle(flavorLabel);
                            if (finalFlavorLabel == "") { finalFlavorLabel = flavorLabelCap; }
                            else { finalFlavorLabel = finalFlavorLabel + " with " + flavorLabelCap; }

                            List<string> ingredientLabelsForDescription = CleanupIngredientLabels(bestFlavors[i], ingredientGroupSorted, "description");  // change ingredient labels to fit better in the upcoming flavorLabel
                            string flavorDescription = FillInCategories(bestFlavors[i], ingredientLabelsForDescription, "description");  // replace placeholders in the flavor description with the corresponding ingredient in the meal
                            flavorDescription = GenText.EndWithPeriod(flavorDescription);

                            int pseudo = GenText.StableStringHash(bestFlavors[i].def.defName);
                            if (i == 0) { finalFlavorDescription = GenText.CapitalizeSentences(flavorDescription); }  // first description
                            else // multiple descriptions, link to the previous
                            {
                                pseudo = new Random(worldSeed + pseudo).Next(sideDishConjunctions.Count);
                                finalFlavorDescription = Regex.Replace(finalFlavorDescription, @".$", "");
                                finalFlavorDescription = finalFlavorDescription + sideDishConjunctions[pseudo] + GenText.UncapitalizeFirst(flavorLabel);
                            }
                            finalFlavorDescription = GenText.EndWithPeriod(finalFlavorDescription);
                        }
                        else { Log.Error("One of the chosen FlavorDefs is null, skipping it."); }
                    }
                }
                if (finalFlavorLabel == "" || finalFlavorLabel == null) { finalFlavorLabel = "FAIL"; return label; }  // if you failed to find a label, return FAIL so you don't try again later
                return finalFlavorLabel;
            }
            catch (Exception e) { Log.Error("Encountered an error transforming the label, returning original label. Error: " + e);  finalFlavorLabel = "FAIL"; return label; }
        }


        public override string CompInspectStringExtra()  // if you've successfully created a new flavor label, move the original name down
        {
            if (finalFlavorLabel != "")
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
            if (finalFlavorLabel == "") { Log.Error("Could not get FlavorText description because FlavorText label is blank"); return ""; }
            if (finalFlavorDescription == "") { Log.Error("Could not get FlavorText description because FlavorText description is blank"); return ""; }
            string description = finalFlavorDescription;
            return description;
        }

        public override void PostExposeData()  // include the flavor label in game save files
        {
            base.PostExposeData();
            Scribe_Values.Look(ref finalFlavorLabel, "finalFlavorLabel");
            Scribe_Values.Look(ref finalFlavorDescription, "finalFlavorDescription");
            Scribe_Values.Look(ref finalFlavorDefs, "finalFlavorDefs");
        }
    }

    public class CompProperties_Flavor : CompProperties
    {

        public CompProperties_Flavor()
        {
            compClass = typeof(CompFlavor);
        }
    }
}