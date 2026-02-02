
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using Verse.Grammar;

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
//RELEASE: 3 nuggets runs out of memory xx// not b/c of FlavorText
//RELEASED: FTV is becoming generic again
//RELEASED: haute error -xx not FlavorText
//DONE: a/an grammar
//DONE: ghost ingredients can add "simple meal" etc as an ingredient
//DONE: ^ appearing sometimes in recipes
//DONE: berries adj = berries
//DONE: ghost ingredient should match vegetarian/carnivore
//TODO: merge stack error on dev quicktest b/c game starts paused
//--TODO: options to prevent merging meals
//DONE: name generation should be based on previous meal made in this save, to make it more consistent
//DONE: VCE_Soup names are re-rolled after cooking
//DONE: soylent green isn't generating

//RELEASED: check all with v1.6
//RELEASED: update XML files
//RELEASED: check remove from game
//RELEASED: check add to game
//RELEASED: check new game
//RELEASED: check updating FlavorText on save
//RELEASED: check save and reload game
//RELEASED: check all meal types
//RELEASED: check food modlist
//RELEASED: check FTV
//RELEASED: check your own saves
//RELEASED: check CommonSense: starting spawned/drop-podded, drop pod meals, trader meals
//RELEASED: disable log messages
//RELEASED: test C# meats
//RELEASED: test medieval overhaul


//TODO: variety matters warnings and errors?
//TODO: milk/cheese problem; in a mod with specialty cheeses, that name should be included, but otherwise milk should sometimes produce the word "cheese"
//TODO: WhatsThatMod loses color in its tag
//TODO: [Soy/Chicken, PlantFoodRaw] fails when searching [soy, chicken]
//TODO: test iterations carryover for merge/split/save
//TODO: check how disallowed slot categories are handled
//TODO: sidedishclauses for single flavordef descriptions
//TODO: common sense spawned bread is becoming sourdough
//TODO: TryGetFlavorText is running each time a meal is dropped on the ground
//TODO: ensure that carnivore meals always generate with at least 1 meat ingredient
//TODO: vegan meals are generating with any FlavorDefs
//TODO: update from backup

/// <summary>
///  CompFlavor contains the primary code execution
///     one is attached to each meal that should get a new Flavor Text label
///     stores FlavorDef data
///     makes and stores new flavor labels
///     makes and stores new flavor descriptions
/// </summary>

namespace FlavorText;

public class CompFlavor : ThingComp
{

    private readonly bool tag;

    private bool generatedCoreFlavorDef = true;

    public bool TriedFlavorText;

    private List<string> FlavorLabels = [];

    private string FinalFlavorLabel;

    private List<string> FlavorDescriptions = [];

    private string FinalFlavorDescription;

    private List<FlavorDef> FinalFlavorDefs = [];

    public ThingDef CookingStation;

    public int? HourOfDay = null;

    public int? TickCreated = null;

    public int? CookID = null;

    public float? IngredientsHitPointPercentage;

    public List<string> MealTags = [];

    internal DietTuple mealDietKind;

    internal List<FlavorCategoryDef> excludedCategories;

    internal int? iteration = null;

    public List<ThingDef> Ingredients => [.. from def in base.parent.TryGetComp<CompIngredients>().ingredients.FindAll(i => i != null && FlavorCategoryDefOf.FT_Foods.ContainedInThisOrDescendant(i))
                                          orderby def.defName.GetHashCode()
                                          select def];

    public CompIngredients CompIngredients => parent.TryGetComp<CompIngredients>();

    public CompProperties_Flavor Props => (CompProperties_Flavor)base.props;

    public override string TransformLabel(string label)
    {
        TryGetFlavorText();
        return base.parent.stackCount == 1 || FlavorTextSettings.flavorTextForStacks
            ? (!FinalFlavorLabel.NullOrEmpty()) ? (FinalFlavorLabel + " (" + base.TransformLabel(label) + ")") : base.TransformLabel(label)
            : base.TransformLabel(label);
    }

    public override string GetDescriptionPart()
    {
        return (!FinalFlavorDescription.NullOrEmpty()) ? FinalFlavorDescription : base.GetDescriptionPart();
    }

