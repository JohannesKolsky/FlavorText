using RimWorld;
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
//--TODO: fish?
//--TODO: flavor descriptions
//--TODO: buttered "and" is disappearing again X(
//--TODO: learn RulePacks
//--TODO: split {Root} category from {FoodRaw}; this will allow for easy special treatment of non {FoodRaw} ingredients
//--TODO: \n not working in descriptions?
//--TODO: merging stacks doesn't change the meal name
//--TODO: merge->pickup erases flavor data (is it because when you merge the one merged in loses all its data?)
//--TODO: specific meat type overrides (and overrides in general)
//xxTODO: noun, plural, adj form of each resource (if none given, use regular label); this can then replace all the rearranging with meats and such; cross-reference label and defName to get singular form; cannibal and twisted appear in descriptions
//--TODO: Vanilla Expanded compat: canned meat -> canned (special case), gourmet meals (condiment is an ingredient), desserts (derived from ResourceBase), etc
//--TODO: RawMeat simplification mod: "raw meat" shows up as "raw"
//xxTODO: trigger GetFlavorText on finding a non-empty ingredients list
//--TODO: generalize meat substitution
//--TODO: VegetableGarden: Garden Meats
//--TODO: different eggs don't merge

//--RELEASE: side dish clauses isn't working
//--RELEASE: check for that null bug again
//--RELEASE: merge bug is happening again
//--RELEASE: eggs aren't getting into FT_Eggs
//xxTODO: does linking ingredients to CompIngredients.ingredients cause problems when merging or splitting meals?  // xx no
//--RELEASE: meals without flavor text loaded from a save don't get loaded properly: saved variables are null, GetFlavorText isn't triggered, etc
//--RELEASE: agave is in PlantFoodRaw

//--RELEASE: build as release build
//--RELEASE: update XML files
//--RELEASE: check add to game
//--RELEASE: check remove from game  //xxRELEASE: throws error on float menu for new meals until save and reload in vanilla  xx not from FlavorText
//--RELEASE: check new game
//--RELEASE: check save and reload game
//--RELEASE: check updating FlavorText on save
//--RELEASE: merging vanilla blank with FT meals
//--RELEASE: vanilla blank meals  --> load from save, name still appears (is this bad?)  xx moot point

