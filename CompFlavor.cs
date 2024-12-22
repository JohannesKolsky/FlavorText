using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using Verse.Grammar;
using static FlavorText.CompProperties_Flavor;

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
//--TODO: buttered "and" is disappearing again X(
//--TODO: learn RulePacks
//--TODO: split {Root} category from {FoodRaw}; this will allow for easy special treatment of non {FoodRaw} ingredients
//--TODO: \n not working in descriptions?
//--TODO: merging stacks doesn't change the meal name
//--TODO: merge->pickup erases flavor data (is it because when you merge the one merged in loses all its data?)

//RELEASE: check for that null bug again
//RELEASE: check add to game
//RELEASE: check remove from game
//RELEASE: check new game
//RELEASE: check save and reload game

//TODO: options to prevent merging meals
//TODO: VegetableGarden: Garden Meats
//TODO: Vanilla Expanded compat: canned meat -> canned (special case), gourmet meals (condiment is an ingredient), desserts (derived from ResourceBase), etc
//TODO: baby food is derived from OrganicProductBase
//TODO: noun, plural, adj form of each resource (if none given, use regular label); this can then replace all the rearranging with meats and such; cross-reference label and defName to get singular form; cannibal and twisted appear in descriptions
//TODO: generalize meat substitution
//TODO: meat doesn't get sorted to the front when there's a veggie {Food} in front of it
//TODO: different eggs don't merge
//TODO: specific meat type overrides (and overrides in general)
//TODO: nested rules: sausage --> [sausage] --> [meat] // 3-ingredients -> 2[1]
//TODO: allow old label to show up in map search
//TODO: change job string? does this add anything?
//TODO: RawMeat simplification mod: "raw meat" shows up as "raw"
//TODO: stinker fungus (VCE_Mushrooms) is in Foods, but glowcap fungus is in PlantFoodRaw
//TODO: hyperlinks to FlavorDefs

//fixedIngredientFilter: which items are allowed
//defaultIngredientFilter: default assignment of fixedIngredientFilter
//fixedIngredient: used if fixedIngredientFilter.Count == 1


namespace FlavorText;

/// <summary>
///  the main body of Flavor Text
///     CompFlavor attaches to all meals
///         stores ingredients and recipe data
///         makes and stores new flavor labels
///         makes and stores new flavor descriptions
/// </summary>

public class CompFlavor : ThingComp
{
    // ingredient list of the meal
    private List<Thing> ingredients = [];
    public List<Thing> Ingredients
    {
        get
        {
            return ingredients;
        }
        set
        {
            ingredients = value;
        }
    }

    public List<string> flavorLabels = [];
    public string finalFlavorLabel;  // final human-readable label for the meal

    public List<string> flavorDescriptions = [];
    public string finalFlavorDescription;  // final human-readable description for the meal

    public List<FlavorDef> flavorDefs = [];  // final chosen FlavorDef for the meal

    public bool fail = false;

    public CompProperties_Flavor Props => (CompProperties_Flavor)props;

