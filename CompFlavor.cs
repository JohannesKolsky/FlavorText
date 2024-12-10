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

//RELEASE: check for that null bug again

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


namespace FlavorText;

public class CompFlavor : ThingComp
{
    public List<Thing> ingredients;  // ingredient list of the meal

    public string finalFlavorLabel; //final human-readable label for the meal

    public string finalFlavorDescription;

    public List<FlavorDef> finalFlavorDefs = new List<FlavorDef>();  //final chosen FlavorDef for the meal

    private const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite

    public static readonly int worldSeed;

    public static readonly List<FlavorDef> flavorDefList;

    public static List<ThingCategoryDef> flavorThingCategoryDefs;  // compile a list of all FlavorDefs

    public CompProperties_Flavor Props => (CompProperties_Flavor)props;
    public class IngredientComparer : IComparer<Thing>
    {
        public int Compare(Thing a, Thing b)
        {
            // null is considered lowest
            if (a.def.thingCategories == null && b.def.thingCategories == null) { return 0; }
            else if (a.def.thingCategories == null) { return -1; }
            else if (b.def.thingCategories == null) { return 1; }

            foreach (ThingCategoryDef cat in a.def.thingCategories)
            {
                // if the ingredients share a category, group them and sort them by shortHash
                if (b.def.thingCategories.Contains(cat))
                {
                    return a.def.shortHash.CompareTo(b.def.shortHash);
                }
                return a.def.defName.CompareTo(b.def.defName);
            }
            Log.Error("unable to compare some ingredients, missing defName or bad thingCategories");
            return 0;
        }
    }

    public class MeatComparer : IComparer<Thing>
    {
        public int Compare(Thing meat1, Thing meat2)
        {
            if (meat1.def.thingCategories == null && meat2.def.thingCategories == null) { return 0; }
            else if (meat1.def.thingCategories == null) { return -1; }
            else if (meat2.def.thingCategories == null) { return 1; }
            List<int> ranking = [meat1.def.defName switch { "Meat_Twisted" => 0, "Meat_Human" => 3, _ => 12, }, meat2.def.defName switch { "Meat_Twisted" => 0, "Meat_Human" => 3, _ => 12, }];
            int difference = ranking[0] - ranking[1];
            return difference;
        }
    }

    static CompFlavor()
    {
        worldSeed = GenText.StableStringHash(Find.World.info.seedString);
        flavorDefList = (List<FlavorDef>)DefDatabase<FlavorDef>.AllDefs;
        flavorThingCategoryDefs = new List<ThingCategoryDef>();
        Log.Warning("Compiling CompFlavor static data");
        CompileFlavorCategories();
        DiscoverChildren();
    }

/*    public override void Initialize(CompProperties properties)
    {
        Log.Warning("Initializing");
        if (!ingredients.NullOrEmpty())
        {
            Log.Warning("During initialization, ingredients found for Thing");
            Log.Message("Found ingredient " + ingredients[0]);
            GetFlavorText();
            Log.Message("During initialization, found flavorLabel called [" + finalFlavorLabel + "]");
        }
        else
        {
            Log.Error("During initialization, failed to get ingredients from CompFlavor");
        }
        base.Initialize(properties);
    }

    public CompFlavor()
    {
        Log.Warning("Instatiating");
        if (!ingredients.NullOrEmpty())
        {
            Log.Warning("During instatiation, ingredients found for Thing");
            Log.Message("Found ingredient " + ingredients[0]);
            GetFlavorText();
            Log.Message("During instatiation, found flavorLabel called [" + finalFlavorLabel + "]");
        }
        else
        {
            Log.Error("During instantiation, failed to get ingredients from CompFlavor");
        }
    }

    public override void PostPostMake()
    {
        Log.Warning("PostPostMaking");
        if (!ingredients.NullOrEmpty())
        {
            Log.Warning("During PostPostMake, ingredients found for Thing ");
            GetFlavorText();
            Log.Message("During PostPostMake, found flavorLabel called [" + finalFlavorLabel + "]");
        }
        else
        {
            Log.Error("During PostPostMaking, failed to get ingredients from CompFlavor");
        }
        base.PostPostMake();
    }*/