//TODO: options to prevent merging meals
//TODO: baby food is derived from OrganicProductBase
//TODO: meat doesn't get sorted to the front when there's a veggie {Food} in front of it
//TODO: nested rules: sausage --> [sausage] --> [meat] // 3-ingredients -> 2[1]
//TODO: allow old label to show up in map search
//TODO: change job string? does this add anything?
//TODO: stinker fungus (VCE_Mushrooms) is in Foods, but glowcap fungus is in PlantFoodRaw
//TODO: hyperlinks to FlavorDefs
//TODO: overrides
//TODO: CompFlavor only applies to direct child ThingDefs of FoodMeals
//TODO: revise fail system
//TODO: variety matters warnings and errors?
//TODO: add nutrient paste meals to CompFlavor

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
    public List<ThingDef> Ingredients => parent.TryGetComp<CompIngredients>().ingredients;

    public List<string> flavorLabels = [];
    public string finalFlavorLabel;  // final human-readable label for the meal

    public List<string> flavorDescriptions = [];
    public string finalFlavorDescription;  // final human-readable description for the meal

    public List<FlavorDef> flavorDefs = [];  // final chosen FlavorDef for the meal

    public bool fail = false;  // has an attempt been made to find a flavorLabel and failed? if so, don't ever try again

    // should FlavorText apply to this meal? Not everything with a CompFlavor gets FlavorText (yes this is messy, but it's the easiest way atm)
    public bool HasFlavorText => parent.HasComp<CompFlavor>();

    public CompProperties_Flavor Props => (CompProperties_Flavor)props;

    // when MakeRecipeProducts notifies you that the meal ingredients are available to be read, make all the Flavor Text names
    public override void ReceiveCompSignal(string signal)
    {
        base.ReceiveCompSignal(signal);
        if (signal == "IngredientsRegistered")
        {
            /*            Log.Warning($"Ingredients registered");*/
            GetFlavorText();
        }
    }


    // if there's a flavor label made, transform the original meal label into it
    public override string TransformLabel(string label)
    {
        // if you haven't failed, check for a flavor label or make one
        if (fail == false)
        {
            if (!finalFlavorLabel.NullOrEmpty())
            {
                return finalFlavorLabel;
            }
        }
        // otherwise return the original label
        return base.TransformLabel(label);
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
        if (fail == true) { return base.GetDescriptionPart(); }
        else if (finalFlavorLabel.NullOrEmpty())
        {
            Log.Message("->Could not get FlavorText description because FlavorText label is blank or null");
            return "";
        }
        else if (finalFlavorDescription.NullOrEmpty())
        {
            Log.Message("->Could not get FlavorText description because FlavorText description is blank or null.");
            return "";
        }
        return finalFlavorDescription;
    }

    // include the final flavor text data in game save files
    public override void PostExposeData()
    {
        /*        Log.Warning($"PostExposeData for {parent.ThingID}");*/
/*        base.PostExposeData();
        Scribe_Values.Look(ref fail, "fail");*/

        /*        Scribe_Collections.Look(ref ingredients, "ingredientsFlavor", LookMode.Def);
                if (Scribe.mode == LoadSaveMode.PostLoadInit && ingredients.NullOrEmpty())
                {
                    Log.Warning($"ingredients in CompFlavor was null or empty, reacquiring...");
                    ingredients = [];
                    try
                    {
                        foreach (ThingDef thingDef in parent.TryGetComp<CompIngredients>().ingredients)
                        {
                            ingredients.Add(thingDef);
                        }
                    }
                    catch (Exception) { throw; }
                }*/

/*        Scribe_Collections.Look(ref flavorDefs, "flavorDefs");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && flavorDefs == null)
        {
            flavorDefs = [];
        }
        Scribe_Collections.Look(ref flavorLabels, "flavorLabels");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && flavorLabels == null)
        {
            flavorLabels = [];
        }
        Scribe_Collections.Look(ref flavorDescriptions, "flavorDescriptions");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && flavorDescriptions == null)
        {
            flavorDescriptions = [];
        }
        Scribe_Values.Look(ref finalFlavorLabel, "finalFlavorLabel");
        Scribe_Values.Look(ref finalFlavorDescription, "finalFlavorDescription");*/
    }


    // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    public FlavorWithIndices AcquireFlavor(List<ThingDef> ingredientsToSearchFor)
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

    private FlavorWithIndices IsMatchingFlavor(List<ThingDef> ingredientsToSearchFor, FlavorDef flavorDef)  // check if all the ingredients match the given FlavorDef
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

    private FlavorWithIndices BestIngredientMatch(ThingDef ingredient, FlavorWithIndices matchingFlavor)  // find the best match for the current single ingredient in the current FlavorDef
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
            /*            foreach (FlavorWithIndices entry in matchingFlavors) { Log.Message(entry.flavorDef.label + " = " + entry.flavorDef.specificity); }*/
            /*            Log.Message($"Best flavor was {matchingFlavors.Last().flavorDef.label}");*/
            return new FlavorWithIndices(matchingFlavors.Last().flavorDef, matchingFlavors.Last().indices);
        }
        Log.Error("No valid flavorDefs to choose from");
        return null;
    }

    private string FormatFlavorString(FlavorWithIndices flavor, List<ThingDef> ingredients, string flag)  // replace placehodlers in flavor label/description with the correctly inflected ingredient label
    {
        try
        {
            // get label or description depending on "flag"
            string flavorString = flavor.flavorDef.GetType().GetField(flag).GetValue(flavor.flavorDef).ToString();
            Log.Message($"flavorString is {flavorString}");

            // find placeholders and replace them with appropriate ingredient and inflection
            List<string> flavorStringSplit = [.. flavorString.Split(' ')];
            List<string> temp = [];
            Tuple<string, string, string, string> inflections;
            for (int i = 0; i < flavorStringSplit.Count; i++)
            {
                Match digits = Regex.Match(flavorStringSplit[i], "\\d+");
                if (digits.Success)
                {
                    int index = int.Parse(digits.Value);
                    inflections = ThingCategoryDefUtilities.ingredientInflections[ingredients[index]];
                    Log.Message($"ingredient was {ingredients[index]}");

                    flavorStringSplit[i] = Regex.Replace(flavorStringSplit[i], "\\{" + index + "_plur\\}", inflections.Item1);
                    flavorStringSplit[i] = Regex.Replace(flavorStringSplit[i], "\\{" + index + "_coll\\}", inflections.Item2);
                    flavorStringSplit[i] = Regex.Replace(flavorStringSplit[i], "\\{" + index + "_sing\\}", inflections.Item3);
                    flavorStringSplit[i] = Regex.Replace(flavorStringSplit[i], "\\{" + index + "_adj\\}", inflections.Item4);
                }

                // remove words repeated directly after each other
                if (temp.NullOrEmpty() || Remove.RemoveDiacritics(flavorStringSplit[i]).ToLower() != Remove.RemoveDiacritics(temp.Last()).ToLower())
                {
                    temp.Add(flavorStringSplit[i]);
                }
            }
            flavorString = string.Join(" ", temp);
            return flavorString;
        }
        catch (Exception e) { Log.Error($"Error when formatting flavor {flag}: reason: {e}"); fail = true; return null; }
    }

    private List<ThingDef> SortIngredientsAndFlavor(FlavorWithIndices flavor, List<ThingDef> ingredientGroup)  // sort ingredients by the flavor indices, then the placeholders, then the indices themselves
    {
        // sort everything by flavor indices (ascending)
        ingredientGroup = [.. ingredientGroup.OrderBy((ThingDef ing) => flavor.indices[ingredientGroup.IndexOf(ing)])];  // sort ingredients by indices
        flavor.indices.Sort();  // sort indices 0-n
        List<ThingDef> meat = [];
        List<int> meatIndices = [];
        for (int i = 0; i < ingredientGroup.Count; i++)
        {
            if (DefDatabase<ThingCategoryDef>.GetNamed("MeatRaw").ContainedInThisOrDescendant(ingredientGroup[i]))
            {
                meat.Add(ingredientGroup[i]);
                meatIndices.Add(flavor.indices[i]);
            }
        }
        if (meat.Count > 1)
        {
            meat = meat.OrderBy((ThingDef m) => m, new MeatComparer()).ToList();
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
                /*                Log.Warning($"Failed before, cancelling search");*/
                return;
            }

            else if (!HasFlavorText)
            {
                /*                Log.Message($"Parent should not receive flavor text, cancelling the search.");*/
                fail = true;
                return;
            }

            // if you haven't tried to find a label before, do so
            if (flavorDefs.NullOrEmpty())
            {
                // if no ingredients, fail
                if (Ingredients.NullOrEmpty())
                {
                    /*                    Log.Message("List of ingredients for the meal in CompIngredients was empty or null, cancelling the search.");*/
                    fail = true;
                    return;
                }
                // make a new list of ingredients that are in flavorRoot (default FT_Foods)
                List<ThingDef> ingredientsFoods = [];
                foreach (ThingDef ingredient in Ingredients)
                {
                    if (ingredient != null && ThingCategoryDefUtilities.flavorRoot.ContainedInThisOrDescendant(ingredient))
                    {
                        ingredientsFoods.Add(ingredient);
                    }
                }
                ingredientsFoods = [.. ingredientsFoods.OrderBy((ThingDef ing) => ing, new IngredientComparer())];  // sort the ingredients by category

                // TODO: simplify this section so you don't have to use a copy of the ingredients list
                // divide the ingredients into groups by pseudorandomly pulling ingredients from the ingredient list
                List<ThingDef> ingredientsCopy = ingredientsFoods.Select((ThingDef ingredient) => ingredient).ToList();
                List<List<ThingDef>> ingredientsSplit = [];
                if (ingredientsCopy.Count > 0)
                {
                    int pseudo = GenText.StableStringHash(ingredientsCopy[0].defName);

                    // form groups of size 3 (default)
                    while (ingredientsCopy.Count > 0)
                    {
                        List<ThingDef> ingredientGroup = [];
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
                List<FlavorWithIndices> bestFlavors = [];
                for (int k = 0; k < ingredientsSplit.Count; k++)
                {
                    List<ThingDef> ingredientGroup = ingredientsSplit[k];
                    FlavorWithIndices bestFlavor = AcquireFlavor(ingredientGroup);  // get best Flavor Def from all possible matching Flavor Defs
                    if (bestFlavor == null)
                    {
                        Log.Error($"AcquireFlavor failed to find a best flavor for the ingredient group with index of {k}, skipping that ingredient group.");
                        continue;
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
                        List<ThingDef> ingredientGroupSorted = SortIngredientsAndFlavor(bestFlavors[i], ingredientsSplit[i]);
                        string flavorLabel = FormatFlavorString(bestFlavors[i], ingredientGroupSorted, "label");  // make flavor labels look nicer for main label; replace placeholders in the flavor label with the corresponding ingredient in the meal
                        if (flavorLabel.NullOrEmpty()) { Log.Error($"FormatFlavorString failed to get a formatted flavor label for the ingredient group with index of {i}, skipping that ingredient group."); continue; }
                        flavorLabels.Add(flavorLabel);

                        string flavorDescription = FormatFlavorString(bestFlavors[i], ingredientGroupSorted, "description");  // make flavor labels look nicer for main description; replace placeholders in the flavor description with the corresponding ingredient in the meal
                        flavorDescriptions.Add(flavorDescription);
                        int pseudo = GenText.StableStringHash(bestFlavors[i].flavorDef.defName);

                    }
                    else
                    {
                        Log.Error($"A chosen FlavorDef with index of {i} is null, skipping it and continuing with the rest.");
                        continue;
                    }
                }
                CompileFlavorLabels();
                CompileFlavorDescriptions();
            }

            // if you failed to find a label, set fail so you don't try again later
            if (finalFlavorLabel.NullOrEmpty())
            {
                Log.Error($"The final compiled and formatted flavor label was null or empty, cancelling the search.");
                fail = true;
                return;
            }
        }
        catch (Exception e)
        {
            Log.Error("Encountered an error while attempting to get all flavor text, cancelling the search. Error was: " + e);
            fail = true;
            return;
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
                conj = j == 0 ? "" : j == 1 ? " with " : " and ";
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
                if (j == 0)  // if it's the 1st description, use the description
                {
                    flavorDescription = FormatDescription(flavorDescriptions[j]);
                    stringBuilder.Append(flavorDescription);
                }
                else if (j > 0)  // if it's the 2nd+ description, in a new paragraph, use a side dish connector clause, then the description
                {
                    RulePackDef sideDishClauses = Props.sideDishClauses;
                    GrammarRequest request = default;
                    if (sideDishClauses != null) { request.Includes.Add(sideDishClauses); }
                    string sideDishText = GrammarResolver.Resolve("sidedish", request);  // get a random connector sentence
                    string flavorDescriptionConnector = string.Format(sideDishText, flavorLabels[j]);  // place the current flavor label in its placeholder spot within the sentence
                    flavorDescriptionConnector = FormatDescription(flavorDescriptionConnector);
                    stringBuilder.AppendWithSeparator(flavorDescriptionConnector, " ");
                }
            }
            finalFlavorDescription = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
    }

    private static string FormatDescription(string flavorDescription)
    {
        if (!flavorDescription.NullOrEmpty())
        {
            flavorDescription = flavorDescription.Trim(',', ' ');
            flavorDescription = flavorDescription.EndWithPeriod();
            flavorDescription = GenText.CapitalizeSentences(flavorDescription);
        }

        return flavorDescription;
    }

    // merge 4 flavor variables: Ingredients, label, description, FlavorDefs, then recalculate finalFlavorLabel and finalFlavorDescription
    public override void PreAbsorbStack(Thing otherStack, int count)  // otherStack is the item being added to the stack
    {
        try
        {
            /*            if (otherStack.HasComp<CompFlavor>()) { Log.Warning("otherstack has CompFlavor"); }*/
            CompFlavor otherCompFlavor = otherStack.TryGetComp<CompFlavor>();
            flavorLabels = [];
            finalFlavorLabel = null;
            flavorDescriptions = [];
            finalFlavorDescription = null;
            flavorDefs = [];
            fail = false;
            GetFlavorText();

/*
            if (otherCompFlavor.fail == false) { fail = false; }

            foreach (FlavorDef otherFlavorDef in otherCompFlavor.flavorDefs)
            {
                if (!flavorDefs.Contains(otherFlavorDef)) { flavorDefs.Add(otherFlavorDef); }
            }

            for (int i = 0; i < otherCompFlavor.flavorLabels.Count; i++)
            {
                string otherFlavorLabel = otherCompFlavor.flavorLabels[i];
                if (!flavorLabels.Contains(otherFlavorLabel))
                {
                    flavorLabels.Prepend(otherFlavorLabel);
                    flavorDescriptions.Prepend(otherCompFlavor.flavorDescriptions[i]);
                }
            }
            CompileFlavorLabels();
            CompileFlavorDescriptions();*/

        }
        catch (Exception e) { Log.Error($"Failed to merge stacks properly, reason: {e}"); }
    }

    // split all 6 stored flavor variables
    public override void PostSplitOff(Thing piece)
    {
        try
        {
            base.PostSplitOff(piece);
            if (piece != parent)
            {
                /*                piece.TryGetComp<CompFlavor>().ingredients = ingredients;*/
                piece.TryGetComp<CompFlavor>().flavorDefs = flavorDefs;
                piece.TryGetComp<CompFlavor>().flavorLabels = flavorLabels;
                piece.TryGetComp<CompFlavor>().flavorDescriptions = flavorDescriptions;
                piece.TryGetComp<CompFlavor>().finalFlavorLabel = finalFlavorLabel;
                piece.TryGetComp<CompFlavor>().finalFlavorDescription = finalFlavorDescription;
                piece.TryGetComp<CompFlavor>().fail = fail;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to split stacks properly, reason: {e}");
        }
    }

    public class IngredientComparer : IComparer<ThingDef>
    {
        public int Compare(ThingDef a, ThingDef b)
        {
            // null is considered lowest
            if (a.thingCategories == null && b.thingCategories == null) { return 0; }
            else if (a.thingCategories == null) { return -1; }
            else if (b.thingCategories == null) { return 1; }

            foreach (ThingCategoryDef cat in a.thingCategories)
            {
                // if the ingredients share a category, group them and sort them by shortHash
                return b.thingCategories.Contains(cat) ? a.shortHash.CompareTo(b.shortHash) : a.defName.CompareTo(b.defName);
            }
            Log.Error("Unable to compare some ingredients, missing defName or bad thingCategories");
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
}
