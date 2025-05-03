using RimWorld;
using System;
using System.Collections.Generic;
// ReSharper disable once RedundantUsingDirective
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using Verse.Grammar;
using static FlavorText.CompPropertiesFlavor;

//DONE: make flavor entries a class
//DONE: eggs + eggs makes weird names like omlette w/eggs
//DONE: move databases to xml
//DONE: dynamically build flavor database from all foodstuffs
//DONE: organize flavors tightly: category-->category-->...-->item defName
//DONE: flavor label does not appear while meal is being carried directly from stove to stockpile
//DONE: function to remove duplicate ingredients
//DONE: default to vanilla name on null
//DONE: formatted strings
//DONE: track which ingredients will go into a placeholder
//DONE: store indices and everything else directly in fields of CompFlavor
//DONE: put tags for empty flavor descriptions
//DONE: handle arbitrary # ingredients: give each ingredient ranked-choice of what it matches with; assign most accurate matches first; if ingredients remain run again with remainder; join all final labels together
//DONE: swapping an ingredient with another of the same category should result in the same dish; sort by categories, not hash codes
//DONE: check if contains parent category
//DONE: fish?
//DONE: flavor descriptions
//DONE: buttered "and" is disappearing again X(
//DONE: learn RulePacks
//DONE: split {Root} category from {FoodRaw}; this will allow for easy special treatment of non {FoodRaw} ingredients
//DONE: \n not working in descriptions?
//DONE: merging stacks doesn't change the meal name
//DONE: merge->pickup erases flavor data (is it because when you merge the one merged in loses all its data?)
//DONE: specific meat type overrides (and overrides in general)
//DONE: noun, plural, adj form of each resource (if none given, use regular label); this can then replace all the rearranging with meats and such; cross-reference label and defName to get singular form; cannibal and twisted appear in descriptions
//DONE: Vanilla Expanded compat: canned meat -> canned (special case), gourmet meals (condiment is an ingredient), desserts (derived from ResourceBase), etc
//DONE: RawMeat simplification mod: "raw meat" shows up as "raw"
//DONE: trigger TryGetFlavorText on finding a non-empty ingredients list
//DONE: generalize meat substitution
//DONE: VegetableGarden: Garden Meats
//DONE: different eggs don't merge
//DONE: CompFlavor only applies to direct child ThingDefs of FoodMeals
//DONE: meals > than a single stack sent in a drop pod split into 2 stacks, one of which fails to get a flavor name despite keeping its ingredients
//DONE: pawn spawned with meals, those meals don't get flavor text until save and reload
//DONE: stinker fungus (VCE_Mushrooms) is in Foods, but glowcap fungus is in PlantFoodRaw
//DONE: no compFlavor for nutrient paste meals for now

//RELEASED: side dish clauses isn't working
//RELEASED: check for that null bug again
//RELEASED: merge bug is happening again
//RELEASED: eggs aren't getting into FT_Eggs
//DONE: does linking ingredients to CompIngredients.ingredients cause problems when merging or splitting meals?  // xx no
//RELEASED: meals without flavor text loaded from a save don't get loaded properly: saved variables are null, TryGetFlavorText isn't triggered, etc
//RELEASED: agave is in PlantFoodRaw
//RELEASED: vanilla blank meals  --> load from save, name still appears (is this bad?)  xx moot point
//RELEASED: when CommonSense random ingredients is enabled, random ingredients are added to cooked meals; prob caused by interference of Harmony MakeThing PostFixes
//RELEASED: merging vanilla blank with FT meals
//DONE: chicken twisted sausage: wrong meat order
//DONE: baby food is derived from OrganicProductBase
//DONE: change job string? does this add anything?  // xx doesn't jive with hourOfDay
//DONE: allow old label to show up in map search
//DONE: overrides
//DONE: condiments shouldn't be a full ingredient (FT_Foods -> FT_FoodRaw)
//DONE: revise fail system  // if keeping fail, fail as a string would be useful: fail = "ingredientsEmpty"
//DONE: full egg labels
//DONE: meat doesn't get sorted to the front when there's a veggie {Food} in front of it