    public override string CompInspectStringExtra()
    {
        if (!FinalFlavorLabel.NullOrEmpty())
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine((parent.stackCount != 1 && !FlavorTextSettings.flavorTextForStacks) ? FinalFlavorLabel : base.TransformLabel(base.parent.def.label));
            return stringBuilder.ToString().TrimEndNewlines();
        }
        return base.CompInspectStringExtra();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Defs.Look(ref CookingStation, "cookingStation");
        Scribe_Values.Look(ref HourOfDay, "hourOfDay");
        Scribe_Values.Look(ref TickCreated, "tickCreated");
        Scribe_Values.Look(ref iteration, "iteration");
        Scribe_Collections.Look(ref MealTags, "tags", LookMode.Undefined);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && MealTags == null)
        {
            MealTags = [];
        }
        try
        {
            Scribe_Collections.Look(ref FinalFlavorDefs, "flavorDefs", LookMode.Undefined);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (FinalFlavorDefs == null)
                {
                    FinalFlavorDefs = [];
                }
                else if (FinalFlavorDefs.Any(def => def == null || DefDatabase<FlavorDef>.GetNamedSilentFail(def.defName.ToString()) == null))
                {
                    FinalFlavorDefs = [];
                    if (Prefs.DevMode)
                    {
                        Log.Warning("Found a null or unknown FlavorDef in list of saved FlavorDefs, probably deprecated from an older version of FlavorText. Will get new FlavorDefs");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (Prefs.DevMode)
            {
                Log.Warning($"Found an invalid FlavorDef. Will attempt to get new Flavor Text. Error: {ex}");
            }
        }
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            TriedFlavorText = false;
            TryGetFlavorText(FinalFlavorDefs);
        }
    }

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
                otherCompFlavor.TickCreated = TickCreated;
                otherCompFlavor.MealTags = MealTags;
                otherCompFlavor.iteration = iteration;
            }
        }
        catch (Exception arg)
        {
            Log.Error($"Failed to split stacks properly, reason: {arg}");
        }
    }

    public override void PreAbsorbStack(Thing otherStack, int count)
    {
        try
        {
            base.PreAbsorbStack(otherStack, count);
            if (!TriedFlavorText)
            {
                TryGetFlavorText();
            }
            CompFlavor otherFlavorComp = otherStack.TryGetComp<CompFlavor>();
            FlavorLabels = [];
            FinalFlavorLabel = null;
            FlavorDescriptions = [];
            FinalFlavorDescription = null;
            FinalFlavorDefs = [];
            int valueOrDefault = TickCreated.GetValueOrDefault();
            if (!TickCreated.HasValue)
            {
                TickCreated = GenTicks.TicksAbs;
            }
            Rand.PushState(Find.World.info.Seed + iteration.Value);
            CookingStation = Rand.Element(CookingStation, otherFlavorComp.CookingStation);
            HourOfDay = Rand.Element(HourOfDay, otherFlavorComp.HourOfDay);
            TickCreated = Rand.Element(TickCreated, otherFlavorComp.TickCreated);
            iteration = Rand.Element(iteration, otherFlavorComp.iteration);
            try
            {
                List<string> mealTags = MealTags;
                List<string> mealTags2 = otherFlavorComp.MealTags;
                List<string> list = [.. mealTags, .. mealTags2];
                List<string> mergedTags = list;
                mergedTags.RemoveAll((mealTag) => mergedTags.Count(t => t == mealTag) < 2 && Rand.Range(0, 10) == 0);
                MealTags = [.. mergedTags.Distinct()];
                using List<string>.Enumerator enumerator = otherFlavorComp.MealTags.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    GenCollection.AddDistinct(obj: enumerator.Current, list: MealTags);
                }
            }
            catch (NullReferenceException)
            {
                if (Prefs.DevMode)
                {
                    Log.Error("Error merging meals: the tag list of one of the meals was null");
                }
            }
            catch (Exception ex)
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"Error merging tag lists of meals, error: {ex}");
                }
            }
            finally
            {
                Rand.PopState();
            }
            TriedFlavorText = false;
            TryGetFlavorText();
        }
        catch (Exception e)
        {
            if (Prefs.DevMode)
            {
                Log.Error($"Failed to merge stacks properly, reason: {e}");
            }
        }
    }

    public void TryGetFlavorText(List<FlavorDef> flavorDefsToSearch = null)
    {
        if (TriedFlavorText)
        {
            return;
        }
        if (!iteration.HasValue)
        {
            CompFlavorUtility.Iterate();
            iteration = CompFlavorUtility.Iterations;
            //Log.Message($"it = {CompFlavorUtility.Iterations}");
        }
        TriedFlavorText = true;
        Stopwatch stopwatch = new();
        stopwatch.Start();
        try
        {
            // reset the flavor data
            FlavorLabels = [];
            FinalFlavorLabel = null;
            FlavorDescriptions = [];
            FinalFlavorDescription = null;
            FinalFlavorDefs = [];
            if (Ingredients != null && (!Ingredients.Empty() || FlavorTextSettings.numAllowedMissingIngredients != 0))
            {
                // fill in the extra parameters with pseudorandom data if they are null
                int valueOrDefault = TickCreated.GetValueOrDefault();
                if (!TickCreated.HasValue)
                {
                    TickCreated = GenTicks.TicksAbs;
                }
                Rand.PushState(Find.World.info.Seed + iteration.Value);
                Random r = new();
                valueOrDefault = HourOfDay.GetValueOrDefault();
                if (!HourOfDay.HasValue)
                {
                    HourOfDay = r.Next(0, 24);
                }
                if (CookingStation == null)
                {
                    List<ThingDef> allCookingStations = [.. FlavorCategoryDef.Named("FT_CookingStations").DescendantThingDefs.Distinct()];
                    CookingStation = allCookingStations[r.Next(allCookingStations.Count)];
                }
                //IngredientsHitPointPercentage ??= Rand.Range(0f, 1f);
                Rand.PopState();
                GetFlavorText(flavorDefsToSearch);
            }
        }
        catch (Exception ex)
        {
            string flavorSummary = string.Concat(string.Concat($"Unable to find a matching FlavorDef for meal {parent.ThingID} at {parent.PositionHeld}. Please report." + $"\n{FlavorDef.ActiveFlavorDefs.Count()} FlavorDefs are loaded", $"\n{flavorDefsToSearch?.Count} FlavorDefs were passed into TryGetFlavorDef from a saved game to search within"), $"\n{FlavorDef.ValidFlavorDefs(parent).ToList().Count} FlavorDefs match the meal type");
            for (int i = 0; i < Ingredients.Count; i++)
            {
                flavorSummary = string.Concat(flavorSummary + string.Format(arg1: Ingredients[i].defName, format: "\ningredient {0} was {1}", arg0: i), "\ningredient was in the following Flavor Categories");
                foreach (FlavorCategoryDef item in CategoryUtility.ThingCategories[Ingredients[i]])
                {
                    flavorSummary = flavorSummary + "\n" + item.defName;
                }
            }
            ex.Data.Add("allIngredients", flavorSummary);
            if (Prefs.DevMode)
            {
                Log.Error(string.Format("Error: {0}\n{1}\n{2}\n{3}", ex, ex.Data["flavorSummary"], ex.Data["flavorDef"], ex.Data["ingredients"]));
            }
        }
        finally
        {
            stopwatch.Stop();
            double elapsed = stopwatch.Elapsed.TotalMilliseconds;
            if (Prefs.DevMode)
            {
                Log.Message("[Flavor Text] TryGetFlavorText ran in " + elapsed + " milliseconds");
            }
        }
    }

    //find the best flavorDefs for the parent meal and use them to generate flavor text label and description
    private void GetFlavorText(List<FlavorDef> flavorDefsToSearch)
    {
        // if ghost ingredients are being used, set restrictions based on the FoodKind of the meal (e.g. no ghost meat in veggie meals)
        if (FlavorTextSettings.numAllowedMissingIngredients > 0)
        {
            excludedCategories = [.. Props.defaultGhostExcludedCategories];
            CompIngredients compIngredients = parent.TryGetComp<CompIngredients>();
            try
            {
                if (FoodUtility.GetFoodKind(parent) == FoodKind.Meat)
                {
                    mealDietKind = compIngredients.Props.noIngredientsFoodKind == FoodKind.Meat ? DietKind.carnivore : DietKind.omnivore;
                }
                else if (FoodUtility.GetFoodKind(parent) == FoodKind.NonMeat)
                {
                    mealDietKind = DietKind.vegan;
                }
                else
                {
                    if (FoodUtility.GetFoodKind(parent) != FoodKind.Any)
                    {
                        throw new ArgumentException("Error when getting food kind for meal. FoodKind: " + FoodUtility.GetFoodKind(parent).ToStringSafe());
                    }
                    mealDietKind = DietKind.vegetarian;
                }
                using (IEnumerator<FlavorCategoryDef> enumerator = DietKind.GetFlavorCategoriesFromDiet(mealDietKind).GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        GenCollection.AddDistinct(obj: enumerator.Current, list: excludedCategories);
                    }
                }
                generatedCoreFlavorDef = Ingredients.Count() > 0;
            }
            catch (Exception)
            {
                throw;
            }
        }

        // divide the ingredients into groups of size n and get a flavorDef for each group
        // within each group, move all meat to the front and arrange it in an order that will be more grammatically pleasing
        List<List<ThingDef>> ingredientChunks =
        [
            []
        ];
        if (Ingredients.Count() > 0)
        {
            ingredientChunks = [.. from chunk in Chunk(Ingredients)
                                select chunk.OrderByDescending(m => m, new MeatComparer()).ToList()];
        }
        List<(FlavorDef def, List<int> index)> bestFlavors = [];
        // try searching in any saved FlavorDefs that you were given
        if (!flavorDefsToSearch.NullOrEmpty())
        {
            try
            {
                flavorDefsToSearch = [.. FlavorDef.ValidFlavorDefs(parent, flavorDefsToSearch)];
                if (!flavorDefsToSearch.Empty())
                {
                    bestFlavors = (!ingredientChunks.Empty()) ? ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk, flavorDefsToSearch)).ToList() : [GetBestFlavorDef([], flavorDefsToSearch)];
                }
            }
            catch (Exception ex2) when (ex2 is NullReferenceException or InvalidOperationException)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning("Saved Flavor Text no longer matches for a meal, it is probably from an older version of FlavorText. Will attempt to get new Flavor Text.");
                }
                bestFlavors = [];
            }
        }
        // if the above failed, try searching with all valid FlavorDefs
        if (bestFlavors.Empty())
        {
            List<FlavorDef> validFlavorDefsForMealType = [.. FlavorDef.ValidFlavorDefs(parent)];
            if (validFlavorDefsForMealType.NullOrEmpty())
            {
                throw new InvalidOperationException("Attempted to get list of all valid Flavor Defs for meal type '" + parent.def.defName + "' in [" + CategoryUtility.ThingCategories.TryGetValue(parent.def).ToStringSafeEnumerable() + "] but there were none. Please report.");
            }
            bestFlavors = (!ingredientChunks.Empty()) ? ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk, validFlavorDefsForMealType)).ToList() : [GetBestFlavorDef([], validFlavorDefsForMealType)];
            if (bestFlavors.Empty())
            {
                throw new InvalidOperationException("Could not find any best Flavor Defs for meal " + parent.ThingID);
            }
        }
        // assemble all the flavor labels chosen into one big label that looks nice
        for (int i = 0; i < bestFlavors.Count; i++)
        {
            if (bestFlavors?[i].def != null && bestFlavors[i].index != null)
            {
                FinalFlavorDefs.Add(bestFlavors[i].def);
                (FlavorDef, List<int>) flavor = bestFlavors[i];
                List<ThingDef> ingredientGroup = ingredientChunks[i];
                string flavorLabel = FormatFlavorString(bestFlavors[i], ingredientGroup, bestFlavors[i].def.label); // make flavor labels look nicer for main label; replace placeholders in the flavor label with the corresponding ingredient from the meal
                if (flavorLabel.NullOrEmpty())
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"FormatFlavorString failed to get a formatted flavor label for ingredient group {i} containing [{ingredientGroup.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                    }
                    throw new FormatException();
                }
                FlavorLabels.Add(flavorLabel);
                string flavorDescription = FormatFlavorString(bestFlavors[i], ingredientGroup, bestFlavors[i].def.description);  // make flavor descriptions look nicer for main description; replace placeholders in the flavor description with the corresponding ingredient from the meal
                if (flavorDescription.NullOrEmpty())
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"FormatFlavorString failed to get a formatted flavor description for ingredient group {i} containing [{ingredientGroup.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                    }
                    throw new FormatException();
                }
                FlavorDescriptions.Add(flavorDescription);
                continue;
            }
            throw new NullReferenceException($"A chosen FlavorDef with index of {i} is null, cancelling the search. Please report.");
        }
        if (FlavorLabels.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Labels for meal " + base.parent.ThingID + " was empty. Please report.");
        }
        if (FlavorDescriptions.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Descriptions for meal " + base.parent.ThingID + " was empty. Please report.");
        }
        CompileFlavorLabels();
        CompileFlavorDescriptions();
        if (FinalFlavorLabel.NullOrEmpty())
        {
            throw new NullReferenceException("The final compiled and formatted flavor label was null or empty despite getting valid Flavor Defs [" + FinalFlavorDefs.ToStringSafeEnumerable() + "]. Please report.");
        }
    }

    // split ingredients into chunks of size 3 (default)
    private static List<List<T>> Chunk<T>(List<T> source)
    {
        return (from x in source.Select((T x, int i) => new
        {
            Index = i,
            Value = x
        })
                group x by x.Index / 3 into x
                select x.Select(v => v.Value).ToList()).ToList();
    }

    // see which FinalFlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    private (FlavorDef, List<int>) GetBestFlavorDef(List<ThingDef> ingredients, List<FlavorDef> flavorDefsToSearch)
    {
        try
        {
            if (ingredients == null || (ingredients.Empty() && FlavorTextSettings.numAllowedMissingIngredients == 0))
            {
                throw new ArgumentNullException("ingredients", "List of ingredients to search for is null or empty");
            }
            if (flavorDefsToSearch.NullOrEmpty())
            {
                throw new ArgumentNullException("flavorDefsToSearch", "List of Flavor Defs to search is null or empty");
            }
            //see which FinalFlavorDefs match with the ingredients in the meal
            List<(FlavorDef def, List<int> indices)> matchingFlavors = [];
            foreach (FlavorDef flavorDef in flavorDefsToSearch)
            {
                List<int> matchedIndices = GetMatchIndices(ingredients, flavorDef);
                if (!matchedIndices.NullOrEmpty())
                {
                    matchingFlavors.Add((flavorDef, matchedIndices));
                }
            }
            // pick the most specific matching FlavorDef
            // note that the slots may be reordered from the XML, however the flavor strings are not, hence the need for FlavorDef.slotIndices
            if (matchingFlavors.Count > 0)
            {
                matchingFlavors = [.. matchingFlavors.OrderByDescending(entry => entry.def.specificity)];
                //foreach (var (def, indices) in matchingFlavors) { Log.Message(def.defName + " = " + def.specificity); }
                (FlavorDef def, List<int> indices) bestFlavor;
                if (FlavorTextSettings.randomizedRecipeOuput)
                {
                    Rand.PushState(Find.World.info.Seed + iteration.Value);
                    bestFlavor = matchingFlavors.RandomElementByWeight(((FlavorDef def, List<int> indices) matchingFlavor) => matchingFlavor.def.specificity);
                    Rand.PopState();
                }
                else
                {
                    bestFlavor = matchingFlavors.First();
                }
                //Log.Warning($"best FlavorDef {bestFlavor.def.ToStringSafe()} matched ingredients [{ingredients.ToStringSafeEnumerable()}] using indices [{bestFlavor.indices.ToStringSafeEnumerable()}] to [{bestFlavor.def.ingredients.Select(slot => "[" + slot.categories.ToStringSafeEnumerable() + "]").ToStringSafeEnumerable()}]\nFlavorDet diet = [{bestFlavor.def.allowedDietKinds.ToStringSafeEnumerable()}]\nghostExcludedCategories = [{excludedCategories.ToStringSafeEnumerable()}]");
                return bestFlavor.def != null && bestFlavor.indices != null
                    ? ((FlavorDef, List<int>))bestFlavor
                    : throw new NullReferenceException("Failed to find a matching Flavor Def. The best Flavor Def [" + bestFlavor.def.ToStringSafe() + "] or its list of indices [" + bestFlavor.indices.ToStringSafeEnumerable() + "] was null.");
            }
            throw new InvalidOperationException("Failed to find a matching Flavor Def. There were no matching Flavor Defs found.");
        }
        catch (Exception ex)
        {
            string errorString = "\ningredients were:";
            for (int i = 0; i < ingredients.Count; i++)
            {
                errorString += string.Format(arg1: ingredients[i].defName, format: "\n{0} {1}", arg0: i);
            }
            ex.Data.Add("ingredients", errorString);
            throw;
        }
        
        // check if the ingredients match the given FlavorDef
        List<int> GetMatchIndices(List<ThingDef> ingredients, FlavorDef flavorDef)
        {
            try
            {
                // if flavorDef is null, skip
                if (flavorDef == null)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("Found a null FlavorDef in list of FinalFlavorDefs to search for a meal. Probably deprecated from an older version of FlavorText. Skipping...");
                    }
                    return null;
                }
                // if flavorDef length doesn't match ingredient list length, skip
                if (ingredients.Count > flavorDef.ingredients.Count || flavorDef.ingredients.Count > ingredients.Count + FlavorTextSettings.numAllowedMissingIngredients)
                {
                    return null;
                }

                // {Potato, Dairy}
                // <MeatRaw, AnimalProductRaw>

                // if incorrect diet kind, skip
                if (flavorDef.allowedDiets.Empty())
                {
                    if (ingredients.Empty())
                    {
                        return null;
                    }
                }
                else if (!flavorDef.allowedDiets.Contains(mealDietKind))
                {
                    return null;
                }

                // {Food, Vegetable, Rice} => {Rice, Vegetable, Food} {2, 1, 0} with [berries, mushrooms] => [0, -1, 1]
                List<int> matchedIndices = Enumerable.Repeat(-1, flavorDef.ingredients.Count()).ToList();
                List<(ThingDef def, int index)> availableIngredients = [.. ingredients.Select((ThingDef value, int i) => (value: value, i: i))];
                // try to match each slot with the first ingredient that fits it
                // note that the slots may be reordered from the XML, however the flavor strings are not, hence the need for FlavorDef.formattingIndices
                // output is the indices of how the slots match with the ingredients

                // when generating from 0 ingredients, ensure there's viable options for all slots
                if (ingredients.Count() == 0)
                {
                    if (flavorDef.ingredients.Any((IngredientSlot slot) => slot.categories.All((FlavorCategoryDef cat) => cat.ThisAndParents.Intersect(excludedCategories).Count() > 0)))
                    {
                        return null;
                    }
                }
                else
                {
                    // {grain, vegetable, fungus/meat} [corn, potato]
                    for (int s = 0; s < flavorDef.ingredients.Count; s++)
                    {
                        IngredientSlot slot = flavorDef.ingredients[s];
                        try
                        {
                            IEnumerable<(ThingDef def, int index)> bestIngredients = availableIngredients.Where(((ThingDef def, int index) item) => slot.AllowedThingDefs.Contains(item.def));
                            if (bestIngredients.Count() != 0)
                            {
                                matchedIndices[s] = bestIngredients.First().index;
                                availableIngredients.Remove(bestIngredients.First());
                            }
                        }
                        catch (Exception)
                        {
                            Log.Error($"{slot?.ToStringSafe()} {s} with categories [{slot?.categories.ToStringSafeEnumerable()}] in {flavorDef?.ToStringSafe()} had an error when attempting to find a matching ingredient.\ningredients = [{ingredients.ToStringSafeEnumerable()}]\nmatchedIndices = [{matchedIndices.ToStringSafeEnumerable()}]\navailableIngredients = [{availableIngredients.Select(((ThingDef def, int index) item) => item.def).ToStringSafeEnumerable()}]");
                            throw;
                        }
                    }
                }
                // # missing ingredients must be LESS than the number of ingredient slots, and less than the number of allowed missing ingredients
                //Log.Warning($"FlavorDef {flavorDef.defName}.\nslots = [{flavorDef.ingredients.Select(slot => "[" + slot.categories.ToStringSafeEnumerable() + "]").ToStringSafeEnumerable()}]\ningredients = [{ingredients.ToStringSafeEnumerable()}]\nmatchedIndices = [{matchedIndices.ToStringSafeEnumerable()}]\ningredientsCopy = [{availableIngredients.ToStringSafeEnumerable()}]");
                int missingIngredients = matchedIndices.Count((int index) => index == -1);
                return (availableIngredients.Empty() && missingIngredients <= FlavorTextSettings.numAllowedMissingIngredients) ? matchedIndices : null;
            }
            catch (Exception ex3)
            {
                ex3.Data.Add("flavorDef", flavorDef?.defName + " was the FlavorDef that caused the error");
                throw;
            }
        }
    }

    // replace placeholders in flavor label/description with the correctly inflected ingredient label
    private string FormatFlavorString((FlavorDef def, List<int> index) flavorTuple, List<ThingDef> ingredients, string flavorString)
    {
        try
        {
            Rand.PushState(Find.World.info.Seed + iteration.Value);
            // {Grain, Foods}
            // [-1, 0]
            // [milk]

            // find placeholders and replace them with the appropriate inflection of the right ingredient
            int n = 0;
            List<NamedArgument> placeholderList = [];
            List<ThingDef> ghostIngredients = [];
            Log.Warning("Found [" + ingredients.ToStringSafeEnumerable() + "] in meal with FlavorDef " + flavorTuple.def.defName.ToStringSafe() + "] and indices [" + flavorTuple.index.ToStringSafeEnumerable() + "]");
            for (int i = 0; i < flavorTuple.def.ingredients.Count; i++)
            {
                IngredientSlot slot = flavorTuple.def.ingredients[i];
                int ingIndex = flavorTuple.index[i];
                int formattingIndex = flavorTuple.def.formattingIndices[i];
                List<string> inflections = [];

                //FT_Grain
                //FT_Grain, FT_Rice, FT_Corn
                //FT_Rice, FT_Corn
                // [FT_AnimalProductRaw]
                // ^[FT_MeatRaw]


                // if you're at a missing ingredient, fill it with a random one from the available categories for a slot
                if (ingIndex == -1)
                {
                    List<FlavorCategoryDef> ghostCategories = slot.categories.SelectMany((FlavorCategoryDef cat) => cat.ThisAndChildren.Where((FlavorCategoryDef childCat) => !childCat.inflectionsOverride.NullOrEmpty() && childCat.ThisAndParents.Intersect(excludedCategories).Count() == 0)).ToList();
                    if (ghostCategories.NullOrEmpty())
                    {
                        Log.Error($"Error when generating ghost ingredients for {flavorTuple.def.ToStringSafe()}, slot {i} with categories [{slot.categories.ToStringSafeEnumerable()}]. The restrictions [{excludedCategories.ToStringSafeEnumerable()}] prevented any ghost ingredients from being generated.");
                        throw new NullReferenceException();
                    }
                    if (!generatedCoreFlavorDef)
                    {
                        IEnumerable<FlavorCategoryDef> coreCats = DietKind.GetFlavorCategoriesFromDiet(mealDietKind);
                        List<FlavorCategoryDef> coreGhostCategories = ghostCategories.Where((FlavorCategoryDef ghostCat) => ghostCat.ThisAndParents.Intersect(coreCats).Count() > 0).ToList();
                        if (!coreGhostCategories.Empty())
                        {
                            generatedCoreFlavorDef = true;
                            ghostCategories = coreGhostCategories;
                        }
                    }
                    if (slot.AllowedThingDefs.Count() == 0)
                    {
                        inflections = ghostCategories.RandomElement().inflectionsOverride;
                    }
                    else
                    {
                        IEnumerable<ThingDef> eles = from thing in ghostCategories.Where((FlavorCategoryDef cat) => cat.childThingDefs.Count > 0).SelectMany((FlavorCategoryDef cat) => cat.childThingDefs)
                                                     where !ingredients.Contains(thing) && !ghostIngredients.Contains(thing)
                                                     select thing;
                        if (eles.Count() == 0)
                        {
                            inflections = ghostCategories.RandomElement().inflectionsOverride;
                        }
                        else
                        {
                            ThingDef ele = eles.RandomElement();
                            ghostIngredients.Add(ele);
                            inflections = InflectionUtility.ThingInflectionsDictionary[ele];
                        }
                    }
                }
                else
                {
                    inflections = InflectionUtility.ThingInflectionsDictionary[ingredients[ingIndex]];
                }
                if (inflections.Count != 4)
                {
                    throw new ArgumentOutOfRangeException($"Error formatting string for {flavorTuple}. Should have {4} inflections, but found {inflections.Count} inflections");
                }
                //TODO: simplify this
                while (true)
                {
                    //capture the placeholder and the word before and after it
                    Match placeholderWithContext = Regex.Match(flavorString, "([^ .,;:]*) *\\{" + formattingIndex + "_plur\\} *([^ .,;:]*)");
                    if (placeholderWithContext.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[0], placeholderWithContext);
                        flavorString = Regex.Replace(flavorString, "\\{" + formattingIndex + "_plur\\}", "{" + n + "}");
                        placeholderList.Add(inflection.Named(n.ToString()));
                        n++;
                        continue;
                    }
                    placeholderWithContext = Regex.Match(flavorString, "([^ .,;:]*) *\\{" + formattingIndex + "_coll\\} *([^ .,;:]*)");
                    if (placeholderWithContext.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[1], placeholderWithContext);
                        flavorString = Regex.Replace(flavorString, "\\{" + formattingIndex + "_coll\\}", "{" + n + "}");
                        placeholderList.Add(inflection.Named(n.ToString()));
                        n++;
                        continue;
                    }
                    placeholderWithContext = Regex.Match(flavorString, "([^ .,;:]*) *\\{" + formattingIndex + "_sing\\} *([^ .,;:]*)");
                    if (placeholderWithContext.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[2], placeholderWithContext);
                        flavorString = Regex.Replace(flavorString, "\\{" + formattingIndex + "_sing\\}", "{" + n + "}");
                        placeholderList.Add(inflection.Named(n.ToString()));
                        n++;
                        continue;
                    }
                    placeholderWithContext = Regex.Match(flavorString, "([^ .,;:]*) *\\{" + formattingIndex + "_adj\\} *([^ .,;:]*)");
                    if (placeholderWithContext.Success)
                    {
                        string inflection = RemoveRepeatedWords(inflections[3], placeholderWithContext);
                        flavorString = Regex.Replace(flavorString, "\\{" + formattingIndex + "_adj\\}", "{" + n + "}");
                        placeholderList.Add(inflection.Named(n.ToString()));
                        n++;
                        continue;
                    }
                    break;
                }
            }
            Rand.PopState();
            flavorString = flavorString.Formatted(placeholderList);
            return flavorString;
        }
        catch (Exception e)
        {
            throw new Exception($"Error when formatting flavor {flavorString.ToStringSafe()} for {flavorTuple.ToStringSafe()} with ingredients [{ingredients.ToStringSafeEnumerable()}]: reason: {e}");
        }

        // remove words repeated directly after each other
        static string RemoveRepeatedWords(string inflection, Match placeholderWithContext)
        {
            // return if a blank inflection, currently only used for the adjectival form of "flour"
            if (inflection == "")
            {
                return inflection;
            }
            List<string> inflectionSplit = inflection.Split(' ').ToList();
            if (placeholderWithContext.Groups.Count != 3)
            {
                throw new ArgumentOutOfRangeException("The number of capture groups from Regex.Match for " + inflection + " was not 3.");
            }

            // if you captured a word before the placeholder, see if it duplicates the first word of "inflection"
            if (Remove.RemoveDiacritics(placeholderWithContext.Groups[1].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit.First()).ToLower())
            {
                inflectionSplit.RemoveAt(0);
            }

            // if you captured a word after the placeholder, see if it duplicates the last word of "inflection"
            if (Remove.RemoveDiacritics(placeholderWithContext.Groups[2].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit.Last()).ToLower())
            {
                inflectionSplit.RemoveLast();
            }
            inflection = string.Join(" ", inflectionSplit);
            return inflection;
        }
    }

    // compile the flavor labels into one long displayed flavor label
    private void CompileFlavorLabels()
    {
        if (!FlavorLabels.NullOrEmpty())
        {
            // don't ask
            StringBuilder stringBuilder = new();
            if (MealTags.Contains("hairy"))
            {
                GrammarRequest request = default;
                request.Includes.Add(RulePackDef.Named("FT_Tags"));
                stringBuilder.Append(GrammarResolver.Resolve("hairy", request));
            }
            for (int j = 0; j < FlavorLabels.Count; j++)
            {
                stringBuilder.AppendWithSeparator(j switch
                {
                    1 => "with ",
                    0 => "",
                    _ => "and ",
                } + GenText.CapitalizeAsTitle(FlavorLabels[j]), " ");
            }
            FinalFlavorLabel = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
    }

    // compile the flavor labels into one long displayed flavor label
    private void CompileFlavorDescriptions()
    {
        try
        {
            if (FlavorDescriptions.NullOrEmpty())
            {
                return;
            }
            Rand.PushState(Find.World.info.Seed + iteration.Value);
            RulePackDef sideDishClauses = RulePackDef.Named("FT_SideDishClauses");  // connector phrases for when meal has multiple FinalFlavorDefs
            StringBuilder stringBuilder = new();
            for (int j = 0; j < FlavorDescriptions.Count; j++)
            {
                if (j == 0)  // if it's the first description, just use the description
                {
                    stringBuilder.Append(CleanUpDescription(FlavorDescriptions[j]));
                }
                if (j > 0)  // if it's the 2nd+ description, in a new paragraph, use a side dish connector clause with the label, then the description
                {
                    // connector clause with side dish label
                    GrammarRequest request = default;  // get a random connector sentence
                    request.Includes.Add(sideDishClauses);
                    stringBuilder.AppendWithSeparator(CleanUpDescription(string.Format(GrammarResolver.Resolve("sidedish", request), FlavorLabels[j], FlavorDescriptions[j])), "\n\n");  // place the current flavor label in its placeholder spot within the sentence
                }
            }
            FinalFlavorDescription = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
        catch (Exception e)
        {
            if (Prefs.DevMode)
            {
                Log.Error($"Error compiling the final flavor description, reason: {e}");
            }
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

    // used to order meat in a more grammatical way when using adjectival forms (e.g. "twisted chicken tacos" rather than "chicken twisted tacos")
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
