
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Verse;
using Verse.Grammar;
using static FlavorText.DietKind;

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
//DONE: update from backup
//DONE: ensure that carnivore meals always generate with at least 1 meat ingredient
//DONE: vegan meals are generating with any FlavorDefs
//--TODO: TryGetFlavorText is running each time a meal is dropped on the ground  // it's running on cursor hover, which is normal
//DONE: FarmersSalad fails b/c only fungus is viable for slot 0
//DONE: something is still sometimes generating with ^
//DONE: improve CompFlavor speed from 8-40 ms
//--TODO: ensure that the full meal diet is factored in correctly when looking at a single group of ingredients // no, for performance
//DONE: paste FlavorDefs are appearing on normal meals

//RELEASE: check all with v1.6
//RELEASE: update XML files
//RELEASE: check remove from game
//RELEASE: check add to game
//RELEASE: check new game
//RELEASE: check updating FlavorText on save
//RELEASE: check save and reload game
//RELEASE: check all meal types
//RELEASE: check food modlist
//RELEASE: check your own saves
//RELEASE: check CommonSense: starting spawned/drop-podded, drop pod meals, trader meals
//RELEASE: test FTV
//RELEASE: test C# meats
//RELEASE: test medieval overhaul
//RELEASE: disable log messages


//TODO: variety matters warnings and errors?
//TODO: milk/cheese problem; in a mod with specialty cheeses, that name should be included, but otherwise milk should sometimes produce the word "cheese" // what about a 5th inflection?
//TODO: WhatsThatMod loses color in its tag
//TODO: [Soy/Chicken, PlantFoodRaw] fails when searching [soy, chicken]
//TODO: test iterations carryover for merge/split/save
//TODO: check how disallowed slot categories are handled
//TODO: sidedishclauses for single flavordef descriptions
//TODO: common sense spawned bread is becoming sourdough
//TODO: take care of issues with deleting RemoveRepeatedWords()
//TODO: holding only 5 random fitting FlavorDefs prevents non-random flavor text generation from working properly
//TODO: fat (and maybe meat) is allowed in vegetarian FlavorDefs
//TODO: some ingredients are getting capitalized in flavor descriptions (Meat, Pumpkin)
//TODO: Vanilla Gourmet Parade meals are appearing as ghost ingredients
//TODO: possible error when removing VCE stews mid-processing

