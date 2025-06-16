using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Verse;
using Verse.Grammar;
using static FlavorText.CompProperties_Flavor;
using System.Linq.Expressions;

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
//RELEASED: VCEF fish are going into FT_MeatRaw
//xxRELEASE: ThingCategoryDefsToAbsorb => SisterCategories (since it happens after failing to search)
//xxRELEASE: if failed to absorb ingredient, throw error but then add it via strings  //xx when would this ever occur??
//RELEASED: ceviche error
//DONE: load warning when FlavorDef MealCategories element is missing
//DONE: time and cooking station aren't working atm
//DONE: revise FlavorWithIndices system: you probably don't need a separate class for this
//DONE: check for cooking station and time in ValidFlavorDefs: this is a fast way to discard invalid FlavorDefs // OR check in CheckIfFlavorMatches before checking ingredients
//RELEASED: rearranging vanilla ingredients in spreadsheet messed up ingredient placeholders
//RELEASED: milk is staying as ^
//RELEASED: "egg" isn't appearing in labels
//RELEASED: VGEP: substring error out of range in CategoryUtility on startup
//RELEASED: add {food} {food, food} {food, food, food} to all mealKinds
//RELEASED: GAB pickled eggs is becoming "pickled eggs eggs"
//DONE: meal types of taglist for dry, wet, sweet, savory meals  // allows auto-labeling of soups vs dishes vs desserts, etc.
//DONE: null ingredient option: e.g. if an ingredient is optional  // but the name will probably change, so isn't a new FlavorDef better?

//TODO: a/an is/are grammar

//RELEASE: check all with v1.6
//RELEASE: update XML files
//RELEASE: check add to game
//RELEASE: check remove from game
//RELEASE: check new game
//RELEASE: check save and reload game
//RELEASE: check updating FlavorText on save
//RELEASE: check all meal types
//RELEASE: check food modlist
//RELEASE: check FTV
//RELEASE: check your own saves
//RELEASE: check CommonSense: starting spawned/drop-podded, drop pod meals, trader meals
//RELEASE: disable log messages
//RELEASE: 3 nuggets runs out of memory
//RELEASE: FTV is becoming generic again


//TODO: options to prevent merging meals
//TODO: variety matters warnings and errors?
//TODO: milk/cheese problem; in a mod with specialty cheeses, that name should be included, but otherwise milk should sometimes produce the word "cheese"


namespace FlavorText;

/// <summary>
///  CompFlavor is attached to each meal that should get a new Flavor Text label
///     stores FlavorDef data
///     makes and stores new flavor labels
///     makes and stores new flavor descriptions
/// </summary>

public class CompFlavor : ThingComp
{

    // ingredients in a pseudo-random order
    public List<ThingDef> Ingredients
    {
        get
        {
            List<ThingDef> ingredients = parent.TryGetComp<CompIngredients>().ingredients;
            //Log.Warning($"{ingredients.Count} ingredients found");
            List<ThingDef> ingredientsFoods = ingredients.FindAll(i => i != null && FlavorCategoryDefOf.FT_Foods.ContainedInThisOrDescendant(i));  // assemble a list of the ingredients that are actually food
            List<ThingDef> ingredientsSorted = [.. ingredientsFoods.OrderBy(def => def.defName.GetHashCode())];  // sort in a pseudo-random order

            //foreach (var ing in ingredientsSorted) { Log.Message($"found {ing.defName} in {parent.ThingID}"); }

            return ingredientsSorted;
        }
    }

    private bool tag; // debug tag
    
    public bool TriedFlavorText;
    
    public List<string> FlavorLabels = [];
    public string FinalFlavorLabel;  // final human-readable label for the meal

    public List<string> FlavorDescriptions = [];
    public string FinalFlavorDescription;  // final human-readable description for the meal

    public List<FlavorDef> FinalFlavorDefs = [];  // final chosen FinalFlavorDefs for the meal

    [CanBeNull] public ThingDef CookingStation;  // which station this meal was cooked on

    public int? HourOfDay;  // what hour of the day this meal was completed

    public float? IngredientsHitPointPercentage;  // average percentage of hit points of each ingredient group (ignoring quantity in group)

    public List<string> MealTags = [];

    /*    public bool fail = false;  // has an attempt been made to find a flavorLabel and failed? if so, don't ever try again*/