//RELEASED: build as release build
//RELEASED: update XML files
//RELEASED: check add to game
//RELEASED: check remove from game
//RELEASED: check new game
//RELEASED: check save and reload game
//RELEASED: check updating FlavorText on save
//RELEASED: check all meal types
//RELEASED: check food modlist
//RELEASE: check FTV
//RELEASE: check your own saves
//RELEASED: check CommonSense: starting spawned/drop-podded, drop pod meals, trader meals
//RELEASED: disable log messages
//RELEASED: check startup impact
//RELEASED: check gameplay impact

//DONE: load warning when FlavorDef MealCategories element is missing

//TODO: time and cooking station aren't working atm

//TODO: revise FlavorWithIndices system: you probably don't need a separate class for this
//TODO: check for cooking station and time in ValidFlavorDefsForMealType: this is a fast way to discard invalid FlavorDefs // OR check in CheckIfFlavorMatches before checking ingredients
//TODO: options to prevent merging meals
//TODO: hyperlinks to FinalFlavorDefs
//TODO: variety matters warnings and errors?
//TODO: null ingredient option: e.g. if an ingredient is optional  // but the name will probably change, so isn't a new FlavorDef better?
//TODO: milk/cheese problem; in a mod with specialty cheeses, that name should be included, but otherwise milk should produce the word "cheese"

//fixedIngredientFilter: which items are allowed
//defaultIngredientFilter: default assignment of fixedIngredientFilter
//fixedIngredient: used if fixedIngredientFilter.Count == 1


namespace FlavorText;

/// <summary>
///  the main body of Flavor Text
///     CompFlavor attaches automatically to appropriate meals
///         appropriate meals are generic, not specific ones like modded sushi
///         stores ingredients and recipe data
///         makes and stores new flavor labels
///         makes and stores new flavor descriptions
/// </summary>

public class CompFlavor : ThingComp
{

    // ingredients in a pseudo-random order
    public List<ThingDef> Ingredients
    {
        get
        {
            List<ThingDef> ingredients = parent.TryGetComp<CompIngredients>().ingredients;
            List<ThingDef> ingredientsFoods = ingredients.FindAll(i => i != null && ThingCategoryDefUtility.flavorRoot.ContainedInThisOrDescendant(i));  // assemble a list of the ingredients that are actually food
            List<ThingDef> ingredientsSorted = [.. ingredientsFoods.OrderBy(def => def.defName.GetHashCode())];  // sort in a pseudo-random order

            /*            foreach (var ing in ingredientsSorted) { Log.Message($"found {ing.defName} in {parent.ThingID}"); }*/

            return ingredientsSorted;
        }
    }

    public bool TriedFlavorText;
    
    public List<string> FlavorLabels = [];
    public string FinalFlavorLabel;  // final human-readable label for the meal

    public List<string> FlavorDescriptions = [];
    public string FinalFlavorDescription;  // final human-readable description for the meal

    public List<FlavorDef> FinalFlavorDefs = [];  // final chosen FinalFlavorDefs for the meal

    public ThingDef CookingStation;  // which station this meal was cooked on

    public int HourOfDay;  // what hour of the day this meal was completed

    public List<string> Tags = [];

    /*    public bool fail = false;  // has an attempt been made to find a flavorLabel and failed? if so, don't ever try again*/

    // should FlavorText apply to this meal? Not everything with a CompFlavor gets FlavorText (yes this is messy, but it's the easiest way atm)
    public bool HasFlavorText => parent.HasComp<CompFlavor>();

    // ReSharper disable once UnusedMember.Global
    public CompPropertiesFlavor Props => (CompPropertiesFlavor)props;

    /*// for settlement and trade caravan inventories
    public override void PostPostGeneratedForTrader(TraderKindDef trader, int forTile, Faction forFaction)
    {
        base.PostPostGeneratedForTrader(trader, forTile, forFaction);
        Log.Warning("PostPostGeneratedForTrader");
        TryGetFlavorText();
    }

    // after spawning on the map, get flavor text
    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        Log.Warning("PostSpawnSetup");
        TryGetFlavorText();

    }*/

    // if there's a flavor label made, transform the original meal label into it
    public override string TransformLabel(string label)
    {
        if (!TriedFlavorText) TryGetFlavorText();
        return !FinalFlavorLabel.NullOrEmpty() ? $"{FinalFlavorLabel} ({parent.def.label})" : base.TransformLabel(label);
    }