    public override string TransformLabel(string label)
    {
        Log.Warning("Transforming label");
        if (finalFlavorLabel != null && finalFlavorLabel != "" && finalFlavorLabel != "FAIL")
        {
            return finalFlavorLabel;
        }
        Log.Error("Could not transform label because it was null, blank, or failed to find a new label.");
        Log.Message("new label was: [" + finalFlavorLabel + "]");
        if (!ingredients.NullOrEmpty())
        {
            Log.Warning("During TransformLabel, ingredients found for Thing ");
            GetFlavorText();
            Log.Message("During TransformLabel, found flavorLabel called [" + finalFlavorLabel + "]");
        }
        else
        {
            Log.Error("During TransformLabel, failed to get ingredients from CompFlavor");
        }
        return label;
    }

    // if you've successfully created a new flavor label, move the original name down
    public override string CompInspectStringExtra()
    {
        if (finalFlavorLabel != "")
        {
            StringBuilder stringBuilder = new StringBuilder();
            string typeLabel = GenText.CapitalizeAsTitle(parent.def.label);
            stringBuilder.AppendLine(typeLabel);
            return stringBuilder.ToString().TrimEndNewlines();
        }
        return null;
    }

    // display the FlavorDef description
    public override string GetDescriptionPart()
    {
        if (finalFlavorLabel == "" || finalFlavorLabel == null)
        {
            Log.Error("Could not get FlavorText description because FlavorText label is blank or null");
            return "";
        }
        if (finalFlavorDescription == "" || finalFlavorDescription == null)
        {
            Log.Error("Could not get FlavorText description because FlavorText description is blank.");
            return "";
        }
        return finalFlavorDescription;
    }