    // should FlavorText apply to this meal? Not everything with a CompFlavor gets FlavorText (yes this is messy, but it's the easiest way atm)
    public bool HasFlavorText => parent.HasComp<CompFlavor>();

    // ReSharper disable once UnusedMember.Global
    public CompProperties_Flavor Props => (CompProperties_Flavor)props;

    // if there's a flavor label made, transform the original meal label into it
    public override string TransformLabel(string label)
    {
        TryGetFlavorText();
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

    // include flavor variables in game save files
    // on reload, check if flavorDefs are still valid and remake the flavor text from them; this helps with compatibility on mod updates
    public override void PostExposeData()
    {
        base.PostExposeData();

        Scribe_Defs.Look(ref CookingStation, "cookingStation");
        Scribe_Values.Look(ref HourOfDay, "hourOfDay");
        Scribe_Values.Look(ref IngredientsHitPointPercentage, "ingredientsHitPointPercentage");

        Scribe_Collections.Look(ref MealTags, "tags");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && MealTags == null)
        {
            MealTags = [];
        }

        // load/save flavorDefs
        try
        {
            Scribe_Collections.Look(ref FinalFlavorDefs, "flavorDefs");
            
            // if FinalFlavorDefs has null values, make it an empty list
            if (Scribe.mode is LoadSaveMode.PostLoadInit)
            {
                if (FinalFlavorDefs is null || FinalFlavorDefs.Any(def => def is null || DefDatabase<FlavorDef>.GetNamedSilentFail(def.defName.ToString()) is null))
                {
                    FinalFlavorDefs = [];
                    if (Prefs.DevMode) Log.Warning($"Found a null or unknown FlavorDef in list of saved FlavorDefs, probably deprecated from an older version of FlavorText. Will get new FlavorDefs");
                }

            }
            
        }
        catch (Exception ex)
        {
            if (Prefs.DevMode) Log.Warning($"Found an invalid FlavorDef. Will attempt to get new Flavor Text. Error: {ex}");
        }

        // check if current flavorDefs are still valid, otherwise try and get completely new flavor text
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            TriedFlavorText = false;
            TryGetFlavorText(FinalFlavorDefs);
        }

    }


    // split all stored flavor variables
    public override void PostSplitOff(Thing piece)
    {
        try
        {
            base.PostSplitOff(piece);
            if (piece != parent)
            {
                CompFlavor otherCompFlavor = piece.TryGetComp<CompFlavor>();
                otherCompFlavor.TriedFlavorText = TriedFlavorText;
                otherCompFlavor.FinalFlavorDefs = FinalFlavorDefs;
                otherCompFlavor.FlavorLabels = FlavorLabels;
                otherCompFlavor.FlavorDescriptions = FlavorDescriptions;
                otherCompFlavor.FinalFlavorLabel = FinalFlavorLabel;
                otherCompFlavor.FinalFlavorDescription = FinalFlavorDescription;
                otherCompFlavor.CookingStation = CookingStation;
                otherCompFlavor.HourOfDay = HourOfDay;
                otherCompFlavor.MealTags = MealTags;
                otherCompFlavor.IngredientsHitPointPercentage = IngredientsHitPointPercentage;
            }
        }
        catch (Exception e)
        {
            Log.Error($"Failed to split stacks properly, reason: {e}");
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
                // merge flavor tags; if both meals have the tag, keep it, otherwise 10% chance for it to be deleted
                List<string> mergedTags = [.. MealTags, .. otherFlavorComp.MealTags];
                mergedTags.RemoveAll(mealTag => mergedTags.Count(t => t == mealTag) < 2 && Rand.Range(0, 10) == 0);
                MealTags = [.. mergedTags.Distinct()];
                foreach (var mealTag in otherFlavorComp.MealTags) { MealTags.AddDistinct(mealTag); }
                
            }
            catch (NullReferenceException)
            {
                if (Prefs.DevMode) Log.Error("Error merging meals: the tag list of one of the meals was null");
                
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode) Log.Error($"Error merging meals, error: {ex}");
            }
            finally
            {
                Rand.PopState();
            }

            // average ingredient hit points
            IngredientsHitPointPercentage = (IngredientsHitPointPercentage + otherFlavorComp.IngredientsHitPointPercentage) / 2;

            TriedFlavorText = false;
            TryGetFlavorText();
            //Log.Warning($"Successfully got FlavorText for {parent.ThingID}");

        }
        catch (Exception e) { if (Prefs.DevMode) Log.Error($"Failed to merge stacks properly, reason: {e}"); }
    }

    public void TryGetFlavorText(List<FlavorDef> flavorDefsToSearch = null)
    {
        if (TriedFlavorText) return;
        TriedFlavorText = true;

        /*        Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();*/
        try
        {
            // reset the flavor data
            FlavorLabels = [];
            FinalFlavorLabel = null;
            FlavorDescriptions = [];
            FinalFlavorDescription = null;
            FinalFlavorDefs = [];

            if (Ingredients.NullOrEmpty())
            {
                //if (Prefs.DevMode) Log.Message($"List of ingredients in CompIngredients was empty or null for {parent.ThingID} at {parent.PositionHeld}, cancelling the search. This is normal for meals without ingredients.");
                return;
            }

            if (!HasFlavorText)
            {
                throw new InvalidOperationException($"Parent {parent.def} does not have CompFlavor, cancelling the search. Please report.");
            }

            // fill in the extra parameters with pseudorandom data if they are null
            Rand.PushState(parent.thingIDNumber);
            Random r = new();
            HourOfDay ??= r.Next(0, 24);
            if (CookingStation is null)
            {
                var allCookingStations = FlavorCategoryDef.Named("FT_CookingStations").DescendantThingDefs.Distinct().ToList();
                CookingStation = allCookingStations[r.Next(allCookingStations.Count)];
            }
            IngredientsHitPointPercentage ??= Rand.Range(0, 1);
            Rand.PopState();

            GetFlavorText(flavorDefsToSearch);
        }

        catch (Exception ex)
        {
            string flavorSummary = $"Unable to find a matching FlavorDef for meal {parent.ThingID} at {parent.PositionHeld}. Please report.";
            flavorSummary += $"\n{FlavorDef.ActiveFlavorDefs.Count()} FlavorDefs are loaded";
            flavorSummary +=
                $"\n{flavorDefsToSearch?.Count} FlavorDefs were passed into TryGetFlavorDef from a saved game to search within";
            flavorSummary += $"\n{FlavorDef.ValidFlavorDefs(parent).ToList().Count} FlavorDefs match the meal type";

            for (int i = 0; i < Ingredients.Count; i++)
            {
                ThingDef ingredient = Ingredients[i];
                flavorSummary += $"\ningredient {i} was {ingredient.defName}";
                flavorSummary += $"\ningredient was in the following Flavor Categories";
                foreach (var cat in CategoryUtility.ThingCategories[Ingredients[i]])
                {
                    flavorSummary += $"\n{cat.defName}";
                }
            }

            ex.Data.Add("allIngredients", flavorSummary);
            if (Prefs.DevMode) Log.Error($"Error: {ex}\n{ex.Data["flavorSummary"]}\n{ex.Data["flavorDef"]}\n{ex.Data["ingredients"]}");
            return;
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
    private void GetFlavorText(List<FlavorDef> flavorDefsToSearch)
    {
        // divide the ingredients into groups of size n and get a flavorDef for each group
        // for each group, move all meat to the front and arrange it in an order that will be more grammatically pleasing
        List<List<ThingDef>> ingredientChunks = [..Chunk(Ingredients).Select(chunk => chunk.OrderByDescending(m => m, new MeatComparer()).ToList())];

        List<(FlavorDef, List<int>)> bestFlavors = [];
        // try searching in any saved FlavorDefs that you were given
        if (!flavorDefsToSearch.NullOrEmpty())
        {
            try
            {
                flavorDefsToSearch = [.. FlavorDef.ValidFlavorDefs(parent, flavorDefsToSearch)];
                if (!flavorDefsToSearch.Empty()) bestFlavors = [.. ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk, flavorDefsToSearch))];
            }
            catch (Exception ex) when (ex is NullReferenceException || ex is InvalidOperationException)
            {
                if (Prefs.DevMode) Log.Warning($"Saved Flavor Text no longer matches for a meal, it is probably from an older version of FlavorText. Will attempt to get new Flavor Text.");
                bestFlavors = [];
            }
        }
        // if the above failed, try searching with all valid FlavorDefs
        if (bestFlavors.Empty())
        {
            var validFlavorDefsForMealType = FlavorDef.ValidFlavorDefs(parent).ToList();
            if (validFlavorDefsForMealType.NullOrEmpty())
            {
                throw new InvalidOperationException($"Attempted to get list of all valid Flavor Defs for meal type '{parent.def.defName}' but there were none. Please report.");
            }
            bestFlavors = [.. ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk, validFlavorDefsForMealType))];
            if (bestFlavors.Empty()) throw new InvalidOperationException($"Could not find any best Flavor Defs for meal {parent.ThingID}");
        }


        // assemble all the flavor labels chosen into one big label that looks nice
        for (int i = 0; i < bestFlavors.Count; i++)
        {
            if (bestFlavors?[i].Item1 != null && bestFlavors[i].Item2 != null)
            {
                FinalFlavorDefs.Add(bestFlavors[i].Item1);
                (FlavorDef, List<int>) flavor = bestFlavors[i];
                List<ThingDef> ingredientGroup = ingredientChunks[i];
                
                // sort the given ingredients by indices (ascending)
                var group = ingredientGroup;
                ingredientGroup = [.. ingredientGroup.OrderBy(ing => flavor.Item2[group.IndexOf(ing)])];  // sort ingredients by indices
                flavor.Item2.Sort();
                List<ThingDef> ingredientGroupSorted = ingredientGroup;
                
                string flavorLabel = FormatFlavorString(bestFlavors[i].Item1, ingredientGroupSorted, "label");  // make flavor labels look nicer for main label; replace placeholders in the flavor label with the corresponding ingredient from the meal
                if (flavorLabel.NullOrEmpty()) 
                { 
                    if (Prefs.DevMode) Log.Error($"FormatFlavorString failed to get a formatted flavor label for ingredient group {i} containing [{ingredientGroupSorted.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                    throw new FormatException();
                }
                FlavorLabels.Add(flavorLabel);

                string flavorDescription = FormatFlavorString(bestFlavors[i].Item1, ingredientGroupSorted, "description");  // make flavor descriptions look nicer for main description; replace placeholders in the flavor description with the corresponding ingredient from the meal
                if (flavorDescription.NullOrEmpty())
                {
                    if (Prefs.DevMode) Log.Error($"FormatFlavorString failed to get a formatted flavor description for ingredient group {i} containing [{ingredientGroupSorted.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                    throw new FormatException();
                }
                FlavorDescriptions.Add(flavorDescription);

            }
            else
            {
                throw new NullReferenceException($"A chosen FlavorDef with index of {i} is null, cancelling the search. Please report.");
            }
        }
        if (FlavorLabels.Empty()) throw new InvalidOperationException($"The list of Flavor Labels for meal {parent.ThingID} was empty. Please report.");
        if (FlavorDescriptions.Empty()) throw new InvalidOperationException($"The list of Flavor Descriptions for meal {parent.ThingID} was empty. Please report.");
        CompileFlavorLabels();
        CompileFlavorDescriptions();

        if (FinalFlavorLabel.NullOrEmpty())
        {
            throw new NullReferenceException($"The final compiled and formatted flavor label was null or empty despite getting valid Flavor Defs [{FinalFlavorDefs.ToStringSafeEnumerable()}]. Please report.");
        }
    }


    // split ingredients into chunks of size 3 (default)
    private static List<List<T>> Chunk<T>(List<T> source)
    {
        return [.. source
            .Select((x, i) => new { Index = i, Value = x })
            .GroupBy(x => x.Index / MaxNumIngredientsFlavor)
            .Select(x => x.Select(v => v.Value).ToList())];
    }


    // see which FinalFlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    private static (FlavorDef, List<int>) GetBestFlavorDef(List<ThingDef> foodsToSearchFor, List<FlavorDef> flavorDefsToSearch)
    {
        try
        {
            if (foodsToSearchFor.NullOrEmpty())
            {
                throw new ArgumentNullException(nameof(foodsToSearchFor), "List of ingredients to search for is null or empty");
            }
            if (flavorDefsToSearch.NullOrEmpty())
            {
                throw new ArgumentNullException(nameof(flavorDefsToSearch), "List of Flavor Defs to search is null or empty");
            }

            //see which FinalFlavorDefs match with the ingredients in the meal
            List<(FlavorDef, List<int>)> matchingFlavors = [];
            foreach (FlavorDef flavorDef in flavorDefsToSearch)
            {
                var matchedIndices = GetMatchIndices(foodsToSearchFor, flavorDef);
                if (!matchedIndices.NullOrEmpty())
                {
                    matchingFlavors.Add((flavorDef, matchedIndices));
                    //Log.Message($"found matching FlavorDef {flavorDef.defName} with ingredients ({foodsToSearchFor.ToStringSafeEnumerable()}) and indices ({matchedIndices.ToStringSafeEnumerable()})");
                }

            }

            // pick the most specific matching FlavorDef
            if (matchingFlavors.Count > 0)
            {
                matchingFlavors = [.. matchingFlavors.OrderBy(entry => entry.Item1.specificity)];
                //foreach (var flavorDef in matchingFlavors) { Log.Message(flavorDef.Item1.defName + " = " + flavorDef.Item1.Specificity); }
                var flavor = matchingFlavors.First();
                if (flavor.Item1 is null || flavor.Item2 is null) throw new NullReferenceException($"Failed to find a matching Flavor Def. The best Flavor Def [{flavor.Item1}] or its list of indices [{flavor.Item2.ToStringSafeEnumerable()}] was null.");
                return flavor;
            }
            else throw new InvalidOperationException($"Failed to find a matching Flavor Def. There were no matching Flavor Defs found.");

        }
        catch (Exception ex)
        {
            string errorString = "\ningredients were:";
            for (int i = 0; i < foodsToSearchFor.Count; i++)
            {
                ThingDef ingredient = foodsToSearchFor[i];
                errorString += $"\n{i} {ingredient.defName}";
            }
            ex.Data.Add("ingredients", errorString);
            throw;
        }
    }

    private static List<int> GetMatchIndices(List<ThingDef> foods, FlavorDef flavorDef)  // check if all the ingredients match the given FlavorDef
    {
        try
        {
            // if flavorDef is null, skip
            if (flavorDef == null)
            {
                if (Prefs.DevMode) Log.Warning($"Found a null FlavorDef in list of FinalFlavorDefs to search for a meal. Probably deprecated from an older version of FlavorText. Skipping...");
                return null;
            }

            // if flavorDef length doesn't match ingredient list length, skip
            if (foods.Count != flavorDef.ingredients.Count)
            {
                return null;
            }

            // if ingredients aren't wholly contained within the FinalFlavorDefs lowest common category of ingredients, skip
            if (!foods.All(flavorDef.lowestCommonIngredientCategory.ContainedInThisOrDescendant))
            {
                return null;
            }

            //Log.Warning($"----------{flavorDef}----------");
            List<int> matchedIndices = new(foods.Count); // [Corn, Berries, Egg] [-1, -1, -1] (FT_Foods, FT_Egg, FT_Grain)
            foreach (var food in foods)
            {
                try
                {
                    //Log.Warning($"examining food {food} with index {foods.IndexOf(food)}");
                    var bestIngredients = flavorDef.ingredients
                        .Select((value, index) => (value, index))
                        .Where(ing => !matchedIndices.Contains(ing.index)
                                      && ing.value.AllowedThingDefs.Contains(food))
                        .OrderBy(ing => ing.value.AllowedThingDefs.Count()).ToList();
                    //Log.Message($"best ingredients: { bestIngredients.ToStringSafeEnumerable() }");

                    if (bestIngredients.NullOrEmpty()) return null;
                    matchedIndices.Add(bestIngredients.First().index);  //  [2, 0, -1]
                    //Log.Message($"matched indices: [{matchedIndices.ToStringSafeEnumerable()}]");
                }
                catch (Exception)
                {
                    Log.Error($"{food?.ToStringSafe()} in {flavorDef?.ToStringSafe()} had an error.");
                    throw;
                }
            }

            return matchedIndices.Count == foods.Count ? matchedIndices : null;
        }
        catch (Exception ex)
        {
            ex.Data.Add("flavorDef", $"{flavorDef?.defName} was the FlavorDef that caused the error");
            throw;
        }
    }


    private static string FormatFlavorString(FlavorDef flavorDef, List<ThingDef> ingredients, string flag)  // replace placehodlers in flavor label/description with the correctly inflected ingredient label
    {
        try
        {
            string flavorString;
            switch (flag)
            {
                // get label or description depending on "flag"
                case "label":
                {
                    flavorString = flavorDef.label;
                    break;
                }
                case "description":
                {
                    flavorString = flavorDef.description;
                    break;
                }
                default:
                {
                    throw new ArgumentException($"Tried to format a flavor string with an invalid field flag. Flag: {flag}");
                }
            }

            // find placeholders and replace them with the appropriate inflection of the right ingredient
            for (int i = 0; i < ingredients.Count; i++)
            {
                var inflections = InflectionUtility.ThingInflectionsDictionary[ingredients[i]];
                while (true)
                {
                    var placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_plur\\} *([^ ]*)");  //capture the placeholder and the word before and after it
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[0], placeholder);
                        flavorString = Regex.Replace(flavorString, "\\{" + i + "_plur\\}", inflection);
                        continue;
                    }
                    placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_coll\\} *([^ ]*)");
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[1], placeholder);
                        flavorString = Regex.Replace(flavorString, "\\{" + i + "_coll\\}", inflection);
                        continue;
                    }
                    placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_sing\\} *([^ ]*)");
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[2], placeholder);
                        flavorString = Regex.Replace(flavorString, "\\{" + i + "_sing\\}", inflection);
                        continue;
                    }

                    placeholder = Regex.Match(flavorString, "([^ ]*) *\\{" + i + "_adj\\} *([^ ]*)");
                    if (placeholder.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[3], placeholder);
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
                if (inflection == "") return inflection;  // return if a blank inflection, currently only used for the adjectival form of "flour"
                List<string> inflectionSplit = [.. inflection.Split(' ')];
                if (placeholder.Groups.Count != 3) throw new ArgumentOutOfRangeException($"The number of capture groups from Regex.Match for {inflection} was not 3.");

                // if you captured a word before the placeholder, see if it duplicates the first word of "inflection"
                if (Remove.RemoveDiacritics(placeholder.Groups[1].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit.First()).ToLower())
                {
                    inflectionSplit.RemoveAt(0);
                }

                // if you captured a word after the placeholder, see if it duplicates the last word of "inflection"
                if (Remove.RemoveDiacritics(placeholder.Groups[2].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit.Last()).ToLower())
                {
                    inflectionSplit.RemoveLast();
                }
                

                inflection = string.Join(" ", inflectionSplit);
                return inflection;
            }
        }
        catch (Exception e) { throw new Exception($"Error when formatting flavor {flag} for {flavorDef.ToStringSafe()} with ingredients [{ingredients.ToStringSafeEnumerable()}]: reason: {e}"); }
    }

    private void CompileFlavorLabels()
    {
        // compile the flavor labels into one long displayed flavor label
        if (!FlavorLabels.NullOrEmpty())
        {
            StringBuilder stringBuilder = new();
            // don't ask
            if (MealTags.Contains("hairy"))
            {
                GrammarRequest request = default;
                request.Includes.Add(RulePackDef.Named("FT_Tags"));
                var hairy = GrammarResolver.Resolve("hairy", request);
                stringBuilder.Append(hairy);
            }

            for (int j = 0; j < FlavorLabels.Count; j++)
            {
                var conj = j == 0 ? "" : j == 1 ? "with " : "and ";
                stringBuilder.AppendWithSeparator(conj + GenText.CapitalizeAsTitle(FlavorLabels[j]), " ");
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

                RulePackDef sideDishClauses = RulePackDef.Named("FT_SideDishClauses");  // connector phrases for when meal has multiple FinalFlavorDefs

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
                        request.Includes.Add(sideDishClauses);
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
            Rand.PopState();
        }
    }

    private static string CleanUpDescription(string flavorDescription)
    {
        if (!flavorDescription.NullOrEmpty())
        {
            flavorDescription = flavorDescription.Trim(',', ' ');
            flavorDescription = GenText.CapitalizeSentences(flavorDescription);
        }

        return flavorDescription;
    }


    public class MeatComparer : IComparer<ThingDef>
    {
        public int Compare(ThingDef ing1, ThingDef ing2)
        {
            if (ing1 is { thingCategories: null } && ing2 is { thingCategories: null }) { return 0; }
            if (ing1 is { thingCategories: null }) { return -1; }
            if (ing2 is { thingCategories: null }) { return 1; }

            List<int> ranking = [ing1 switch
            {
                not null when CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Twisted")) => 0,
                not null when CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Human")) => 3,
                not null when CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Insect")) => 6,
                not null when CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_MeatRaw")) => 9,
                _ => 12
            }, ing2 switch
            {
                not null when CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Twisted")) => 0,
                not null when CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Human")) => 3,
                not null when CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Insect")) => 6,
                not null when CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_MeatRaw")) => 9,
                _ => 12
            }];

            int difference = ranking[1] - ranking[0];
            return difference;
        }
    }
}