/// <summary>
///  CompFlavor contains the primary code execution
///     a FlavorComp is attached to each meal that should get a new Flavor Text label
///     stores FlavorDef data
///     makes and stores new flavor labels
///     makes and stores new flavor descriptions
///     stores meal details like tick created and chef
/// </summary>
/// 

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

    internal Diet mealDietKind;

    internal List<FlavorCategoryDef> sketchyIngredients = [];

    internal List<FlavorCategoryDef> excludedCategories;

    internal int? iteration = null;

    public List<ThingDef> Ingredients => [.. from def in parent.TryGetComp<CompIngredients>().ingredients.FindAll(i => i != null && FlavorCategoryDefOf.FT_Foods.ContainedInThisOrDescendant(i))
                                          orderby def.defName.GetHashCode()
                                          select def];

    public CompIngredients CompIngredients => parent.TryGetComp<CompIngredients>();

    public CompProperties_Flavor Props => (CompProperties_Flavor)props;

    public override string TransformLabel(string label)
    {
        TryGetFlavorText();
        return parent.stackCount == 1 || FlavorTextSettings.flavorTextForStacks
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
            stringBuilder.AppendLine((parent.stackCount != 1 && !FlavorTextSettings.flavorTextForStacks) ? FinalFlavorLabel : base.TransformLabel(parent.def.label));
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
                        Log.Warning("Found a null or unknown FlavorDef in list of saved FlavorDefs, probably deprecated from an old version of FlavorText. Will get new FlavorDefs");
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
                System.Random r = new();
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
                
                if (Prefs.DevMode)
                {
                    stopwatch.Stop();
                    Log.Message("[Flavor Text] TryGetFlavorText setup ran in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
                    stopwatch.Restart();
                }
                GetFlavorText(flavorDefsToSearch);
            }
        }
        catch (Exception ex)
        {
            string flavorSummary = string.Concat(string.Concat($"Unable to find a matching FlavorDef for meal {parent.ThingID} at {parent.PositionHeld}. Please report." + $"\n{FlavorDef.ActiveFlavorDefs.Count()} FlavorDefs are loaded", $"\n{flavorDefsToSearch?.Count} FlavorDefs were passed into TryGetFlavorDef from a saved game to search within"));
            for (int i = 0; i < Ingredients.Count; i++)
            {
                flavorSummary = string.Concat(flavorSummary + string.Format(arg1: Ingredients[i].defName, format: "\ningredient {0} was {1}", arg0: i), "\ningredient was in the following Flavor Categories");
                foreach (FlavorCategoryDef item in CategoryUtility.ThingParentCategories[Ingredients[i]])
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
            if (Prefs.DevMode)
            {
                stopwatch.Stop();
                Log.Message("[Flavor Text] TryGetFlavorText GetFlavorText ran in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
            }
        }
    }

    //find the best flavorDefs for the parent meal and use them to generate flavor text label and description
    private void GetFlavorText(List<FlavorDef> flavorDefsToSearch)
    {
        //Stopwatch stopwatch = new();
        //stopwatch.Start();
        //set restrictions based on the FoodKind of the meal
        generatedCoreFlavorDef = Ingredients.Count() > 0;

        try
        {
            CalculateMealDiet();

            //Log.Warning($"meal dietKind was {mealDietKind.ToStringSafe()}");
        }
        catch (Exception)
        {
            throw;
        }

        //if (Prefs.DevMode)
        //{
        //    stopwatch.Stop();
        //    Log.Message(">[Flavor Text] GetFlavorText setup " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
        //    stopwatch.Restart();
        //}


        // divide the ingredients into groups of size n and get a flavorDef for each group
        // within each group, move all meat to the front and arrange it in an order that will be more grammatically pleasing
        //TODO: does this work with ghost ingredients?
        List<List<ThingDef>> ingredientChunks = [[]];
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
                bestFlavors = (!ingredientChunks.Empty()) ? ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk, flavorDefsToSearch)).ToList() : [GetBestFlavorDef([], flavorDefsToSearch)];
            }
            catch (Exception ex2) when (ex2 is NullReferenceException or InvalidOperationException)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning("Saved Flavor Text no longer matches for a meal, probably due to a settings change or an old version of FlavorText. Will attempt to get new Flavor Text.");
                }
                bestFlavors = [];
            }
        }
        //if (Prefs.DevMode)
        //{
        //    stopwatch.Stop();
        //    Log.Message(">[Flavor Text] GetFlavorText saved FlavorDefs " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
        //    stopwatch.Restart();
        //}

        // if the above failed, try searching with all valid FlavorDefs
        //TODO: this may be able to be merged with the saved FlavorDefs search
        if (bestFlavors.Empty())
        {
            bestFlavors = (!ingredientChunks.Empty()) ? ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk)).ToList() : [GetBestFlavorDef([])];
            if (bestFlavors.Empty())
            {
                throw new InvalidOperationException("Could not find any best Flavor Defs for meal " + parent.ThingID);
            }
            //if (Prefs.DevMode)
            //{
            //    stopwatch.Stop();
            //    Log.Message(">[Flavor Text] GetFlavorText GetBestFlavorDef " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
            //    stopwatch.Restart();
            //}
        }

        // generate the labels and descriptions for the meal
        GenerateFlavorText(ingredientChunks, bestFlavors);

        //if (Prefs.DevMode)
        //{
        //    stopwatch.Stop();
        //    Log.Message(">[Flavor Text] GetFlavorText GenerateFlavorText ran in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
        //}
    }

    private void CalculateMealDiet()
    {
        excludedCategories = [.. Props.defaultGhostExcludedCategories];
        if (FoodUtility.GetFoodKind(parent) == FoodKind.Meat)
        {
            mealDietKind = CompIngredients.Props.noIngredientsFoodKind == FoodKind.Meat
                ? Diet.hyperCarnivore
                : Ingredients.Any(ing => FoodUtility.GetFoodKind(ing) == FoodKind.NonMeat) ? Diet.omnivore : Diet.carnivore;
        }
        else if (FoodUtility.GetFoodKind(parent) == FoodKind.NonMeat)
        {
            mealDietKind = Diet.vegan;
        }
        else
        {
            if (FoodUtility.GetFoodKind(parent) != FoodKind.Any)
            {
                throw new ArgumentException("Unrecognized FoodKind for meal. FoodKind: " + FoodUtility.GetFoodKind(parent).ToStringSafe());
            }
            mealDietKind = Diet.vegetarian;
        }
        foreach (var dietCat in GetExcludedFlavorCategoriesFromDiet(mealDietKind))
        {
            excludedCategories.AddDistinct(dietCat);
        }


        // check for sketchy ingredients like insect meat and fungus
        foreach (var ing in Ingredients)
        {
            foreach (var sketchy in sketchyDietCategories)
            {
                if (sketchy.ContainedInThisOrDescendant(ing)) sketchyIngredients.Add(sketchy);
            }
        }
    }


    // determine what FlavorDefs the given ingredient list matches, factoring in the mealKind of the parent
    private Diet CalculateIngredientDiet(List<ThingDef> ingredients)
    {
        if (ingredients.All(FlavorCategoryDefOf.FT_MeatRaw.ContainedInThisOrDescendant)) return Diet.hyperCarnivore;
        if (ingredients.All(FlavorCategoryDefOf.FT_PlantFoodRaw.ContainedInThisOrDescendant)) return Diet.vegan;
        if (ingredients.Any(FlavorCategoryDefOf.FT_MeatRaw.ContainedInThisOrDescendant) && ingredients.All(ing => FlavorCategoryDefOf.FT_MeatRaw.ContainedInThisOrDescendant(ing) || FlavorCategoryDefOf.FT_AnimalProductRaw.ContainedInThisOrDescendant(ing))) return Diet.carnivore;
        if (ingredients.Count() > 1 && ingredients.Any(FlavorCategoryDefOf.FT_MeatRaw.ContainedInThisOrDescendant) && ingredients.Any(FlavorCategoryDefOf.FT_PlantFoodRaw.ContainedInThisOrDescendant)) return Diet.omnivore;
        if (ingredients.Any(FlavorCategoryDefOf.FT_AnimalProductRaw.ContainedInThisOrDescendant) && ingredients.All(ing => FlavorCategoryDefOf.FT_AnimalProductRaw.ContainedInThisOrDescendant(ing) || FlavorCategoryDefOf.FT_PlantFoodRaw.ContainedInThisOrDescendant(ing))) return Diet.vegetarian;
        return Diet.omnivore;
    }

    // split ingredients into chunks of size 3 (default)
    private static List<List<T>> Chunk<T>(List<T> source)
    {
        return [.. (from x in source.Select((T x, int i) => new
        {
            Index = i,
            Value = x
        })
                group x by x.Index / 3 into x
                select x.Select(v => v.Value).ToList())];
    }

    // see which FinalFlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    private (FlavorDef, List<int>) GetBestFlavorDef(List<ThingDef> ingredients, List<FlavorDef> flavorDefsToSearch = null)
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        try
        {
            //Log.Warning($"GetBestFlavorDef with ingredients [{ingredients.ToStringSafeEnumerable()}]");
            if (ingredients == null || (ingredients.Empty() && FlavorTextSettings.numAllowedMissingIngredients == 0))
            {
                throw new ArgumentNullException("ingredients", "List of ingredients to search for is null or empty");
            }
            Diet ingredientDiet;
            List<(FlavorDef def, List<int> indices)> matchingFlavors = [];

            if (flavorDefsToSearch.NullOrEmpty())
            {
                //see which FinalFlavorDefs match with the ingredients in the meal

                ingredientDiet = CalculateIngredientDiet(ingredients);
                flavorDefsToSearch = [.. FlavorDef.ValidFlavorDefs(parent, ingredientDiet, flavorDefsToSearch)];
                if (flavorDefsToSearch.NullOrEmpty())
                {
                    throw new InvalidOperationException("Attempted to get list of all valid Flavor Defs for meal type '" + parent.def.defName.ToStringSafe() + "' in [" + CategoryUtility.ThingParentCategories.TryGetValue(parent.def).ToStringSafeEnumerable() + "] but there were none. Please report.");
                }
            }


            Rand.PushState(Find.World.info.Seed + iteration.Value);
            int startIndex = Rand.Range(0, flavorDefsToSearch.Count);
            int j;
            for (int i = 0; i < flavorDefsToSearch.Count; i++)
            {
                j = (i + startIndex) % flavorDefsToSearch.Count;
                FlavorDef flavorDef = flavorDefsToSearch[j];
                List<int> matchedIndices = GetMatchIndices(ingredients, flavorDef);
                if (!matchedIndices.NullOrEmpty())
                {
                    Log.Warning($"found match! {flavorDef.ToStringSafe()} with allowedDiets [{flavorDef.allowedDiets.ToStringSafeEnumerable()}] and mealKinds [{flavorDef.mealKinds.ToStringSafeEnumerable()}]");
                    matchingFlavors.Add((flavorDef, matchedIndices));
                }
                if (matchingFlavors.Count >= 5) break;
            }
            Rand.PopState();

            //if (Prefs.DevMode)
            //{
            //    stopwatch.Stop();
            //    Log.Message(">>[Flavor Text] GetMatchIndices ran in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
            //    stopwatch.Restart();
            //}

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
        //finally
        //{
        //    if (Prefs.DevMode)
        //    {
        //        stopwatch.Stop();
        //        Log.Message(">>[Flavor Text] GetBestFlavorDef end ran in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
        //        // negligible runtime
        //    }
        //}

        // check if the ingredients match the given FlavorDef
        // try to match each slot with the first ingredient that fits it
        // note that the slots may be reordered from the XML, however the flavor strings are not, hence the need for FlavorDef.formattingIndices
        // output is the indices of how the slots match with the ingredients
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


                // if incorrect diet kind, skip
                if (flavorDef.allowedDiets.Empty())
                {
                    if (ingredients.Empty())
                    {
                        //Log.Message($"{flavorDef.ToStringSafe()} failed due to empty allowedDiets and ingredients");
                        return null;
                    }
                }

                // (fungus, insect meat) xx [Egg, Fungus]
                // (fungus, insect meat) <=> [Meat, Fungus]
                // if sketchy ingredients (fungus, insect meat, etc) are in the ingredients, ensure 
                if (!flavorDef.requiredSketchyIngredients.Empty())
                {
                    if (flavorDef.requiredSketchyIngredients.Intersect(sketchyIngredients).Count() != flavorDef.requiredSketchyIngredients.Count())
                    {
                        //Log.Message($"{flavorDef.ToStringSafe()} failed due to requiredSketchyIngredients [{flavorDef.requiredSketchyIngredients.ToStringSafeEnumerable()}]");
                        return null;
                    }
                }


                // {Food, Vegetable, Rice} => {Rice, Vegetable, Food} {2, 1, 0} with [berries, mushrooms] => [0, -1, 1]
                List<int> matchedIndices = [.. Enumerable.Repeat(-1, flavorDef.ingredients.Count())];
                List<(ThingDef def, int index)> availableIngredients = [.. ingredients.Select((ThingDef value, int i) => (value: value, i: i))];

                // when generating from 0 ingredients, ensure there's viable options for all slots
                if (ingredients.Count() == 0)
                {
                    if (flavorDef.ingredients.Any((IngredientSlot slot) => slot.AllowedCategories.Where(cat => cat.childThingDefs.Any()).All(activeCat => activeCat.DescendantOf(excludedCategories))))
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
                int missingIngredients = matchedIndices.Count((int index) => index == -1);
                if (availableIngredients.Empty() && missingIngredients <= FlavorTextSettings.numAllowedMissingIngredients)
                {
                    return matchedIndices;
                }
                else
                {
                    //Log.Message($"{flavorDef.ToStringSafe()} failed due to missing ingredients [{availableIngredients.Select(entry => entry.def.ToStringSafe()).ToStringSafeEnumerable()}]");
                    return null;
                }
            }
            catch (Exception ex3)
            {
                ex3.Data.Add("flavorDef", flavorDef?.defName + " was the FlavorDef that caused the error");
                throw;
            }
        }
    }


    // hypercarnivore => hypercarnivore
    // carnivore => hypercarnivore, carnivore
    // omnivore => hypercarnivore, carnivore, omnivore, vegetarian, vegan
    // vegetarian => vegetarian, vegan
    // vegan => vegan

    // omnivore <=> carnivore

    // generate the labels and descriptions for the meal
    private void GenerateFlavorText(List<List<ThingDef>> ingredientChunks, List<(FlavorDef def, List<int> index)> bestFlavors)
    {
        //Stopwatch stopwatch = new();
        //stopwatch.Start();
        for (int i = 0; i < bestFlavors.Count; i++)
        {
            var flavorTuple = bestFlavors[i];
            if (flavorTuple.def == null || flavorTuple.index == null) throw new NullReferenceException($"A chosen FlavorDef with index of {i.ToStringSafe()} is null, cancelling the search. Please report.");

            // {Meat, Grain, Fruit} [0, 1, 2]
            // {Fruit, Grain, Meat} [2, 1, 0]
            // (Berries, Beef) [0, -1, 1] [2, 1, 0]

            FinalFlavorDefs.Add(bestFlavors[i].def);
            List<ThingDef> ingredientChunk = [.. ingredientChunks[i]];
            //if (Prefs.DevMode)
            //{
            //    stopwatch.Stop();
            //    Log.Message(">>[Flavor Text] GenerateFlavorText setup in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
            //    stopwatch.Restart();
            //    //TODO: this is taking up an oddly large amount of time
            //}

            // fill in missing ingredients with ghost ingredients
            for (int j = 0; j < flavorTuple.def.ingredients.Count; j++)
            {
                if (flavorTuple.index[j] == -1)
                {
                    ThingDef ghost = GenerateGhostIngredient(flavorTuple, ingredientChunk, j, flavorTuple.def.ingredients[j]);
                    ingredientChunk.Add(ghost);
                    flavorTuple.index[j] = ingredientChunk.Count - 1;
                }
            }
            //if (Prefs.DevMode)
            //{
            //    stopwatch.Stop();
            //    Log.Message(">>[Flavor Text] GenerateFlavorText GenerateGhostIngredient in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
            //    stopwatch.Restart();
            //}

            string flavorLabel = FormatFlavorString(bestFlavors[i], ingredientChunk, bestFlavors[i].def.label); // make flavor labels look nicer for main label; replace placeholders in the flavor label with the corresponding ingredient from the meal
            if (flavorLabel.NullOrEmpty())
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"FormatFlavorString failed to get a formatted flavor label for ingredient group {i.ToStringSafe()} containing [{ingredientChunk.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                }
                throw new FormatException();
            }
            FlavorLabels.Add(flavorLabel);
            string flavorDescription = FormatFlavorString(bestFlavors[i], ingredientChunk, bestFlavors[i].def.description);  // make flavor descriptions look nicer for main description; replace placeholders in the flavor description with the corresponding ingredient from the meal
            if (flavorDescription.NullOrEmpty())
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"FormatFlavorString failed to get a formatted flavor description for ingredient group {i.ToStringSafe()} containing [{ingredientChunk.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                }
                throw new FormatException();
            }
            FlavorDescriptions.Add(flavorDescription);

            //if (Prefs.DevMode)
            //{
            //    stopwatch.Stop();
            //    Log.Message(">>[Flavor Text] GenerateFlavorText FormatFlavorString in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
            //    stopwatch.Restart();
            //}

        }

        if (FlavorLabels.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Labels for meal " + base.parent.ThingID.ToStringSafe() + " was empty. Please report.");
        }
        if (FlavorDescriptions.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Descriptions for meal " + base.parent.ThingID.ToStringSafe() + " was empty. Please report.");
        }
        CompileFlavorLabels();
        CompileFlavorDescriptions();
        if (FinalFlavorLabel.NullOrEmpty())
        {
            throw new NullReferenceException("The final compiled and formatted flavor label was null or empty despite getting valid Flavor Defs [" + FinalFlavorDefs.ToStringSafeEnumerable() + "]. Please report.");
        }
        //if (Prefs.DevMode)
        //{
        //    stopwatch.Stop();
        //    Log.Message(">>[Flavor Text] GenerateFlavorText Compile in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
        //}
    }

    // replace placeholders in flavor label/description with the correctly inflected ingredient label
    private string FormatFlavorString((FlavorDef def, List<int> index) flavorTuple, List<ThingDef> ingredients, string flavorString)
    {
        try
        {
            //Stopwatch stopwatch = new();
            //stopwatch.Start();
            Rand.PushState(Find.World.info.Seed + iteration.Value);

            // find placeholders and replace them with the appropriate inflection of the right ingredient
            //Log.Warning("Found [" + ingredients.ToStringSafeEnumerable() + "] in meal with FlavorDef " + flavorTuple.def.defName.ToStringSafe() + " and indices [" + flavorTuple.index.ToStringSafeEnumerable() + "]");
            for (int i = 0; i < flavorTuple.def.ingredients.Count; i++)
            {
                IngredientSlot slot = flavorTuple.def.ingredients[i];
                int ingIndex = flavorTuple.index[i];
                int formattingIndex = flavorTuple.def.formattingIndices[i];
                List<string> inflections = ingIndex != -1
                    ? InflectionUtility.ThingInflectionsDictionary[ingredients[ingIndex]]
                    : throw new ArgumentOutOfRangeException($"found a -1 index in {flavorTuple.def.defName.ToStringSafe()} with indices [{flavorTuple.index.ToStringSafeEnumerable()}] which should have been resolved by now");
                if (inflections.Count != 4)
                {
                    throw new ArgumentOutOfRangeException($"Error formatting string for {flavorTuple}. Should have {InflectionUtility.numInflections} inflections, but found {inflections.Count} inflections");
                }

                //if (Prefs.DevMode)
                //{
                //    stopwatch.Stop();
                //    Log.Message(">>>[Flavor Text] FormatFlavorString setup in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
                //    stopwatch.Restart();
                //}
                for (int j = 0; j < InflectionUtility.grammaticalInflections.Count; j++)
                {
                    string infName = InflectionUtility.grammaticalInflections[j];
                    NamedArgument argument = new("{" + formattingIndex + "_" + infName + "}", inflections[j]);
                    flavorString = flavorString.Replace(argument.arg.ToString(), argument.label.ToString());
                    //TODO: can formatted be used here?
                    // String.Replace is about as fast as Regex.Replace
                }
                //if (Prefs.DevMode)
                //{
                //    stopwatch.Stop();
                //    Log.Message(">>>[Flavor Text] FormatFlavorString loop in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
                //    stopwatch.Restart();
                //}
            }
            Rand.PopState();
            return flavorString;
        }
        catch (Exception e)
        {
            throw new Exception($"Error when formatting flavor {flavorString.ToStringSafe()} for {flavorTuple.ToStringSafe()} with ingredients [{ingredients.ToStringSafeEnumerable()}] and indices [{flavorTuple.index.ToStringSafeEnumerable()}]: reason: {e}");
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

    private ThingDef GenerateGhostIngredient((FlavorDef def, List<int> index) flavorTuple, List<ThingDef> ingredients, int slotIndex, IngredientSlot slot)
    {
        ThingDef ghost = null;
        List<FlavorCategoryDef> ghostCategories = [.. slot.categories.SelectMany((FlavorCategoryDef cat) => cat.ThisAndChildren.Where((FlavorCategoryDef childCat) => childCat.childThingDefs.Count > 0 && !childCat.inflectionsOverride.NullOrEmpty() && !childCat.DescendantOf(slot.disallowedCategories) && !childCat.DescendantOf(excludedCategories)))];
        if (ghostCategories.NullOrEmpty())
        {
            Log.Error($"Error when generating ghost ingredients for {flavorTuple.def.ToStringSafe()}, slot {slotIndex} with categories [{slot.categories.ToStringSafeEnumerable()}]. The restrictions [{excludedCategories.ToStringSafeEnumerable()}] prevented any ghost ingredients from being generated.");
            throw new NullReferenceException();
        }
        if (!generatedCoreFlavorDef)
        {
            List<FlavorCategoryDef> coreCats = DietKind.dietIncludedCategories[mealDietKind];
            List<FlavorCategoryDef> coreGhostCategories = [.. ghostCategories.Where((FlavorCategoryDef ghostCat) => ghostCat.DescendantOf(coreCats))];
            if (!coreGhostCategories.Empty())
            {
                generatedCoreFlavorDef = true;
                ghostCategories = coreGhostCategories;
            }
        }

        if (slot.AllowedThingDefs.Count() == 0)
        {
            throw new ArgumentOutOfRangeException($"No AllowedThingDefs found for slot {slotIndex} with categories [{slot.categories.Select(cat => cat.defName).ToStringSafeEnumerable()}] in flavorDef {flavorTuple.def.defName.ToStringSafe()}");
        }
        // try to use a random ingredient that exists
        else
        {
            IEnumerable<ThingDef> eles = from thing in ghostCategories.Where((FlavorCategoryDef cat) => cat.childThingDefs.Count > 0).SelectMany((FlavorCategoryDef cat) => cat.childThingDefs)
                                         where !ingredients.Contains(thing)
                                         select thing;
            if (eles.Count() > 0)
            {
                ghost = eles.RandomElement();
            }
            // if no valid random ingredient, allow repetitions
            else
            {
                eles = from thing in ghostCategories.Where((FlavorCategoryDef cat) => cat.childThingDefs.Count > 0).SelectMany((FlavorCategoryDef cat) => cat.childThingDefs)
                       select thing;
                if (eles.Count() > 0)
                {
                    ghost = eles.RandomElement();
                }
                else
                {
                    throw new ArgumentOutOfRangeException($"No AllowedThingDefs found for slot {slotIndex} with ghost categories [{ghostCategories.ToStringSafeEnumerable()}] in flavorDef {flavorTuple.def.defName.ToStringSafe()}");
                }
            }
        }

        return ghost == null
            ? throw new NullReferenceException($"Failed to generate ghost ingredient for {flavorTuple.def.defName.ToStringSafe()}. Slot {slotIndex} with ghost categories [{ghostCategories.ToStringSafeEnumerable()}] in flavorDef {flavorTuple.def.defName.ToStringSafe()}")
            : ghost;
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
                not null when CategoryUtility.ThingParentCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Blood")) => 0,
                not null when CategoryUtility.ThingParentCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Twisted")) => 1,
                not null when CategoryUtility.ThingParentCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Human")) => 3,
                not null when CategoryUtility.ThingParentCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Insect")) => 6,
                not null when CategoryUtility.ThingParentCategories[ing1].Contains(FlavorCategoryDef.Named("FT_MeatRaw")) => 9,
                _ => 12
            }, ing2 switch
            {
                not null when CategoryUtility.ThingParentCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Blood")) => 0,
                not null when CategoryUtility.ThingParentCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Twisted")) => 1,
                not null when CategoryUtility.ThingParentCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Human")) => 3,
                not null when CategoryUtility.ThingParentCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Insect")) => 6,
                not null when CategoryUtility.ThingParentCategories[ing2].Contains(FlavorCategoryDef.Named("FT_MeatRaw")) => 9,
                _ => 12
            }];

            int difference = ranking[1] - ranking[0];
            return difference;
        }
    }
}