    // display the FlavorDef description
    public override string GetDescriptionPart()
    {
        return FinalFlavorDescription;
    }

    // if you've successfully created a new flavor label, move the original name down
    public override string CompInspectStringExtra()
    {
        if (!FinalFlavorLabel.NullOrEmpty())
        {
            StringBuilder stringBuilder = new();
            string typeLabel = GenText.CapitalizeAsTitle(parent.def.label);
            stringBuilder.AppendLine(typeLabel);
            return stringBuilder.ToString().TrimEndNewlines();
        }
        return null;
    }

    // see which FinalFlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    public FlavorWithIndices AcquireFlavor(List<ThingDef> ingredientsToSearchFor, List<FlavorDef> flavorDefsToSearch)
    {
        if (ingredientsToSearchFor == null)
        {
            Log.Error("Ingredients to search for are null");
            return null;
        }
        if (flavorDefsToSearch.NullOrEmpty())
        {
            Log.Error("List of Flavor Defs is null or empty");
            return null;
        }

        //see which FinalFlavorDefs match with the ingredients in the meal
        List<FlavorWithIndices> matchingFlavors = flavorDefsToSearch.Select(f => CheckIfFlavorMatches(ingredientsToSearchFor, f)).Where(m => m != null).ToList();

        // pick the most specific matching FlavorDef
        FlavorWithIndices flavor = null;
        if (matchingFlavors.Count > 0)
        {
            flavor = ChooseBestFlavor(matchingFlavors);
        }

        // return
        if (flavor == null)
        {
            string errorString = "\ningredients were:";
            for (int i = 0; i < ingredientsToSearchFor.Count; i++)
            {
                ThingDef ingredient = ingredientsToSearchFor[i];
                errorString += $"\n{i} {ingredient.defName}";
            }
            throw new NullReferenceException($"No Flavor Def found that matches one of the ingredient groups. {errorString}");

        }
        return flavor;
    }

    private static FlavorWithIndices CheckIfFlavorMatches(List<ThingDef> ingredientsToSearchFor, FlavorDef flavorDef)  // check if all the ingredients match the given FlavorDef
    {
        try
        {
            FlavorWithIndices matchingFlavor = new(flavorDef, []) { FlavorDef = flavorDef };

            if (flavorDef == null)
            {
                if (Prefs.DevMode) Log.Warning(
                    "Found a null FlavorDef in list of FinalFlavorDefs to search. Probably deprecated from an older version of this mod. Skipping...");
                return null;
            } // if flavorDef is null, skip

            if (ingredientsToSearchFor.Count != matchingFlavor.FlavorDef.ingredients.Count)
            {
                return null;
            } // if flavorDef length doesn't match ingredient list length, cancel

            if (!ingredientsToSearchFor.All(flavorDef.LowestCommonIngredientCategory.ContainedInThisOrDescendant))
            {
                return null;
            } // if ingredients aren't wholly contained within the FinalFlavorDefs lowest common category of ingredients, cancel

            foreach (var ing in ingredientsToSearchFor)
            {
                matchingFlavor =
                    BestIngredientMatch(ing,
                        matchingFlavor); // see if the ingredient fits and if it does, find the most specific match
            }

            return !matchingFlavor.Indices.Contains(-1) ? matchingFlavor : null;
        }
        catch (Exception ex)
        {
            ex.Data["stringInfo"] = $"{flavorDef?.defName} was the FlavorDef that caused the error";
            throw;
        }
    }