    // when MakeRecipeProducts notifies you that the meal ingredients are available to be read, make all the Flavor Text names
    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (signal == "IngredientsRegistered")
        {
            Log.Message("getting flavor text...");
            GetFlavorText();
        }
    }

    // if there's a flavor label made, transform the original meal label into it
    public override string TransformLabel(string label)
    {
        // if the displayed flavor label was already compiled, get it
        if (!finalFlavorLabel.NullOrEmpty() && fail == false)
        {
            return finalFlavorLabel;
        }
        // otherwise return the original label
        return label;
    }

    // if you've successfully created a new flavor label, move the original name down
    public override string CompInspectStringExtra()
    {
        if (!finalFlavorLabel.NullOrEmpty() && fail == false)
        {
            StringBuilder stringBuilder = new();
            string typeLabel = GenText.CapitalizeAsTitle(parent.def.label);
            stringBuilder.AppendLine(typeLabel);
            return stringBuilder.ToString().TrimEndNewlines();
        }
        return null;
    }

    // display the FlavorDef description
    public override string GetDescriptionPart()
    {
        if (finalFlavorLabel.NullOrEmpty())
        {
            Log.Error("Could not get FlavorText description because FlavorText label is blank or null");
            return "";
        }
        if (finalFlavorDescription.NullOrEmpty())
        {
            Log.Error("Could not get FlavorText description because FlavorText description is blank or null.");
            return "";
        }
        return finalFlavorDescription;
    }

    // include the final flavor text data in game save files
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref ingredients, "ingredientsFlavor", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && ingredients == null)
        {
            ingredients = [];
        }
        Scribe_Collections.Look(ref flavorDefs, "flavorDefs");
        Scribe_Collections.Look(ref flavorLabels, "flavorLabels");
        Scribe_Collections.Look(ref flavorDescriptions, "flavorDescriptions");

        Scribe_Values.Look(ref finalFlavorLabel, "finalFlavorLabel");
        Scribe_Values.Look(ref finalFlavorDescription, "finalFlavorDescription");
    }


    // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    public FlavorWithIndices AcquireFlavor(List<Thing> ingredientsToSearchFor)
    {
        if (ingredientsToSearchFor == null)
        {
            Log.Error("Ingredients to search for are null");
            return null;
        }
        if (FlavorDefs == null)
        {
            Log.Error("List of Flavor Defs is null");
            return null;
        }
        if (FlavorDefs.Count == 0)
        {
            Log.Error("List of Flavor Defs is empty");
            return null;
        }

        //see which FlavorDefs match with the ingredients in the meal
        List<FlavorWithIndices> matchingFlavors = [];
        foreach (FlavorDef flavorDef in FlavorDefs)
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
        FlavorWithIndices matchingFlavor = new(flavorDef, [])
        {
            flavorDef = flavorDef
        };
        if (ingredientsToSearchFor.Count == matchingFlavor.flavorDef.ingredients.Count)  // FlavorDef recipe must be same # ingredients as meal
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
        for (int j = 0; j < matchingFlavor.flavorDef.ingredients.Count; j++)  // compare the given ingredient to the FlavorDef's ingredients to see which it matches best with
        {
            // if this FlavorDef ingredient was already matched with, skip it
            if (matchingFlavor.indices.Contains(j))
            {
                continue;
            }
            ThingFilter filter = matchingFlavor.flavorDef.ingredients[j].filter;
            List<string> categories = FlavorDef.GetFilterCategories(filter);
            if (categories != null)
            {
                filter.ResolveReferences();  // TODO: find a way to get rid of this
            }
            if (!matchingFlavor.flavorDef.ingredients[j].filter.Allows(ingredient))
            {
                continue;
            }

            // if you matched with a fixed ingredient, that's the best
            if (matchingFlavor.flavorDef.ingredients[j].IsFixedIngredient)
            {
                lowestIndex = j;
                break;
            }
            if (lowestIndex != -1)
            {
                // if the current FlavorDef ingredient is the most specific so far, mark its index
                if (matchingFlavor.flavorDef.ingredients[j].filter.AllowedDefCount < matchingFlavor.flavorDef.ingredients[lowestIndex].filter.AllowedDefCount)
                {
                    /*Log.Message("found new best ingredient " + matchingFlavor.flavorDef.ingredients[j]);*/
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
            matchingFlavors.SortByDescending((FlavorWithIndices entry) => entry.flavorDef.specificity);
            foreach (FlavorWithIndices entry in matchingFlavors) { Log.Message(entry.flavorDef.label + " = " + entry.flavorDef.specificity); }
            Log.Warning($"Best flavor was {matchingFlavors.Last().flavorDef.label}");
            return new FlavorWithIndices(matchingFlavors.Last().flavorDef, matchingFlavors.Last().indices);
        }
        Log.Error("No valid flavorDefs to choose from");
        return null;
    }

    private List<string> CleanupIngredientLabels(FlavorWithIndices flavor, List<Thing> ingredients, string flag)  // remove unnecessary bits of the ingredient labels, like "meat" and "raw"; may edit flavor label and description strings
    {
        string unfilledString = flavor.flavorDef.GetType().GetField(flag).GetValue(flavor.flavorDef)
            .ToString();
        List<string> ingredientLabels = [];
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
                            flavor.flavorDef.GetType().GetField(flag).SetValue(flavor.flavorDef, unfilledString);
                            cleanLabel = "twisted";
                        }
                        break;
                    }

                case "Meat_Human":
                    {
                        if (flag == "description") { cleanLabel = "human meat"; }
                        else if (Regex.IsMatch(flavor.flavorDef.label, @"(?i)\{" + i + @"\}($| with[^a-zA-Z]|[^a-zA-Z ])")) { cleanLabel = "long pork"; }
                        else
                        {
                            unfilledString = Regex.Replace(unfilledString, @"(?i)(\{" + i + @"\}) and ", "$1 ");
                            flavor.flavorDef.GetType().GetField(flag).SetValue(flavor.flavorDef, unfilledString);
                            cleanLabel = "cannibal";
                        }
                        break;
                    }

                case "Meat_Megaspider":
                    {
                        if (flag == "description") { cleanLabel = "bug guts"; }
                        else if (Regex.IsMatch(flavor.flavorDef.label, @"(?i)\{" + i + @"\}($| with[^a-zA-Z]|[^a-zA-Z ])")) { cleanLabel = "bug guts"; }
                        else
                        {
                            unfilledString = Regex.Replace(unfilledString, @"(?i)(\{" + i + @"\}) and ", "$1 ");
                            flavor.flavorDef.GetType().GetField(flag).SetValue(flavor.flavorDef, unfilledString);
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
                string unfilledString = flavor.flavorDef.GetType().GetField(flag).GetValue(flavor.flavorDef)
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

    private List<Thing> SortIngredientsAndFlavor(FlavorWithIndices flavor, List<Thing> ingredientGroup)  // sort ingredients by the flavor indices, then the placeholders, then the indices themselves
    {
        // sort everything by flavor indices (ascending)
        ingredientGroup = [.. ingredientGroup.OrderBy((Thing ing) => flavor.indices[ingredientGroup.IndexOf(ing)])];  // sort ingredients by indices
        flavor.indices.Sort();  // sort indices 0-n
        List<Thing> meat = [];
        List<int> meatIndices = [];
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
            if (fail == true)
            {
                return;
            }

            // if you haven't tried to find a label before, do so
            if (flavorLabels.NullOrEmpty() && fail == false)
            {
                Log.Message("Getting ingredients");
                if (Ingredients.Count == 0 || Ingredients == null)
                {
                    Log.Warning("Meal has no ingredients, returning original label");
                    Log.Message(Ingredients);
                    fail = true;
                    return;
                }
                Ingredients = [.. Ingredients.OrderBy((Thing ing) => ing, new IngredientComparer())];  // sort the ingredients by category

                // TODO: simplify this section
                // divide the ingredients into groups by pseudorandomly pulling ingredients from the ingredient list
                List<Thing> ingredientsCopy = Ingredients.Select((Thing ingredient) => ingredient).ToList();
                List<List<Thing>> ingredientsSplit = [];
                foreach (Thing ing in Ingredients)
                {
                    Log.Message($"ing: {ing.Label}");
                }
                if (ingredientsCopy.Count > 0)
                {
                    int pseudo = GenText.StableStringHash(ingredientsCopy[0].def.defName);

                    // form groups of size 3 (default)
                    while (ingredientsCopy.Count > 0)
                    {
                        List<Thing> ingredientGroup = [];
                        for (int j = 0; j < maxNumIngredientsFlavor; j++)
                        {
                            pseudo = new Random(WorldSeed + pseudo).Next(ingredientsCopy.Count); // get a pseudorandom index that isn't bigger than the ingredients list count
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
                List<FlavorWithIndices> bestFlavors = [];
                foreach (List<Thing> ingredientGroup in ingredientsSplit)
                {
                    FlavorWithIndices bestFlavor = AcquireFlavor(ingredientGroup);  // get best Flavor Def from all possible matching Flavor Defs
                    if (bestFlavor == null)
                    {
                        fail = true;
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
                        flavorDefs.Add(bestFlavors[i].flavorDef);
                        List<Thing> ingredientGroupSorted = SortIngredientsAndFlavor(bestFlavors[i], ingredientsSplit[i]);
                        List<string> ingredientLabelsForLabel = CleanupIngredientLabels(bestFlavors[i], ingredientGroupSorted, "label");  // make flavor labels look nicer for main label
                        string flavorLabel = FillInCategories(bestFlavors[i], ingredientLabelsForLabel, "label");  // replace placeholders in the flavor label with the corresponding ingredient in the meal
                        flavorLabels.Add(flavorLabel);
                        CompileFlavorLabels();

                        List<string> ingredientLabelsForDescription = CleanupIngredientLabels(bestFlavors[i], ingredientGroupSorted, "description");  // make flavor labels look nicer for main description
                        string flavorDescription = FillInCategories(bestFlavors[i], ingredientLabelsForDescription, "description");  // replace placeholders in the flavor description with the corresponding ingredient in the meal
                        flavorDescriptions.Add(flavorDescription);
                        int pseudo = GenText.StableStringHash(bestFlavors[i].flavorDef.defName);
                        CompileFlavorDescriptions();

                    }
                    else
                    {
                        Log.Error("One of the chosen FlavorDefs is null, skipping.");
                    }
                }
            }

            // if you failed to find a label, return FAIL so you don't try again later
            if (finalFlavorLabel.NullOrEmpty())
            {
                fail = true;
            }
        }
        catch (Exception e)
        {
            Log.Error("Encountered an error transforming the label, returning original label. Error was: " + e);
            fail = true;
        }
    }

    private void CompileFlavorLabels()
    {
        // compile the flavor labels into one long displayed flavor label
        if (!flavorLabels.NullOrEmpty() && fail == false)
        {
            StringBuilder stringBuilder = new();
            string conj;
            for (int j = 0; j < flavorLabels.Count; j++)
            {
                if (j == 0) { conj = ""; }
                else if (j == 1) { conj = " with "; }
                else { conj = " and "; }
                stringBuilder.Append(conj + GenText.CapitalizeAsTitle(flavorLabels[j]));
                /*stringBuilder.AppendInNewLine(GenText.CapitalizeAsTitle(label)); // old label in new line so it shows up in search results but not on the label // TODO: the newline shows up in other places like when a pawn is carrying the meal */
            }
            finalFlavorLabel = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
    }
    private void CompileFlavorDescriptions()
    {
        // compile the flavor labels into one long displayed flavor label
        if (!flavorDescriptions.NullOrEmpty() && fail == false)
        {
            StringBuilder stringBuilder = new();
            for (int j = 0; j < flavorDescriptions.Count; j++)
            {
                string flavorDescription = "";
                if (j == 0)  // if it's the 1st description, use the full description
                {
                    flavorDescription = flavorDescriptions[j];
                }
                else if (j > 0)  // if it's the 2nd+ description, use the label instead and connect it with a side dish connector clause
                {
                    RulePackDef sideDishClauses = Props.sideDishClauses;
                    GrammarRequest request = default;
                    if (sideDishClauses != null) { request.Includes.Add(sideDishClauses); }
                    string sideDishText = GrammarResolver.Resolve("sidedish", request);  // get a random connector sentence
                    flavorDescription = string.Format(sideDishText, flavorLabels[j]);  // place the current flavor description in its placeholder spot within the sentence
                }

                if (!flavorDescription.NullOrEmpty())
                {
                    flavorDescription = flavorDescription.Trim(',', ' ');
                    flavorDescription = flavorDescription.EndWithPeriod();
                    flavorDescription = GenText.CapitalizeSentences(flavorDescription);
                    stringBuilder.AppendWithSeparator(flavorDescription, " ");
                }
            }
            finalFlavorDescription = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
    }

    // merge 4 flavor variables: Ingredients, label, description, FlavorDefs, then recalculate finalFlavorLabel and finalFlavorDescription
    public override void PreAbsorbStack(Thing otherStack, int count)
    {
        base.PreAbsorbStack(otherStack, count);

        foreach (Thing Ingredient in Ingredients)
        {
            if (!Ingredients.Contains(Ingredient)) { Ingredients.Add(Ingredient); }
        }

        foreach (FlavorDef otherFlavorDef in otherStack.TryGetComp<CompFlavor>().flavorDefs)
        {
            if (!flavorDefs.Contains(otherFlavorDef)) { flavorDefs.Add(otherFlavorDef); }
        }

        foreach (string otherFlavorLabel in otherStack.TryGetComp<CompFlavor>().flavorLabels)
        {
            if (!flavorLabels.Contains(otherFlavorLabel)) { flavorLabels.Add(otherFlavorLabel); }
        }
        CompileFlavorLabels();

        foreach (string otherFlavorDescription in otherStack.TryGetComp<CompFlavor>().flavorDescriptions)
        {
            if (!flavorDescriptions.Contains(otherFlavorDescription)) { flavorDescriptions.Add(otherFlavorDescription); }
        }
        CompileFlavorDescriptions();
    }

    // split all 6 stored flavor variables
    public override void PostSplitOff(Thing piece)
    {
        base.PostSplitOff(piece);
        if (piece != parent)
        {
            piece.TryGetComp<CompFlavor>().Ingredients = Ingredients;
            piece.TryGetComp<CompFlavor>().flavorDefs = flavorDefs;
            piece.TryGetComp<CompFlavor>().flavorLabels = flavorLabels;
            piece.TryGetComp<CompFlavor>().flavorDescriptions = flavorDescriptions;
            piece.TryGetComp<CompFlavor>().finalFlavorLabel = finalFlavorLabel;
            piece.TryGetComp<CompFlavor>().finalFlavorDescription = finalFlavorDescription;
        }
    }

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
            Log.Error("Unable to compare some ingredients, missing defName or bad thingCategories");
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
}