    // include the important things in game save files
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref finalFlavorLabel, "finalFlavorLabel");
        Scribe_Values.Look(ref finalFlavorDescription, "finalFlavorDescription");
        Scribe_Values.Look(ref finalFlavorDefs, "finalFlavorDefs");
    }

    private static void CompileFlavorCategories()
    {
        List<ThingCategoryDef> allThingCategoryDefs = (List<ThingCategoryDef>)DefDatabase<ThingCategoryDef>.AllDefs;
        for (int i = 0; i < allThingCategoryDefs.Count; i++)
        {
            if (allThingCategoryDefs[i].defName.StartsWith("FT_"))
            {
                flavorThingCategoryDefs.Add(allThingCategoryDefs[i]);
            }
        }
    }

    private static void DiscoverChildren()
    {
        foreach (ThingCategoryDef thingCategoryDef in flavorThingCategoryDefs)
        {
            List<ThingDef> flavorChildThingDefs = thingCategoryDef.GetModExtension<FlavorCategoryModExtension>().flavorChildThingDefs;
            foreach (ThingDef thingDef in flavorChildThingDefs)
            {
                thingCategoryDef.childThingDefs.Add(thingDef);
            }
            List<ThingCategoryDef> flavorChildCategories = thingCategoryDef.GetModExtension<FlavorCategoryModExtension>().flavorChildCategories;
            foreach (ThingCategoryDef childCategory in flavorChildCategories)
            {
                thingCategoryDef.childCategories.Add(childCategory);
            }
        }
    }

    // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    public FlavorWithIndices AcquireFlavor(List<Thing> ingredientsToSearchFor)
    {
        if (ingredientsToSearchFor == null)
        {
            Log.Error("Ingredients to search for are null");
            return null;
        }
        if (flavorDefList == null)
        {
            Log.Error("List of Flavor Defs is null");
            return null;
        }
        if (flavorDefList.Count == 0)
        {
            Log.Error("List of Flavor Defs is empty");
            return null;
        }

        //see which FlavorDefs match with the ingredients in the meal
        List<FlavorWithIndices> matchingFlavors = new List<FlavorWithIndices>();
        foreach (FlavorDef flavorDef in flavorDefList)
        {
            FlavorWithIndices matchingFlavor = IsMatchingFlavor(ingredientsToSearchFor, flavorDef);
            if (matchingFlavor != null && matchingFlavor.indices.Count == ingredientsToSearchFor.Count)
            {
                matchingFlavors.Add(matchingFlavor);
            }
        }

        // pick the most specific matching FlavorDef
        FlavorWithIndices flavor = null;
        if (matchingFlavors.Count > 0)
        {
            flavor = ChooseBestFlavor(matchingFlavors);
        }

        // return
        if (flavor != null)
        {
            return flavor;
        }
        Log.Error("No matching FlavorDefs found.");
        return null;
    }

    private FlavorWithIndices IsMatchingFlavor(List<Thing> ingredientsToSearchFor, FlavorDef flavorDef)  // check if all the ingredients match the given FlavorDef
    {
        FlavorWithIndices matchingFlavor = new FlavorWithIndices(flavorDef, new List<int>())
        {
            def = flavorDef
        };
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

    private FlavorWithIndices BestIngredientMatch(Thing ingredient, FlavorWithIndices matchingFlavor)  // find the best match for the current single ingredient in the current FlavorDef
    {
        int lowestIndex = -1;
        for (int j = 0; j < matchingFlavor.def.ingredients.Count; j++)  // compare the given ingredient to the FlavorDef's ingredients to see which it matches best with
        {
            // if this FlavorDef ingredient was already matched with, skip it
            if (matchingFlavor.indices.Contains(j))
            {
                continue;
            }
            ThingFilter filter = matchingFlavor.def.ingredients[j].filter;
            List<string> categories = matchingFlavor.def.GetFilterCategories(filter);
            if (categories != null)
            {
                Log.Message("AllowedDefCount is " + filter.AllowedDefCount);
                filter.ResolveReferences();
                Log.Message("AllowedDefCount after ResolveReferences call is " + filter.AllowedDefCount);
            }
            if (!matchingFlavor.def.ingredients[j].filter.Allows(ingredient))
            {
                continue;
            }

            // if you matched with a fixed ingredient, that's the best
            if (matchingFlavor.def.ingredients[j].IsFixedIngredient)
            {
                lowestIndex = j;
                break;
            }
            if (lowestIndex != -1)
            {
                // if the current FlavorDef ingredient is the most specific so far, mark its index
                if (matchingFlavor.def.ingredients[j].filter.AllowedDefCount < matchingFlavor.def.ingredients[lowestIndex].filter.AllowedDefCount)
                {
                    Log.Message("found new best ingredient " + matchingFlavor.def.ingredients[j]);
                    lowestIndex = j;
                }
            }

            // if this is the first match, mark the index
            else
            {
                lowestIndex = j;
            }
        }
        matchingFlavor.indices.Add(lowestIndex);
        return matchingFlavor;
    }

    private FlavorWithIndices ChooseBestFlavor(List<FlavorWithIndices> matchingFlavors)  // rank valid flavor defs and choose the best one
    {
        if (!matchingFlavors.NullOrEmpty())
        {
            matchingFlavors.SortByDescending((FlavorWithIndices entry) => entry.def.specificity);
            return new FlavorWithIndices(matchingFlavors.Last().def, matchingFlavors.Last().indices);
        }
        Log.Error("No valid flavorDefs to choose from");
        return null;
    }

    private List<string> CleanupIngredientLabels(FlavorWithIndices flavor, List<Thing> ingredients, string flag)  // remove unnecessary bits of the ingredient labels, like "meat" and "raw"; may edit flavor label and description strings
    {
        string unfilledString = flavor.def.GetType().GetField(flag).GetValue(flavor.def)
            .ToString();
        List<string> ingredientLabels = new List<string>();
        for (int i = 0; i < ingredients.Count; i++)
        {
            string cleanLabel = ingredients[i].def.label;
            cleanLabel = Regex.Replace(cleanLabel, "(?i)([\\b\\- ]meat)|(meat[\\b\\- ])", "");  // remove "meat"
            cleanLabel = Regex.Replace(cleanLabel, "(?i)([\\b\\- ]raw)|(raw[\\b\\- ])", "");  // remove "raw"
            cleanLabel = Regex.Replace(cleanLabel, "(?i)([\\b\\- ]fruit)|(fruit[\\b\\- ])", ""); // remove "fruit"
            foreach (ThingCategoryDef thingCategoryDef in ingredients[i].def.thingCategories)
            {
                // all eggs -> "egg"
                if (thingCategoryDef.defName == "EggsUnfertilized" || thingCategoryDef.defName == "EggsFertilized")
                {
                    cleanLabel = "egg";
                }
            }

            // specific replacements for special meats
            switch (ingredients[i].def.defName)
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
                        if (flag == "description") { cleanLabel = "human meat"; }
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

    private string FillInCategories(FlavorWithIndices flavor, List<string> ingredientLabels, string flag)  // replace placeholder categories with the corresponding ingredient names
    {
        if (ingredientLabels != null)
        {
            try
            {
                string unfilledString = flavor.def.GetType().GetField(flag).GetValue(flavor.def)
                    .ToString();
                string format = unfilledString;
                int num = 0;
                object[] array = new object[ingredientLabels.Count];
                foreach (string ingredientLabel in ingredientLabels)
                {
                    array[num] = ingredientLabel;
                    num++;
                }
                return string.Format(format, array);  // fill in placeholders; error if labels < placeholders
            }
            catch (Exception ex)
            {
                Log.Error("Error when filling in ingredient category placeholders. Reason was: " + ex);
                return null;
            }
        }
        Log.Error("List of labels to fill in ingredient category placeholders was null.");
        return null;
    }

    //TODO: currently this changes the entire ThingDef label
    //TODO: use this to clean up the label based on context of the other words
    private string CleanupFlavorLabel(string flavorLabel)
    {
        return flavorLabel;
    }

    private List<Thing> SortIngredientsAndFlavor(FlavorWithIndices flavor, List<Thing> ingredientGroup)  // sort ingredients by the flavor indices, then the placeholders, then the indices themselves
    {
        // sort everything by flavor indices (ascending)
        ingredientGroup = [.. ingredientGroup.OrderBy((Thing ing) => flavor.indices[ingredientGroup.IndexOf(ing)])];  // sort ingredients by indices
        flavor.indices.Sort();  // sort indices 0-n
        List<Thing> meat = new List<Thing>();
        List<int> meatIndices = new List<int>();
        for (int i = 0; i < ingredientGroup.Count; i++)
        {
            if (DefDatabase<ThingCategoryDef>.GetNamed("MeatRaw").ContainedInThisOrDescendant(ingredientGroup[i].def))
            {
                meat.Add(ingredientGroup[i]);
                meatIndices.Add(flavor.indices[i]);
            }
        }
        if (meat.Count > 1)
        {
            meat = meat.OrderBy((Thing m) => m, new MeatComparer()).ToList();
            meatIndices.Sort();
        }
        for (int i = 0; i < meat.Count; i++)
        {
            int sortedMeatIndex = meatIndices[i];
            ingredientGroup[sortedMeatIndex] = meat[i];
        }
        return ingredientGroup;
    }

    //find a flavorLabel and apply it to the parent meal
    public void GetFlavorText()
    {
        try
        {
            // if you've failed to find a label before, don't even try
            if (finalFlavorLabel == "FAIL")
            {
                return;
            }

            // if you haven't tried to find a label before, do so
            if (finalFlavorLabel == "" || finalFlavorLabel == null)
            {
                Log.Message("Getting ingredients");
                if (ingredients.Count == 0 || ingredients == null)
                {
                    Log.Warning("Meal has no ingredients, returning original label");
                    Log.Message(ingredients);
                    finalFlavorLabel = "FAIL";
                    return;
                }
                ingredients = ingredients.OrderBy((Thing ing) => ing, new IngredientComparer()).ToList();  // sort the ingredients by category

                List<Thing> ingredientsCopy = ingredients.Select((Thing ingredient) => ingredient).ToList();  // make a copy of the ingredients
                                                                                                          // divide the ingredients into groups by pseudorandomly pulling ingredients from the ingredient list
                List<List<Thing>> ingredientsSplit = new List<List<Thing>>();
                Log.Message("Got ingredients");
                if (ingredientsCopy.Count > 0)
                {
                    int pseudo = GenText.StableStringHash(ingredientsCopy[0].def.defName);

                    // form groups of size 3 (default)
                    while (ingredientsCopy.Count > 0)
                    {
                        List<Thing> ingredientGroup = new List<Thing>();
                        for (int j = 0; j < 3; j++)
                        {
                            pseudo = new Random(worldSeed + pseudo).Next(ingredientsCopy.Count); // get a pseudorandom index that isn't bigger than the ingredients list count
                            int indexPseudoRand = (int)Math.Floor((float)pseudo);
                            ingredientGroup.Add(ingredientsCopy[indexPseudoRand]);
                            ingredientsCopy.Remove(ingredientsCopy[indexPseudoRand]);
                            
                            // if you removed the last ingredient but haven't finished the current ingredient group, you're done
                            if (ingredientsCopy.Count == 0)
                            {
                                break;
                            }
                        }
                        ingredientsSplit.Add(ingredientGroup);  // add the group of 3 ingredients (default) to a list of ingredient groups
                    }
                }
                Log.Message("Split ingredients into groups");
                List<FlavorWithIndices> bestFlavors = new List<FlavorWithIndices>();
                foreach (List<Thing> ingredientGroup in ingredientsSplit)
                {
                    FlavorWithIndices bestFlavor = AcquireFlavor(ingredientGroup);  // get best Flavor Def from all possible matching Flavor Defs
                    if (bestFlavor == null)
                    {
                        finalFlavorLabel = "FAIL";
                        return;
                    }
                    bestFlavors.Add(bestFlavor);
                }

                // assemble all the flavor labels chosen into one big label that looks nice
                for (int i = 0; i < bestFlavors.Count; i++)
                {
                    
                    // sort things by category, then sort meat in a specific order for grammar's sake
                    if (bestFlavors[i] != null)
                    {
                        finalFlavorDefs.Add(bestFlavors[i].def);
                        List<Thing> ingredientGroupSorted = SortIngredientsAndFlavor(bestFlavors[i], ingredientsSplit[i]);
                        List<string> ingredientLabelsForLabel = CleanupIngredientLabels(bestFlavors[i], ingredientGroupSorted, "label");  // change ingredient labels to fit better in the upcoming flavorLabel
                        string flavorLabel = FillInCategories(bestFlavors[i], ingredientLabelsForLabel, "label");  // replace placeholders in the flavor label with the corresponding ingredient in the meal
                        string flavorLabelCap = GenText.CapitalizeAsTitle(flavorLabel);
                        if (finalFlavorLabel == "" || finalFlavorLabel == null)
                        {
                            finalFlavorLabel = flavorLabelCap;
                        }
                        else
                        {
                            finalFlavorLabel = finalFlavorLabel + " with " + flavorLabelCap;
                        }
                        List<string> ingredientLabelsForDescription = CleanupIngredientLabels(bestFlavors[i], ingredientGroupSorted, "description");  // change ingredient labels to fit better in the upcoming flavorLabel
                        string flavorDescription = FillInCategories(bestFlavors[i], ingredientLabelsForDescription, "description");  // replace placeholders in the flavor description with the corresponding ingredient in the meal
                        flavorDescription = flavorDescription.EndWithPeriod();
                        Log.Message("Wrote up label");
                        int pseudo = GenText.StableStringHash(bestFlavors[i].def.defName);

                        // first description
                        if (i == 0)
                        {
                            finalFlavorDescription = GenText.CapitalizeSentences(flavorDescription);
                        }

                        // if more than one description, build the next one and link it to the previous
                        else
                        {
                            RulePackDef sideDishRules = Props.sideDishConjunctions;
                            GrammarRequest request = default;
                            if (sideDishRules != null)
                            {
                                request.Includes.Add(sideDishRules);
                            }
                            string sideDishText = GrammarResolver.Resolve("sidedish", request);  // get a random connector sentence
                            sideDishText = string.Format(sideDishText, flavorLabel);  // place the current flavor description in its placeholder spot within the sentence
                            sideDishText = sideDishText.Trim(',', ' ');
                            sideDishText = sideDishText.EndWithPeriod();
                            sideDishText = GenText.CapitalizeSentences(sideDishText);
                            finalFlavorDescription = finalFlavorDescription + " " + sideDishText;
                        }
                        finalFlavorLabel = Find.ActiveLanguageWorker.PostProcessed(finalFlavorLabel);
                        finalFlavorDescription = Find.ActiveLanguageWorker.PostProcessed(finalFlavorDescription);
                    }
                    else
                    {
                        Log.Error("One of the chosen FlavorDefs is null, skipping.");
                    }
                }
            }

            // if you failed to find a label, return FAIL so you don't try again later
            if (finalFlavorLabel == "" || finalFlavorLabel == null)
            {
                finalFlavorLabel = "FAIL";
            }
        }
        catch (Exception e)
        {
            Log.Error("Encountered an error transforming the label, returning original label. Error was: " + e);
            Log.Error(e.Source);
            Log.Error(e.StackTrace);
            finalFlavorLabel = "FAIL";
        }
    }
}