    private static FlavorWithIndices BestIngredientMatch(ThingDef ingredient, FlavorWithIndices matchingFlavor)  // find the best match for the current single ingredient in the current FlavorDef
    {
        int lowestIndex = -1;
        for (int j = 0; j < matchingFlavor.FlavorDef.ingredients.Count; j++)  // compare the given ingredient to the FlavorDef's ingredients to see which it matches best with
        {
            // if this FlavorDef ingredient was already matched with, skip it
            if (matchingFlavor.Indices.Contains(j))
            {
                continue;
            }
            ThingFilter filter = matchingFlavor.FlavorDef.ingredients[j].filter;
            List<ThingCategoryDef> categories = FlavorDef.GetFilterCategories(filter, "categories");
            List<ThingCategoryDef> disallowedCategories = FlavorDef.GetFilterCategories(filter, "disallowedCategories");

            filter.ResolveReferences();  // TODO: find a way to get rid of this

            // if a parent category of the ingredient is present in "disallowedCategories" or a child of that, it doesn't fit; skip the category
            if (disallowedCategories.Any(cat => cat.ThisAndChildCategoryDefs.Intersect(ingredient.thingCategories).Any()))
            {
                /*                Log.Message($"Ingredient {ingredient} in {matchingFlavor.flavorDef.defName} is disallowed");*/
                continue;
            }


            // if a parent category of the ingredient is present in "categories" or a child of that, it fits; calculate its specificity
            if (categories.Any(cat => cat.ThisAndChildCategoryDefs.Intersect(ingredient.thingCategories).Any()))
            {
                if (lowestIndex != -1)
                {
                    // if the current FlavorDef ingredient is the most specific so far, mark its index
                    if (matchingFlavor.FlavorDef.ingredients[j].filter.AllowedDefCount < matchingFlavor.FlavorDef.ingredients[lowestIndex].filter.AllowedDefCount)
                    {
                        /*                        Log.Message($"found new best category match {matchingFlavor.flavorDef.ingredients[j]} for {ingredient} in {matchingFlavor.flavorDef.defName}");*/
                        lowestIndex = j;
                    }
                }

                // if this is the first match, mark the index
                else
                {
                    /*                    Log.Message($"found first category match {matchingFlavor.flavorDef.ingredients[j]} for {ingredient} in {matchingFlavor.flavorDef.defName}");*/
                    lowestIndex = j;
                }
            }
        }
        matchingFlavor.Indices.Add(lowestIndex);
        return matchingFlavor;
    }

    private FlavorWithIndices ChooseBestFlavor(List<FlavorWithIndices> matchingFlavors)  // rank valid flavor defs and choose the best one
    {
        if (matchingFlavors.NullOrEmpty()) return null;
        
        matchingFlavors.SortBy(entry => entry.FlavorDef.Specificity);
        //foreach (FlavorWithIndices entry in matchingFlavors) { Log.Message(entry.FlavorDef.defName + " = " + entry.FlavorDef.Specificity); }
        foreach (var flavor in matchingFlavors)
        {
            var flavorDef = flavor.FlavorDef;
            if (!flavorDef.CookingStations.NullOrEmpty() && !flavorDef.CookingStations.Any(cat => cat.ContainedInThisOrDescendant(CookingStation)))  // if wrong cooking station, skip
            {
                //Log.Warning($"cooking station {flavorDef.CookingStations[0]} failed for {flavorDef.defName}");
                continue;
            }
            if (!flavorDef.HoursOfDay.Equals(new IntRange(0, 24)) && (flavorDef.HoursOfDay.min > HourOfDay || HourOfDay > flavorDef.HoursOfDay.max))  // if wrong time, skip
            {
                //Log.Warning($"wrong time of day: range from {flavorDef.HoursOfDay.min} to {flavorDef.HoursOfDay.max}, while the time now is {HourOfDay}");
                continue;
            }
            //Log.Message($"best flavor was {flavorDef.defName} cooked at ");
            return new FlavorWithIndices(flavorDef, flavor.Indices);  // choose the current FlavorDef as the best
        }
        return null;
    }

    private static string FormatFlavorString(FlavorWithIndices flavor, List<ThingDef> ingredients, string flag)  // replace placehodlers in flavor label/description with the correctly inflected ingredient label
    {
        try
        {
            // get label or description depending on "flag"
            string flavorString = flavor.FlavorDef.GetType().GetField(flag).GetValue(flavor.FlavorDef).ToString();

            // find placeholders and replace them with the appropriate inflection of the right ingredient
            for (int i = 0; i < ingredients.Count; i++)
            {
                var inflections = ThingCategoryDefUtility.ingredientInflections[ingredients[i]];
                while (true)
                {
                    var placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_plur\\} *([^ ]*)");
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections.Item1, placeholder);
                        flavorString = Regex.Replace(flavorString, "\\{" + i + "_plur\\}", inflection);
                        continue;
                    }
                    placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_coll\\} *([^ ]*)");
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections.Item2, placeholder);
                        flavorString = Regex.Replace(flavorString, "\\{" + i + "_coll\\}", inflection);
                        continue;
                    }
                    placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_sing\\} *([^ ]*)");
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections.Item3, placeholder);
                        flavorString = Regex.Replace(flavorString, "\\{" + i + "_sing\\}", inflection);
                        continue;
                    }

                    placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_adj\\} *([^ ]*)");
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections.Item4, placeholder);
                        flavorString = Regex.Replace(flavorString, "\\{" + i + "_adj\\}", inflection);
                        continue;
                    }
                    break;
                }
            }

            return flavorString;


            // remove words repeated directly after each other
            static string RemoveRepeatedWords(string inflection, Match placeholder)
            {
                List<string> inflectionSplit = [.. inflection.Split(' ')];

                // if you captured a word before the placeholder, see if it duplicates the first word of "inflection"
                if (placeholder.Groups.Count > 1)
                {
                    if (Remove.RemoveDiacritics(placeholder.Groups[1].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit[0]).ToLower())
                    {
                        inflectionSplit.RemoveAt(0);
                    }
                }

                // if you captured a word after the placeholder, see if it duplicates the last word of "inflection"
                if (placeholder.Groups.Count > 2)
                {
                    if (Remove.RemoveDiacritics(placeholder.Groups[2].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit[inflectionSplit.Count - 1]).ToLower())
                    {
                        inflectionSplit.RemoveAt(inflectionSplit.Count - 1);
                    }
                }

                inflection = string.Join(" ", inflectionSplit);
                return inflection;
            }
        }
        catch (Exception e) { throw new Exception($"Error when formatting flavor {flag}: reason: {e}"); }
    }

    private static List<ThingDef> RearrangeIngredients(FlavorWithIndices flavor, List<ThingDef> ingredientGroup)  // sort ingredients by the flavor indices and rearrange meats so the label flows better
    {
        // sort everything by flavor indices (ascending)
        var group = ingredientGroup;
        ingredientGroup = [.. ingredientGroup.OrderBy((ing) => flavor.Indices[group.IndexOf(ing)])];  // sort ingredients by indices
        flavor.Indices.Sort();  // sort indices 0-n

        // rearrange meat so special meats like human and twisted are in front and get the adjective forms (usually)
        List<ThingDef> meat = [];
        List<int> meatIndices = [];
        for (int i = 0; i < ingredientGroup.Count; i++)
        {
            if (DefDatabase<ThingCategoryDef>.GetNamed("MeatRaw").ContainedInThisOrDescendant(ingredientGroup[i]))
            {
                meat.Add(ingredientGroup[i]);
                meatIndices.Add(flavor.Indices[i]);
            }
        }
        if (meat.Count > 1)
        {
            meat = [.. meat.OrderBy(m => m, new MeatComparer())];
            meatIndices.Sort();
            for (int i = 0; i < meat.Count; i++)
            {
                int sortedMeatIndex = meatIndices[i];
                ingredientGroup[sortedMeatIndex] = meat[i];
            }
        }

        return ingredientGroup;
    }

    public void TryGetFlavorText(List<FlavorDef> flavorDefsToSearch = null)
    {
        /*Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();*/

        try
        {
            TriedFlavorText = true;

            // if no ingredients, return
            if (Ingredients.NullOrEmpty())
            {
                FlavorLabels = [];
                FinalFlavorLabel = null;
                FlavorDescriptions = [];
                FinalFlavorDescription = null;
                FinalFlavorDefs = [];

                if (Prefs.DevMode) Log.Message("List of ingredients for the meal in CompIngredients was empty or null, cancelling the search.");

                return;
            }

            if (!HasFlavorText)
            {
                Log.Error($"Parent {parent.def} does not have CompFlavor, cancelling the search. Please report.");
                throw new Exception("Tried to call TryGetFlavorText() on a meal that has no CompFlavor.");
            }

            // try searching in the FlavorDefs that you were given
            try
            {
                if (!flavorDefsToSearch.NullOrEmpty())
                {
                    GetFlavorText(flavorDefsToSearch);

                    if (!FinalFlavorDefs.NullOrEmpty())
                    {
                        if(Prefs.DevMode) Log.Warning($"Successfully got old Flavor Text for meal {parent.ThingID}");
                        return;
                    }
                    if (Prefs.DevMode) Log.Warning("Old Flavor Text no longer matches. Probably due to an update to this mod. Will attempt to get new Flavor Text.");
                }
            }
            catch (NullReferenceException){}
            
            // otherwise try searching with all valid FlavorDefs
            var validFlavorDefsForMealType = ValidFlavorDefsForMealType(parent.def).ToList();
            if (validFlavorDefsForMealType.NullOrEmpty())
            {
                throw new InvalidOperationException("Attempted to get valid flavor defs for meal type but there were none");
            }
            GetFlavorText(validFlavorDefsForMealType);
            if (Prefs.DevMode && !FinalFlavorDefs.NullOrEmpty()) Log.Warning($"Successfully got new Flavor Text for meal {parent.ThingID}");
        }
    
        catch (Exception ex)
        {
            string errorString = "";
            errorString += $"\n{DefDatabase<FlavorDef>.AllDefs.Count()} FlavorDefs are loaded";
            errorString += $"\n{flavorDefsToSearch?.Count} FlavorDefs were initially passed into TryGetFlavorDef to search within";
            errorString += $"\n{ValidFlavorDefsForMealType(parent.def).ToList().Count} FlavorDefs match the meal type";
            
            for (int i = 0; i < Ingredients.Count; i++)
            {
                ThingDef ingredient = Ingredients[i];
                errorString += $"\ningredient in slot {i} was {ingredient.defName}";
                errorString += $"\ningredient was in the following thingCategories";
                foreach (var cat in Ingredients[i].thingCategories)
                {
                    errorString += $"\n{cat.defName}";
                }
            }

            ex.Data["errorString"] = errorString;
            Log.Error($"Unable to find a matching FlavorDef for meal {parent.ThingID} at {parent.PositionHeld}. Please report. Error: {ex}\n{ex.Data["errorString"]}\n\n{ex.Data["stringInfo"]}\n\n");

        }
        /*finally
        {
            stopwatch.Stop();
            TimeSpan elapsed = stopwatch.Elapsed;
            if (Prefs.DevMode)
            {
                Log.Message("[Flavor Text] TryGetFlavorText ran in " + elapsed.ToString("ss\\.fffff") + " seconds");
            }
        }*/
    }
    
    //find the best flavorDefs for the parent meal and use them to generate flavor text label and description
    public void GetFlavorText(List<FlavorDef> flavorDefsToSearch)
    {
        // divide the ingredients into groups of size n and get a flavorDef for each group
        List<List<ThingDef>> ingredientChunks = Chunk(Ingredients);
        List<FlavorWithIndices> bestFlavors = ingredientChunks.Select(ingredientChunk => AcquireFlavor(ingredientChunk, flavorDefsToSearch)).ToList();

        // reset the flavor data
        FlavorLabels = [];
        FinalFlavorLabel = null;
        FlavorDescriptions = [];
        FinalFlavorDescription = null;
        FinalFlavorDefs = [];

        // assemble all the flavor labels chosen into one big label that looks nice
        for (int i = 0; i < bestFlavors.Count; i++)
        {

            // sort things by category, then sort meat in a specific order for grammar's sake
            if (bestFlavors[i] != null)
            {
                FinalFlavorDefs.Add(bestFlavors[i].FlavorDef);
                List<ThingDef> ingredientGroupSorted = RearrangeIngredients(bestFlavors[i], ingredientChunks[i]);
                string flavorLabel = FormatFlavorString(bestFlavors[i], ingredientGroupSorted, "label");  // make flavor labels look nicer for main label; replace placeholders in the flavor label with the corresponding ingredient in the meal
                if (flavorLabel.NullOrEmpty()) 
                { 
                    if (Prefs.DevMode) Log.Error($"FormatFlavorString failed to get a formatted flavor label for the ingredient group with index of {i}, cancelling the search. Please report.");
                    throw new FormatException();
                }
                FlavorLabels.Add(flavorLabel);

                string flavorDescription = FormatFlavorString(bestFlavors[i], ingredientGroupSorted, "description");  // make flavor labels look nicer for main description; replace placeholders in the flavor description with the corresponding ingredient in the meal
                FlavorDescriptions.Add(flavorDescription);

            }
            else
            {
                if (Prefs.DevMode) Log.Error($"A chosen FlavorDef with index of {i} is null, cancelling the search. Please report.");
                throw new NullReferenceException();
            }
        }
        CompileFlavorLabels();
        CompileFlavorDescriptions();

        if (FinalFlavorLabel.NullOrEmpty())
        {
            Log.Error("The final compiled and formatted flavor label was null or empty despite getting valid Flavor Defs.");
            throw new NullReferenceException("Flavor label was empty despite getting valid Flavor Defs");
        }
    }

    // split ingredients into chunks of size 3 (default)
    public List<List<T>> Chunk<T>(List<T> source)
    {
        return source
            .Select((x, i) => new { Index = i, Value = x })
            .GroupBy(x => x.Index / MaxNumIngredientsFlavor)
            .Select(x => x.Select(v => v.Value).ToList())
            .ToList();
    }

    private void CompileFlavorLabels()
    {
        // compile the flavor labels into one long displayed flavor label
        if (!FlavorLabels.NullOrEmpty())
        {
            StringBuilder stringBuilder = new();
            if (Tags.Contains("hairy"))
            {
                stringBuilder.Append("hairy ");
            }

            for (int j = 0; j < FlavorLabels.Count; j++)
            {
                var conj = j == 0 ? "" : j == 1 ? " with " : " and ";
                stringBuilder.Append(conj + GenText.CapitalizeAsTitle(FlavorLabels[j]));
            }
            FinalFlavorLabel = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
    }
    private void CompileFlavorDescriptions()
    {
        // compile the flavor labels into one long displayed flavor label
        try
        {

            if (!FlavorDescriptions.NullOrEmpty())
            {
                // switch to pseudorandom generation using ingredient list seed
                IEnumerable<string> ingredientDefNames = (from ing in Ingredients select ing.defName);
                string ingredientDefNamesJoined = string.Join(",", ingredientDefNames);
                int seed = ingredientDefNamesJoined.GetHashCode();
                Rand.PushState(seed);

                RulePackDef sideDishClauses = RulePackDef.Named("SideDishClauses");  // connector phrases for when meal has multiple FinalFlavorDefs

                StringBuilder stringBuilder = new();
                for (int j = 0; j < FlavorDescriptions.Count; j++)
                {
                    if (j == 0)  // if it's the first description, just use the description
                    {
                        var flavorDescription = CleanUpDescription(FlavorDescriptions[j]);
                        stringBuilder.Append(flavorDescription);
                    }
                    if (j > 0)  // if it's the 2nd+ description, in a new paragraph, use a side dish connector clause with the label, then the description
                    {
                        // connector clause with side dish label
                        GrammarRequest request = default;
                        if (sideDishClauses != null) { request.Includes.Add(sideDishClauses); }
                        string sideDishText = GrammarResolver.Resolve("sidedish", request);  // get a random connector sentence
                        sideDishText = string.Format(sideDishText, FlavorLabels[j], FlavorDescriptions[j]);  // place the current flavor label in its placeholder spot within the sentence
                        sideDishText = CleanUpDescription(sideDishText);
                        stringBuilder.AppendWithSeparator(sideDishText, "\n\n");
                    }
                }
                FinalFlavorDescription = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());

            }
        }
        catch (Exception e) 
        { 
            if (Prefs.DevMode) Log.Error($"Error compiling the final flavor description, reason: {e}");
            throw;
        }
        finally
        {
            // exit pseudorandom generation
            Rand.PopState();
        }
    }

    private static string CleanUpDescription(string flavorDescription)
    {
        if (!flavorDescription.NullOrEmpty())
        {
            flavorDescription = flavorDescription.Trim(',', ' ');
            /*            flavorDescription = flavorDescription.EndWithPeriod();*/
            flavorDescription = GenText.CapitalizeSentences(flavorDescription);
        }

        return flavorDescription;
    }


    // include flavor variables in game save files
    // on reload, check if flavorDefs are still valid and remake the flavor text from them; this helps with compatibility on mod updates
    public override void PostExposeData()
    {
        base.PostExposeData();

        Scribe_Values.Look(ref TriedFlavorText, "TriedFlavorText");
        Scribe_Defs.Look(ref CookingStation, "cookingStation");
        Scribe_Values.Look(ref HourOfDay, "hourOfDay");

        Scribe_Collections.Look(ref Tags, "tags");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && Tags == null)
        {
            Tags = [];
        }

        // load/save flavorDefs
        try
        {
            Scribe_Collections.Look(ref FinalFlavorDefs, "flavorDefs");
        }
        catch (Exception ex)
        {
            if (Prefs.DevMode) Log.Warning($"Invalid FlavorDef for meal {parent.ThingID} at {parent.PositionHeld}. Will attempt to get new Flavor Text. Error: {ex}");
        }

        // if flavorDefs is null, make it an empty list
        if (Scribe.mode == LoadSaveMode.PostLoadInit && FinalFlavorDefs == null)
        {
            FinalFlavorDefs = [];
        }

        // check if current flavorDefs are still valid, otherwise try and get completely new flavor text
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            TriedFlavorText = false;
            TryGetFlavorText(FinalFlavorDefs);
        }

    }

    // merge flavor variables: recalculate finalFlavorLabel and finalFlavorDescription
    public override void PreAbsorbStack(Thing otherStack, int count)  // otherStack is the item being added to the stack
    {
        try
        {
            var otherFlavorComp = otherStack.TryGetComp<CompFlavor>();
            FlavorLabels = [];
            FinalFlavorLabel = null;
            FlavorDescriptions = [];
            FinalFlavorDescription = null;
            FinalFlavorDefs = [];

            // for cookingStation and hourOfDay, choose randomly
            Rand.PushState(otherStack.thingIDNumber);
            CookingStation = Rand.Element(CookingStation, otherFlavorComp.CookingStation);
            HourOfDay = Rand.Element(HourOfDay, otherFlavorComp.HourOfDay);


            try
            {
                // merge flavor tags; if both meals have the tag, keep it, otherwise 10% for it to be deleted
                List<string> mergedTags = [.. Tags, .. otherFlavorComp.Tags];
                mergedTags.RemoveAll(tag => mergedTags.Count(t => t == tag) < 2 && Rand.Range(0, 10) == 0);
                Tags = mergedTags.Distinct().ToList();
                Rand.PopState();
            }
            catch (NullReferenceException)
            {
                if (Prefs.DevMode) Log.Error("Error merging meals: the tag list of one of the meals was null");
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Log.Error($"Error merging meals, error: {ex}");
            }

            foreach (var tag in otherFlavorComp.Tags) { Tags.AddDistinct(tag); }

            TriedFlavorText = false;
            TryGetFlavorText();

        }
        catch (Exception e) { if (Prefs.DevMode) Log.Error($"Failed to merge stacks properly, reason: {e}"); }
    }

    // split all stored flavor variables
    public override void PostSplitOff(Thing piece)
    {
        try
        {
            base.PostSplitOff(piece);
            if (piece != parent)
            {
                CompFlavor compFlavor = piece.TryGetComp<CompFlavor>();
                compFlavor.TriedFlavorText = TriedFlavorText;
                compFlavor.FinalFlavorDefs = FinalFlavorDefs;
                compFlavor.FlavorLabels = FlavorLabels;
                compFlavor.FlavorDescriptions = FlavorDescriptions;
                compFlavor.FinalFlavorLabel = FinalFlavorLabel;
                compFlavor.FinalFlavorDescription = FinalFlavorDescription;
                compFlavor.CookingStation = CookingStation;
                compFlavor.HourOfDay = HourOfDay;
                compFlavor.Tags = Tags;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to split stacks properly, reason: {e}");
        }
    }

    public class MeatComparer : IComparer<ThingDef>
    {
        public int Compare(ThingDef meat1, ThingDef meat2)
        {
            if (meat1 is { thingCategories: null } && meat2 is { thingCategories: null }) { return 0; }
            if (meat1 is { thingCategories: null }) { return -1; }
            if (meat2 is { thingCategories: null }) { return 1; }
            
            List<int> ranking = [meat1!.defName switch { "Meat_Twisted" => 0, "Meat_Human" => 3, "Meat_Megaspider" => 6, _ => 12, }, meat2!.defName switch { "Meat_Twisted" => 0, "Meat_Human" => 3, "Meat_Megaspider" => 6, _ => 12, }];
            int difference = ranking[1] - ranking[0];
            return difference;
        }
    }
}
