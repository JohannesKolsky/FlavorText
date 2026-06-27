
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
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
//DONE: this can mismatch; in VV, some foods are categorized in FT_FoodRaw, which can create omnivore ingredient diet with a vegan meal diet
//DONE: ruined grill conversion

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
//--TODO: merge stack error on dev quicktest // b/c game starts paused
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
//DONE: Pyon hornet jelly and smokey honey aren't being considered ingredients for Flavor Text
//DONE: VCE chili peppers may be invalid
//DONE: FTV isn't accessing FinalFlavorDefs at all
//DONE: take care of issues with deleting RemoveRepeatedWords()
//DONE: remove if (Prefs.DevMode) requirement for errors?
//DONE: fat (and maybe meat) is allowed in vegetarian FlavorDefs
//DONE: some ingredients are getting capitalized in flavor descriptions (Meat, Pumpkin)  // medieval overhaul has inconsistent capitalization
//DONE: WhatsThatMod loses color in its tag
//DONE: error when saving VCE stews mid-processing
//DONE: test iterations carryover for merge/split/save
//DONE: simple meal jjigae with 1-ingredient dried meat fails to generate ghost ingredient
//DONE: 0-ingredient meals only get condiment ghost ingredients
//--TODO: VCE take bring soup to pot option not appearing // not from FT
//DONE: check how disallowed slot categories are handled
//DONE: spawned bread is becoming sourdough
//DONE: sort error ghost ingredients
//DONE: certain spawned meals still cause an error on first save and reload
//DONE: 4-6 FlavorDefs bug out on Debug meal spawn (not enough types of ingredients, sketchy ingredient issues)
//DONE: for ghost ingredients add 0-n random, then search
//DONE: Vanilla Gourmet Parade meals are appearing as ghost ingredients
//DONE: holding only 5 random fitting FlavorDefs prevents non-random flavor text generation from working properly
//--TODO: variety matters warnings and errors?  // from Variety Matters Redux, not Flavor Text
//DONE: bad cooks make weirder meals?
//RELEASED: TryAddGhostIngredients should attempt to fill out the recipe if the meal has no ingredients
//DONE: simple dessert doesn't seem to add as many ing as it should on average
//DONE: fix pluralizations in side dish clauses
//DONE: side dish label rulepacks are repetitive

//RELEASE: update XML files
//RELEASE: check new game
//RELEASE: check add to game
//RELEASE: check remove from game
//RELEASE: check updating FlavorText on save
//RELEASE: check save and reload game
//RELEASE: check all meal types
//RELEASE: check without DLCs or mods
//RELEASE: check food modlist
//RELEASE: check your own saves
//RELEASE: check starting spawned/drop-podded, drop pod meals, trader meals
//RELEASE: test FTV
//RELEASE: test C# meats
//RELEASE: test medieval overhaul
//RELEASE: test translations
//RELEASE: check speed
//RELEASE: disable log messages
///TODO: error spawnMode Near when FT fillUpBlankMeals == true && CS fillMeals == false
///when initially spawned, a meal is vegetarian; however FT puts an ingredient in it, which can change the diet kind
///if the game attempts to spawn a vegetarian meal on a non-vegetarian meal, it throws the error
///ultimately the issue is that by the time you hit PreAbsorbStack, you're committed to merging the meals, and adding new ingredients at that stage causes problems
///clicking on the stack before spawning in the new meal makes it so the error doesn't trigger
///A DIFFERENT ERROR when FT addExtraIngredients > 0 && (fillUpBlank Meals == true || CS fillMeals == true)
///mealDiet is incorrect, which allows extra incorrect ingredients to be added, which changes the FoodKind
///when this happens while stacks are committed to being merged, it causes issues
//TODO: FT_Coconut being in FT_Fruit and FT_Nut causes weird behavior for spawning every FlavorDef

//TODO: milk/cheese problem; in a mod with specialty cheeses, that name should be included, but otherwise milk should sometimes produce the word "cheese" // what about a 5th inflection?
//TODO: [Soy/Chicken, PlantFoodRaw] fails when searching [soy, chicken]
//TODO: sidedishclauses for single flavordef descriptions
//TODO: add list operators, like {0_plur_ALL}
//TODO: AC hemp oil => oil when used as ingredient. Is there a way to use the hemp oil label?
//TODO: bad cooks make weirder meals?
//TODO: can mealTags replace sketchyIngredients?
//TODO: spawnCaravanInventory meals have fewer ingredients on average than extraIngredientCap should make
//TODO: sing/coll/adj inflections for FlavorDef labels: "a peach smoothie"/"peach smoothie"; "an apple fritter"/"apple fritters"/"apple fritter"
//TODO: VNPE seems to pick the same FlavorDef repeatedly within a given meal, not always, but way more often than it should

/// <summary>
///  CompFlavor contains the primary code execution
///     a FlavorComp is attached to each meal that should get a new Flavor Text label
///     stores FlavorDef data
///     makes and stores new flavor labels
///     makes and stores new flavor descriptions
///     stores meal details like tick created and chef
///     
///     1-ingredient meal: 3 ms
///     40-ingredient meal: 31 ms
///     
///     during gameplay TryGetFlavorText is only called when needed:
///         making a product from a cooking station
///         merging stacks for absorbing stack
///         loading game from save if TriedFlavorText == true
///         selecting the meal
/// </summary>
/// 

namespace FlavorText;

public class CompFlavor : ThingComp, IExposable
{

    private readonly bool tag;
    private const int numFlavorDefsBeforeBreak = 10;
    private bool generatedCoreFlavorDef = true;

    internal bool TriedFlavorText { get; set; }

    private bool generatedGhostIngredients;
    internal bool GeneratedGhostIngredients { get => generatedGhostIngredients; set => generatedGhostIngredients = value; }

    private List<string> flavorLabels = [];

    private string finalFlavorLabel;

    private List<string> flavorDescriptions = [];

    private string finalFlavorDescription;

    private List<FlavorDef> finalFlavorDefs = [];
    public List<FlavorDef> FinalFlavorDefs { get => finalFlavorDefs; }

    private Diet mealDiet;

    private readonly List<FlavorCategoryDef> sketchyIngredients = [];

    internal List<FlavorCategoryDef> excludedCategories;


    private ThingDef cookingStation;
    private int? tickCreated = null;
    private int? iteration = null;
    private int? hourOfDay = null;
    private string cookID = "";
    private List<string> mealTags = [];

    public ThingDef CookingStation { get => cookingStation; set => cookingStation = value; }
    public int? TickCreated { get => tickCreated; set => tickCreated = value; }
    public int? Iteration { get => iteration; set => iteration = value; }

    public int? HourOfDay { get => hourOfDay; set => hourOfDay = value; }
    public string CookID { get => cookID; set => cookID = value; }

    public List<string> MealTags { get => mealTags; set => mealTags = value; }

    internal List<ThingDef> ingredientsCached;

    internal List<ThingDef> Ingredients
    {
        get
        {
            Iteration ??= GameComponentFlavorText.Iterate();
            if (ingredientsCached == null) { Rand.PushState(FlavorSeed);  ingredientsCached = [..parent.TryGetComp<CompIngredients>().ingredients.FindAll(i => i != null && FlavorCategoryDefOf.FT_Ingredients.ContainedInThisOrDescendant(i)).OrderBy(def => def.defName).OrderBy(def => Rand.Value)] ; Rand.PopState(); }
            return ingredientsCached;
        }
    }

    private int FlavorSeed => Find.World.info.Seed + Iteration.Value;

    private CompIngredients CompIngredients => parent.TryGetComp<CompIngredients>();

    public CompProperties_Flavor Props => (CompProperties_Flavor)props;

    public override string TransformLabel(string label)
    {
        TryGetFlavorText();
        return parent.stackCount == 1 || FlavorTextSettings.flavorTextForStacks
            ? (!finalFlavorLabel.NullOrEmpty()) ? (finalFlavorLabel + " (" + base.TransformLabel(label) + ")") : base.TransformLabel(label)
            : base.TransformLabel(label);
    }

    public override string GetDescriptionPart()
    {
        return (!finalFlavorDescription.NullOrEmpty()) ? finalFlavorDescription : base.GetDescriptionPart();
    }

    public override string CompInspectStringExtra()
    {
        if (!finalFlavorLabel.NullOrEmpty())
        {
            StringBuilder stringBuilder = new();
            stringBuilder.AppendLine((parent.stackCount != 1 && !FlavorTextSettings.flavorTextForStacks) ? finalFlavorLabel : base.TransformLabel(parent.def.label));
            return stringBuilder.ToString().TrimEndNewlines();
        }
        return base.CompInspectStringExtra();
    }

    //TODO: seems like some saved FlavorDefs are still taking up to 4 ms to load for bigger meals
    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref generatedGhostIngredients, "generatedGhostIngredients");
        Scribe_Defs.Look(ref cookingStation, "cookingStation");
        Scribe_Values.Look(ref hourOfDay, "hourOfDay");
        Scribe_Values.Look(ref tickCreated, "tickCreated");
        Scribe_Values.Look(ref iteration, "iteration");
        Scribe_Values.Look(ref cookID, "cookID");
        Scribe_Collections.Look(ref mealTags, "tags", LookMode.Undefined);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && MealTags == null)
        {
            MealTags = [];
        }
        try
        {
            Scribe_Collections.Look(ref finalFlavorDefs, "finalFlavorDefs", LookMode.Undefined);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (finalFlavorDefs == null)
                {
                    finalFlavorDefs = [];
                }
                else if (finalFlavorDefs.Any(def => def == null || DefDatabase<FlavorDef>.GetNamedSilentFail(def.defName.ToString()) == null))
                {
                    finalFlavorDefs = [];
                    if (Prefs.DevMode) Log.Warning($"Found a null or unknown FlavorDef in list of saved FlavorDefs for meal {parent.ThingID.ToStringSafe()} at {parent.PositionHeld.ToStringSafe()} with ingredients [{Ingredients.ToStringSafeEnumerable()}], probably deprecated from an old version of FlavorText. Will get new FlavorDefs");
                }
            }
        }
        catch (Exception ex)
        {
            if (Prefs.DevMode) Log.Warning($"Found an invalid FlavorDef. Will attempt to get new Flavor Text. Error: {ex}");
        }
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            // retry flavor text, ignoring meals where it hasn't been triggered yet
            if (TriedFlavorText)
            {
                TriedFlavorText = false;
                TryGetFlavorText(finalFlavorDefs);
            }
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
                otherCompFlavor.GeneratedGhostIngredients = GeneratedGhostIngredients;
                otherCompFlavor.TriedFlavorText = TriedFlavorText;
                otherCompFlavor.finalFlavorDefs = finalFlavorDefs;
                otherCompFlavor.flavorLabels = flavorLabels;
                otherCompFlavor.flavorDescriptions = flavorDescriptions;
                otherCompFlavor.finalFlavorLabel = finalFlavorLabel;
                otherCompFlavor.finalFlavorDescription = finalFlavorDescription;
                otherCompFlavor.CookingStation = CookingStation;
                otherCompFlavor.HourOfDay = HourOfDay;
                otherCompFlavor.TickCreated = TickCreated;
                otherCompFlavor.MealTags = MealTags;
                otherCompFlavor.Iteration = Iteration;
                otherCompFlavor.CookID = CookID;
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
            Log.Warning($"PreAbsorb stack start for {parent.ToStringSafe()} x{parent.stackCount.ToStringSafe()} spawned={parent.Spawned.ToStringSafe()} at {parent.PositionHeld.ToStringSafe()} with mealKind={FoodUtility.GetFoodKind(parent)} and ingredients [{CompIngredients.ingredients.ToStringSafeEnumerable()}]\nOtherstack was {otherStack.ToStringSafe()} x{otherStack.stackCount.ToStringSafe()} spawned={otherStack.Spawned.ToStringSafe()} at {otherStack.PositionHeld.ToStringSafe()} with mealKind={FoodUtility.GetFoodKind(otherStack)} and ingredients [{otherStack.TryGetComp<CompIngredients>().ingredients.ToStringSafeEnumerable()}]");
            base.PreAbsorbStack(otherStack, count);
            TryGetFlavorText();

            CompFlavor otherFlavorComp = otherStack.TryGetComp<CompFlavor>();
            Rand.PushState(FlavorSeed);

            IEnumerable<CompFlavor> bothComps = [this, otherFlavorComp];

            otherFlavorComp.Iteration = Iteration = bothComps.Select(comp => comp.Iteration).Where(iteration => iteration != null).OrderBy(ele => Rand.Value).FirstOrFallback(null);
            otherFlavorComp.HourOfDay = HourOfDay = bothComps.Select(comp => comp.HourOfDay).Where(hourOfDay => hourOfDay != null).OrderBy(ele => Rand.Value).FirstOrFallback(null);
            otherFlavorComp.TickCreated = TickCreated = bothComps.Select(comp => comp.TickCreated).Where(tickCreated => tickCreated != null).OrderBy(ele => Rand.Value).FirstOrFallback(null);
            otherFlavorComp.CookingStation = CookingStation = bothComps.Select(comp => comp.CookingStation).Where(cookingStation => cookingStation != null).OrderBy(ele => Rand.Value).FirstOrFallback(null);
            otherFlavorComp.CookID = CookID = bothComps.Select(comp => comp.CookID).Where(cookID => cookID != "").OrderBy(ele => Rand.Value).FirstOrFallback("");
            try
            {
                List<string> mergedTags = [];
                List<string> mergedTagsCleaned = [];
                if (!MealTags.NullOrEmpty()) mergedTags.AddRange(MealTags);
                if (!otherFlavorComp.MealTags.NullOrEmpty()) mergedTags.AddRange(otherFlavorComp.MealTags);
                foreach (var tag in mergedTags)
                {
                    if (mergedTags.Count(m => m == tag) >= 2) // 100% to keep tag if it's in both lists
                    {
                        mergedTagsCleaned.AddDistinct(tag);
                    }
                    else if (Rand.Range(0, 10) > 0)  // 90% to keep tag if it's only in 1 list
                    {
                        mergedTagsCleaned.AddDistinct(tag);
                    }
                }
                otherFlavorComp.MealTags = MealTags = mergedTagsCleaned;
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
            TriedFlavorText = otherFlavorComp.TriedFlavorText = false;
            ingredientsCached = otherFlavorComp.ingredientsCached = null;
        }
        catch (Exception e)
        {
            if (Prefs.DevMode)
            {
                Log.Error($"Failed to merge stacks properly, reason: {e}");
            }
        }
    }

    private void ResetFlavorDefsAndText()
    {
        flavorLabels = [];
        finalFlavorLabel = null;
        flavorDescriptions = [];
        finalFlavorDescription = null;
        finalFlavorDefs = [];
    }


    // must be public b/c FTV accesses it
    public void TryGetFlavorText(List<FlavorDef> flavorDefsToSearch = null)
    {
        if (TriedFlavorText) return;
        TriedFlavorText = true;
        Stopwatch stopwatch = new();
        stopwatch.Start();
        try
        {
            if (Ingredients == null) throw new NullReferenceException($"Ingredients for {parent.ThingID.ToStringSafe()} were null. Please report.");
            ResetFlavorDefsAndText();
            Iteration ??= GameComponentFlavorText.Iterate();

            // fill in the extra parameters with pseudorandom data if they are null
            TickCreated ??= GenTicks.TicksAbs;
            if (TickCreated == null || TickCreated < 0) Log.Error($"meal {parent.ToStringSafe()} had TickCreated = {TickCreated.ToStringSafe()} which is an invalid value. Please report.");
            Rand.PushState(FlavorSeed);
            HourOfDay ??= Rand.Range(0, 24);
            if (CookingStation == null)
            {
                List<ThingDef> allCookingStations = [.. FlavorCategoryDefOf.FT_CookingStations.DescendantThingDefs.Distinct()];
                CookingStation = allCookingStations[Rand.Range(0, allCookingStations.Count - 1)];
            }
            Rand.PopState();

            //set restrictions based on the FoodKind of the meal and weird ingredients
            SetMealDiet();
            //Log.Warning($"diet for {parent.ThingID} at {parent.PositionHeld} is {mealDiet}");

            TryAddExtraIngredients();
            TriedFlavorText = true;

            // actually try to get some FlavorDefs and generate some flavor text from them
            GetFlavorText(flavorDefsToSearch);
            //if (Ingredients.Empty()) Log.Warning($"0 ingredients for {parent.ThingID.ToStringSafe()} at {parent.PositionHeld.ToStringSafe()}");



        }
        catch (Exception ex)
        {
            string flavorSummary = string.Concat(string.Concat($"Unable to find a matching FlavorDef for meal {parent.ThingID.ToStringSafe()} at {parent.PositionHeld.ToStringSafe()}. Please report." + $"\n{FlavorDef.ActiveFlavorDefs.Count()} FlavorDefs are loaded", $"\n{flavorDefsToSearch?.Count} FlavorDefs were passed into TryGetFlavorDef from a saved game to search within"));
            for (int i = 0; i < Ingredients.Count; i++)
            {
                flavorSummary = string.Concat(flavorSummary + string.Format(arg1: Ingredients[i].defName, format: "\ningredient {0} was {1}", arg0: i), "\ningredient was in the following Flavor Categories");
                foreach (FlavorCategoryDef item in CategoryUtility.ThingParentCategories[Ingredients[i]])
                {
                    flavorSummary = flavorSummary + "\n" + item.defName;
                }
            }
            ex.Data.Add("allIngredients", flavorSummary);
            Log.Error(string.Format("Error: {0}\n{1}\n{2}\n{3}\n{4}\n{5}\n{6}", ex, ex.Data["flavorSummary"], ex.Data["flavorDef"], ex.Data["ingredients"], ex.Data["diets"], ex.Data["meal"], ex.Data["flavorDefsToSearch"]));
        }
        finally
        {
            if (Prefs.DevMode)
            {
                stopwatch.Stop();
                Log.Message("[Flavor Text] TryGetFlavorText ran in " + stopwatch.Elapsed.TotalMilliseconds + " milliseconds");
            }
        }
    }

    //find the best flavorDefs for the parent meal and use them to generate flavor text label and description
    private void GetFlavorText(List<FlavorDef> flavorDefsToSearch)
    {
        // divide the ingredients into groups of size n and get a flavorDef for each group
        // within each group, move all meat to the front and arrange it in an order that will be more grammatically pleasing
        List<List<ThingDef>> ingredientChunks = [[]];
        if (Ingredients.Count() > 0)
        {
            ingredientChunks = [.. from chunk in Chunk(Ingredients)
                                select chunk.OrderByDescending(m => m, new MeatComparer()).ToList()];
        }
        else return;
        List<(FlavorDef def, List<int> index)> bestFlavors = [];
        // try searching in any saved FlavorDefs that you were given
        if (!flavorDefsToSearch.NullOrEmpty())
        {
            try
            {
                bestFlavors = [.. ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk, flavorDefsToSearch))];
            }
            catch (Exception ex2) when (ex2 is NullReferenceException or InvalidOperationException)
            {
                if (Prefs.DevMode) Log.Warning($"Saved FlavorDefs [{flavorDefsToSearch.ToStringSafeEnumerable()}] no longer matches for meal {parent.ThingID.ToStringSafe()} at {parent.PositionHeld.ToStringSafe()} with ingredients [{Ingredients.ToStringSafeEnumerable()}], probably due to a settings change or an old version of FlavorText. Will attempt to get new Flavor Text. Error: \n\n{ex2}");
                bestFlavors = [];
            }
        }

        // if the above failed, try searching with all valid FlavorDefs
        //TODO: this may be able to be merged with the saved FlavorDefs search
        if (bestFlavors.Empty())
        {
            bestFlavors = (!ingredientChunks.Empty()) ? ingredientChunks.Select(ingredientChunk => GetBestFlavorDef(ingredientChunk)).ToList() : [GetBestFlavorDef([])];
            if (bestFlavors.Empty()) throw new InvalidOperationException("Could not find any best Flavor Defs for meal " + parent.ThingID);
        }

        // generate the labels and descriptions for the meal
        GenerateFlavorText(ingredientChunks, bestFlavors);

    }

    private void SetMealDiet()
    {
        excludedCategories = [.. Props.defaultGhostExcludedCategories];
        if (Ingredients.Count == 0)
        {
            if (FoodUtility.GetFoodKind(parent) == FoodKind.NonMeat) mealDiet = Diet.vegan;
            else if (FoodUtility.GetFoodKind(parent) == FoodKind.Meat) mealDiet = Diet.carnivore;
            else mealDiet = Diet.vegetarian;
        }
        else
        {
            if (FoodUtility.GetFoodKind(parent) == FoodKind.NonMeat)
            {
                mealDiet = Diet.vegan;
            }
            else
            {
                mealDiet = CalculateIngredientDiet(Ingredients);
            }
            //Log.Message($"{parent.ToStringSafe()} had mealDiet = {mealDiet.ToStringSafe()}");


            // check for sketchy ingredients like insect meat and fungus
            foreach (var ing in Ingredients)
            {
                foreach (var sketchy in SketchyDietCategories)
                {
                    if (sketchy.ContainedInThisOrDescendant(ing)) sketchyIngredients.Add(sketchy);
                }
            }

            foreach (var dietCat in GetExcludedFlavorCategoriesFromDiet(mealDiet))
            {
                excludedCategories.AddDistinct(dietCat);
            }
        }


        Log.Warning($"mealDiet for {parent.ThingID.ToStringSafe()} was {mealDiet.ToStringSafe()} and meal FoodKind was {FoodUtility.GetFoodKind(parent).ToStringSafe()} and noIngredientsFoodKind was {CompIngredients.Props.noIngredientsFoodKind.ToStringSafe()}");
    }

    // determine what FlavorDefs the given ingredient list matches
    private Diet CalculateIngredientDiet(List<ThingDef> ingredients)
    {
        (bool, bool, bool) dietTuple = (false, false, false);
        foreach (var ing in ingredients)
        {
            if (FlavorCategoryDefOf.FT_MeatRaw.ContainedInThisOrDescendant(ing)) { dietTuple.Item1 = true; }
            if (FlavorCategoryDefOf.FT_AnimalProductRaw.ContainedInThisOrDescendant(ing)) { dietTuple.Item2 = true; }
            if (FlavorCategoryDefOf.FT_PlantFoodRaw.ContainedInThisOrDescendant(ing)) { dietTuple.Item3 = true; }
        }

        Log.Message($"dietTuple was {dietTuple.ToStringSafe()}");
       
        switch (dietTuple)
        {
            case (false, false, false): return Diet.vegan; // if everything is false (all condiments), default to the most restricted: vegan
            case (true, false, false): return Diet.hyperCarnivore;
            case (false, true, false): return Diet.animalProduct;
            case (false, false, true): return Diet.vegan;
            case (true, true, false): return Diet.carnivore;
            case (true, false, true): return Diet.animalFree;
            case (false, true, true): return Diet.vegetarian;
            case (true, true, true): return Diet.omnivore;
        }

    }

    // split ingredients into chunks of size 3 (default)
    private static List<List<T>> Chunk<T>(List<T> source)
    {
        return [.. from x in source.Select((T x, int i) => new
        {
            Index = i,
            Value = x
        })
                group x by x.Index / 3 into x
                select x.Select(v => v.Value).ToList()];
    }

    // see which FinalFlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
    private (FlavorDef, List<int>) GetBestFlavorDef(List<ThingDef> ingredients, List<FlavorDef> flavorDefsToSearch = null)
    {
        string ingredientsDietString = "";
        try
        {
            if (Ingredients.NullOrEmpty())
            {
                throw new ArgumentNullException("ingredients", "List of ingredients to search for is null or empty during GetBestFlavorDef(). Should have returned during TryGetFlavorText() or GetFlavorText(). Please report.");
            }
            List<(FlavorDef def, List<int> indices)> matchingFlavors = [];

            //see which FinalFlavorDefs match with the ingredients in the meal

            Diet diet;
            if (!ingredients.Empty()) diet = CalculateIngredientDiet(ingredients);
            else diet = mealDiet;
            ingredientsDietString = diet.ToStringSafe();
            List<FlavorDef> validFlavorDefsToSearch = [.. FlavorDef.ValidFlavorDefs(parent, diet, flavorDefsToSearch)];
            if (validFlavorDefsToSearch.NullOrEmpty())
            {
                throw new InvalidOperationException("Attempted to get list of all valid Flavor Defs for meal type '" + parent.def.defName.ToStringSafe() + "' in [" + CategoryUtility.ThingParentCategories[parent.def].ToStringSafeEnumerable() + "] but there were none. Please report.");
            }
            


            Rand.PushState(FlavorSeed);
            int startIndex = Rand.Range(0, validFlavorDefsToSearch.Count);

            int j;
            for (int i = 0; i < validFlavorDefsToSearch.Count; i++)
            {
                j = (i + startIndex) % validFlavorDefsToSearch.Count;
                FlavorDef flavorDef = validFlavorDefsToSearch[j];
                List<int> matchedIndices = GetMatchIndices(ingredients, flavorDef);
                if (!matchedIndices.NullOrEmpty())
                {
                    //Log.Warning($"found match! {flavorDef.ToStringSafe()} with allowedDiets [{flavorDef.allowedDiets.ToStringSafeEnumerable()}] and mealKinds [{flavorDef.mealKinds.ToStringSafeEnumerable()}]");
                    matchingFlavors.Add((flavorDef, matchedIndices));
                }
                if (FlavorTextSettings.quickSearch == true && matchingFlavors.Count >= numFlavorDefsBeforeBreak) break;
            }
            Rand.PopState();

            // pick the most specific matching FlavorDef
            // note that the slots may be reordered from the XML, however the flavor strings are not, hence the need for FlavorDef.slotIndices
            if (matchingFlavors.Count > 0)
            {
                //foreach (var (def, indices) in matchingFlavors) { Log.Message(def.defName + " = " + def.specificity); }
                (FlavorDef def, List<int> indices) bestFlavor;
                
                Rand.PushState(FlavorSeed);
                bestFlavor = matchingFlavors.RandomElementByWeight(((FlavorDef def, List<int> indices) matchingFlavor) => matchingFlavor.def.Specificity);
                Rand.PopState(); bestFlavor = matchingFlavors.First();
                
                //Log.Warning($"best FlavorDef {bestFlavor.def.ToStringSafe()} matched ingredients [{ingredients.ToStringSafeEnumerable()}] using indices [{bestFlavor.indices.ToStringSafeEnumerable()}] to [{bestFlavor.def.ingredients.Select(slot => "[" + slot.categories.ToStringSafeEnumerable() + "]").ToStringSafeEnumerable()}]\nFlavorDet diet = [{bestFlavor.def.allowedDiets.ToStringSafeEnumerable()}]\nghostExcludedCategories = [{excludedCategories.ToStringSafeEnumerable()}]");
                return bestFlavor.def != null && bestFlavor.indices != null
                    ? ((FlavorDef, List<int>))bestFlavor
                    : throw new NullReferenceException("Failed to find a matching Flavor Def. The best Flavor Def [" + bestFlavor.def.ToStringSafe() + "] or its list of indices [" + bestFlavor.indices.ToStringSafeEnumerable() + "] was null.");
            }
            throw new InvalidOperationException($"Failed to find a matching Flavor Def. There were no matching Flavor Defs found. Received {flavorDefsToSearch?.Count} flavorDefsToSearch and there were {validFlavorDefsToSearch?.Count} validFlavorDefsToSearch. All flavorDefsToSearch list:\n[{flavorDefsToSearch.ToStringSafeEnumerable()}]");
        }
        catch (Exception ex)
        {
            string errorString = "\ningredients were:";
            for (int i = 0; i < ingredients.Count; i++)
            {
                errorString += string.Format(arg1: ingredients[i].defName, format: "\n{0} {1}", arg0: i);
            }
            ex.Data.Add("ingredients", errorString);
            string errorString2 = "\ndiets were:";
            errorString2 += $"\nmealDietKind = {mealDiet}\ningredientDietKind = {ingredientsDietString}";
            ex.Data.Add("diets", errorString2);
            string errorString3 = "\nmeals were:";
            errorString3 += $"\nmeal was type {parent.def.defName.ToStringSafe()}\nmeal is at location ({parent.PositionHeld.ToStringSafe()})";
            ex.Data.Add("meal", errorString3);
            string errorString4 = "\nflavorDefsToSearch were:";
            errorString4 += $"\n[{flavorDefsToSearch.ToStringSafeEnumerable()}]";
            ex.Data.Add("flavorDefsToSearch", errorString4);
            throw;
        }

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
                    Log.Warning("Found a null FlavorDef in list of FinalFlavorDefs to search for a meal. Probably deprecated from an older version of FlavorText. Skipping...");
                    return null;
                }
                // if flavorDef length doesn't match ingredient list length, skip
                if (ingredients.Count != flavorDef.Ingredients.Count)
                {
                    return null;
                }


                // if incorrect diet kind, skip
                if (flavorDef.allowedDiets.Empty())
                {
                    if (ingredients.Empty())
                    {
                        Log.Warning($"{flavorDef.ToStringSafe()} failed due to empty allowedDiets and ingredients");
                        return null;
                    }
                }

                //TODO: is this needed anymore?
                // (fungus, insect meat) xx [Egg, Fungus]
                // (fungus, insect meat) <=> [Meat, Fungus]
                // if sketchy ingredients (fungus, insect meat, etc) are in the ingredients, ensure they appear
/*                if (!flavorDef.RequiredSketchyIngredients.Empty())
                {
                    if (flavorDef.RequiredSketchyIngredients.Intersect(sketchyIngredients).Count() != flavorDef.RequiredSketchyIngredients.Count())
                    {
                        Log.Message($"{flavorDef.ToStringSafe()} failed due to requiredSketchyIngredients [{flavorDef.RequiredSketchyIngredients.ToStringSafeEnumerable()}]");
                        return null;
                    }
                }*/


                // {Food, Vegetable, Rice} => {Rice, Vegetable, Food} {2, 1, 0} with [berries, mushrooms] => [0, -1, 1]
                List<int> matchedIndices = [.. Enumerable.Repeat(-1, flavorDef.Ingredients.Count())];
                List<(ThingDef def, int index)> availableIngredients = [.. ingredients.Select((ThingDef value, int i) => (value: value, i: i))];

                // when generating from 0 ingredients, ensure there's viable options for all slots
                if (ingredients.Count() == 0)
                {
                    if (flavorDef.Ingredients.Any((IngredientSlot slot) => slot.AllowedCategories.Where(cat => cat.ChildThingDefs.Any()).All(activeCat => activeCat.InListOrDescendantOf(excludedCategories))))
                    {
                        return null;
                    }
                }
                else
                {
                    // {grain, vegetable, fungus/meat} [corn, potato]
                    for (int s = 0; s < flavorDef.Ingredients.Count; s++)
                    {
                        IngredientSlot slot = flavorDef.Ingredients[s];
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
                            Log.Error($"{slot?.ToStringSafe()} {s} with categories [{slot?.Categories.ToStringSafeEnumerable()}] in {flavorDef?.ToStringSafe()} had an error when attempting to find a matching ingredient.\ningredients = [{ingredients.ToStringSafeEnumerable()}]\nmatchedIndices = [{matchedIndices.ToStringSafeEnumerable()}]\navailableIngredients = [{availableIngredients.Select(((ThingDef def, int index) item) => item.def).ToStringSafeEnumerable()}]");
                            throw;
                        }
                    }
                }

                if (matchedIndices.Any(index => index == -1)) return null;
                return matchedIndices;
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
        for (int i = 0; i < bestFlavors.Count; i++)
        {
            var flavorTuple = bestFlavors[i];
            if (flavorTuple.def == null || flavorTuple.index == null) throw new NullReferenceException($"A chosen FlavorDef with index of {i.ToStringSafe()} is null, cancelling the search. Please report.");

            finalFlavorDefs.Add(bestFlavors[i].def);
            List<ThingDef> ingredientChunk = [.. ingredientChunks[i]];

            string flavorLabel = FormatFlavorString(bestFlavors[i], ingredientChunk, bestFlavors[i].def.label); // make flavor labels look nicer for main label; replace placeholders in the flavor label with the corresponding ingredient from the meal
            if (flavorLabel.NullOrEmpty())
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"FormatFlavorString failed to get a formatted flavor label for ingredient group {i.ToStringSafe()} containing [{ingredientChunk.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                }
                throw new FormatException();
            }
            flavorLabels.Add(flavorLabel);
            string flavorDescription = FormatFlavorString(bestFlavors[i], ingredientChunk, bestFlavors[i].def.description);  // make flavor descriptions look nicer for main description; replace placeholders in the flavor description with the corresponding ingredient from the meal
            if (flavorDescription.NullOrEmpty())
            {
                if (Prefs.DevMode)
                {
                    Log.Error($"FormatFlavorString failed to get a formatted flavor description for ingredient group {i.ToStringSafe()} containing [{ingredientChunk.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                }
                throw new FormatException();
            }
            flavorDescriptions.Add(flavorDescription);
        }

        if (flavorLabels.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Labels for meal " + base.parent.ThingID.ToStringSafe() + " was empty. Please report.");
        }
        if (flavorDescriptions.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Descriptions for meal " + base.parent.ThingID.ToStringSafe() + " was empty. Please report.");
        }
        CompileFlavorLabels();
        CompileFlavorDescriptions();
        if (finalFlavorLabel.NullOrEmpty())
        {
            throw new NullReferenceException("The final compiled and formatted flavor label was null or empty despite getting valid Flavor Defs [" + finalFlavorDefs.ToStringSafeEnumerable() + "]. Please report.");
        }
    }

    // replace placeholders in flavor label/description with the correctly inflected ingredient label
    private string FormatFlavorString((FlavorDef def, List<int> index) flavorTuple, List<ThingDef> ingredients, string flavorString)
    {
        try
        {
            Rand.PushState(FlavorSeed);

            // find placeholders and replace them with the appropriate inflection of the right ingredient
            for (int i = 0; i < flavorTuple.def.Ingredients.Count; i++)
            {
                IngredientSlot slot = flavorTuple.def.Ingredients[i];
                int ingIndex = flavorTuple.index[i];
                int formattingIndex = flavorTuple.def.FormattingIndices[i];
                List<string> inflections = ingIndex != -1
                    ? InflectionUtility.ThingInflectionsDictionary[ingredients[ingIndex]]
                    : throw new ArgumentOutOfRangeException($"found a -1 index in {flavorTuple.def.defName.ToStringSafe()} with indices [{flavorTuple.index.ToStringSafeEnumerable()}] which should have been resolved by now");
                if (inflections.Count != 4)
                {
                    throw new ArgumentOutOfRangeException($"Error formatting string for {flavorTuple}. Should have {InflectionUtility.numInflections} inflections, but found {inflections.Count} inflections");
                }

                for (int j = 0; j < InflectionUtility.grammaticalInflections.Count; j++)
                {
                    string infName = InflectionUtility.grammaticalInflections[j];
                    NamedArgument argument = new("{" + formattingIndex + "_" + infName + "}", inflections[j]);

                    flavorString = RemoveRepeatedWords(flavorString, argument);

                    flavorString = flavorString.Replace(argument.arg.ToString(), argument.label);
                    //TODO: formatted here causes kéÿ łańġųaǵə
                    // String.Replace is about as fast as Regex.Replace
                }
            }
            Rand.PopState();
            return flavorString;
        }
        catch (Exception e)
        {
            throw new Exception($"Error when formatting flavor ({flavorString.ToStringSafe()}) for {flavorTuple.def.ToStringSafe()} with ingredients [{ingredients.ToStringSafeEnumerable()}] and indices [{flavorTuple.index.ToStringSafeEnumerable()}]: reason: {e}");
        }

        // remove words repeated directly after each other
        static string RemoveRepeatedWords(string flavorString, NamedArgument argument)
        {

            // capture the words before and after the placeholder
            // Match.Groups = [regex string, capture group 1, capture group 2]
            MatchCollection argumentsWithContext = Regex.Matches(flavorString, "([^ .,;:]*) *" + argument.arg.ToString() + " *([^ .,;:]*)");
            foreach (Match match in argumentsWithContext)
            {
                // return if a blank inflection, currently only used for the adjectival form of "flour"
                if (match == Match.Empty)
                {
                    continue;
                }
                if (match.Groups.Count != 3)
                {
                    throw new ArgumentOutOfRangeException("The number of groups from Regex.Match for was not 3.");
                }
                //some meat {0_coll} meat dog
                //(10, 17) (0, 9) (18, 26)

                //fried {0_coll}
                //(6, 13) (0, 5) (14, 14)
                //(0, 13) (0, 0) (13, 0)
                //(0, 13) (0, 0) (13, 0)


                List<string> flavorSubstrings = [flavorString.Substring(0, Math.Max(match.Groups[0].Index, 0)), flavorString.Substring(match.Groups[0].Index, match.Groups[0].Length), flavorString.Substring(Math.Min(match.Groups[0].Index + match.Groups[0].Length, flavorString.Length - 1), flavorString.Length - (match.Groups[0].Index + match.Groups[0].Length))];

                List<string> argumentSplit = [.. argument.label.Split(' ')];

                // if you captured a word before the placeholder, see if it duplicates the first word of "inflection"
                if (InflectionUtility.RemoveDiacritics(match.Groups[1].Value).ToLower() == InflectionUtility.RemoveDiacritics(argumentSplit.First()).ToLower())
                {
                    flavorSubstrings[1] = flavorSubstrings[1].Remove(0, match.Groups[1].Length);
                }

                // if you captured a word after the placeholder, see if it duplicates the last word of "inflection"
                if (InflectionUtility.RemoveDiacritics(match.Groups[2].Value).ToLower() == InflectionUtility.RemoveDiacritics(argumentSplit.Last()).ToLower())
                {
                    flavorSubstrings[1] = flavorSubstrings[1].Remove(flavorSubstrings[1].Length - match.Groups[2].Length, match.Groups[2].Length);
                }
                flavorString = string.Join("", flavorSubstrings);
            }
            return flavorString;

        }
    }

    internal ThingDef GenerateGhostIngredientsManuallyDebug((FlavorDef def, List<int> index) flavorTuple, List<ThingDef> ingredients, int slotIndex, IngredientSlot slot)
    {
        ThingDef ghost = null;
        List<ThingDef> ghostIngredients = [.. slot.AllowedThingDefs.Where(ing => !excludedCategories.Any(ecat => ecat.ContainedInThisOrDescendant(ing)))];
        List<FlavorCategoryDef> ghostCategories = [.. slot.Categories.SelectMany((FlavorCategoryDef cat) => cat.ThisAndDescendants.Where((FlavorCategoryDef childCat) => childCat.ChildThingDefs.Count > 0 && !childCat.InListOrDescendantOf(slot.DisallowedCategories) && !childCat.InListOrDescendantOf(excludedCategories)))];
        if (ghostCategories.Empty())  // if you'd fail to generate, recalculate diet from ingredients instead of meal
        {
            Log.Message($"Meal {parent.ThingID.ToStringSafe()} at {parent.PositionHeld.ToStringSafe()} with real ingredients [{ingredients.ToStringSafeEnumerable()}]. When generating ghost ingredients for {flavorTuple.def.ToStringSafe()}, slot {slotIndex} with categories [{slot.Categories.ToStringSafeEnumerable()}], the restrictions prevented any ghost ingredients from being generated. The ghost ingredients will now be regenerated with restrictions based on the ingredient categories from FlavorText instead of the vanilla meal FoodKinds.");
            excludedCategories = [.. Props.defaultGhostExcludedCategories];
            foreach (var dietCat in GetExcludedFlavorCategoriesFromDiet(CalculateIngredientDiet(ingredients)))
            {
                excludedCategories.AddDistinct(dietCat);
            }
            ghostCategories = [.. slot.Categories.SelectMany((FlavorCategoryDef cat) => cat.ThisAndDescendants.Where((FlavorCategoryDef childCat) => childCat.ChildThingDefs.Count > 0 && !childCat.InListOrDescendantOf(slot.DisallowedCategories) && !childCat.InListOrDescendantOf(excludedCategories)))];

        }
        if (!generatedCoreFlavorDef)
        {
            List<FlavorCategoryDef> coreCats = dietIncludedCategories[mealDiet];
            List<FlavorCategoryDef> coreGhostCategories = [.. ghostCategories.Where((FlavorCategoryDef ghostCat) => ghostCat.InListOrDescendantOf(coreCats))];
            if (!coreGhostCategories.Empty())
            {
                generatedCoreFlavorDef = true;
                ghostCategories = coreGhostCategories;
            }
        }

        if (slot.AllowedThingDefs.Count() == 0)
        {
            throw new InvalidOperationException($"No AllowedThingDefs found for slot {slotIndex} with categories [{slot.Categories.Select(cat => cat.defName).ToStringSafeEnumerable()}] in flavorDef {flavorTuple.def.defName.ToStringSafe()}");
        }
        // try to use a random ingredient that exists
        else
        {
            ThingDef[] eles = [.. from thing in ghostCategories.Where((FlavorCategoryDef cat) => cat.ChildThingDefs.Count > 0).SelectMany((FlavorCategoryDef cat) => cat.ChildThingDefs)
                                         where !ingredients.Contains(thing)
                                         select thing];
            if (eles.Count() > 0)
            {
                ghost = Rand.Element(eles);
            }
            // if no valid random ingredient, allow repetitions
            else
            {
                eles = [.. from thing in ghostCategories.Where((FlavorCategoryDef cat) => cat.ChildThingDefs.Count > 0).SelectMany((FlavorCategoryDef cat) => cat.ChildThingDefs)
                       select thing];
                if (eles.Count() > 0)
                {
                    ghost = Rand.Element(eles);
                }
                else
                {
                    throw new InvalidOperationException($"No AllowedThingDefs found for slot {slotIndex} with ghost categories [{ghostCategories.ToStringSafeEnumerable()}] in flavorDef {flavorTuple.def.defName.ToStringSafe()}");
                }
            }
        }

        return ghost ?? throw new NullReferenceException($"Failed to generate ghost ingredient for {flavorTuple.def.defName.ToStringSafe()}. Slot {slotIndex} with ghost categories [{ghostCategories.ToStringSafeEnumerable()}] in flavorDef {flavorTuple.def.defName.ToStringSafe()}");
    }

    private void TryAddExtraIngredients()
    {
        Log.Warning($"TryAddExtraIngredients for {parent.ThingID.ToStringSafe()} x{parent.stackCount.ToStringSafe()} spawned={parent.Spawned.ToStringSafe()} at {parent.PositionHeld.ToStringSafe()} with ingredients [{Ingredients.ToStringSafeEnumerable()}]");
        if (FlavorTextSettings.fillUpBlankMeals == false && FlavorTextSettings.ghostIngredientCap == 0) return;

        Rand.PushState(FlavorSeed);

        RecipeDef mealRecipe = null;
        List<RecipeDef> mealRecipes = GameComponentFlavorText.MealRecipeDatabase.TryGetValue(parent.def);
        if (!mealRecipes.NullOrEmpty())
        {
            int r = Rand.Range(0, mealRecipes.Count());
            mealRecipe = mealRecipes[r];
        }

        FillInRecipe(mealRecipe);
        AddGhostIngredients(mealRecipe);

        Rand.PopState();
    }
    
    //TODO: this stacks up when meals are repeatedly spawned
    // fill up blank meal with random ingredients matching its recipe
    private void FillInRecipe(RecipeDef mealRecipe)
    {
        if (FlavorTextSettings.fillUpBlankMeals == true && mealRecipe != null && Ingredients.Count() == 0)
        {
            foreach (var slot in mealRecipe.ingredients)
            {
                // don't use categories here b/c its a private field and too annoying to get into
                List<ThingDef> candidateIngredients = [.. slot.filter.AllowedThingDefs.Where(ing => !Ingredients.Contains(ing) && !excludedCategories.Any(ex => ex.ContainedInThisOrDescendant(ing)))];
                AddGhostIngredientSingle(candidateIngredients);
            }
        }

    }

    // add random extra ingredients if settings allow, 50% chance for each
    internal void AddGhostIngredients(RecipeDef recipe)
    {
        if (GeneratedGhostIngredients) return;
        else
        {
            if (Ingredients.Empty() || Ingredients.Count >= FlavorTextSettings.ghostIngredientCap)
            {
                GeneratedGhostIngredients = true;
                return;
            }
        }

        List<ThingDef> recipeAllowedDefs = [.. recipe.ingredients.SelectMany(slot => slot.filter.AllowedThingDefs)];
        recipeAllowedDefs.RemoveDuplicates();
        if (recipeAllowedDefs.Empty()) throw new NullReferenceException($"{recipe.ToStringSafe()} had no allowedThingDefs.");

        List<bool> ghostBools = [.. Enumerable.Repeat(false, FlavorTextSettings.ghostIngredientCap - Ingredients.Count).Select(e => Rand.Bool)];
        Log.Message($"ghostBools were [{ghostBools.ToStringSafeEnumerable()}]");


        List<List<ThingDef>> ghostCategories = [.. GetIncludedFlavorCategoriesFromDiet(mealDiet)
            .Where(cat => cat.DescendantThingDefs.Count > 0)
            .SelectMany(cat => cat.ThisAndDescendants)
            .Distinct()
            .Where(desc => desc.ChildThingDefs.Count > 0 && !desc.ThisAndAncestors.Any(excludedCategories.Contains))
            .Select(desc => desc.ChildThingDefs)
            .Select<List<ThingDef>, List<ThingDef>>(things => [.. things.Intersect(recipeAllowedDefs)])
            .Where(things => things.Any())];
        List<List<ThingDef>> ghostCategoriesSorted = [];
        try
        {
            ghostCategoriesSorted = [.. ghostCategories.OrderBy(c => Rand.Value)];  // sort in random order so you can iterate over it
        }
        catch
        {
            Log.Error($"error in TryAddGhostIngredients for meal {parent.ThingID.ToStringSafe()} with ghostCategories [{ghostCategories.ToStringSafeEnumerable()}] ingredients [{Ingredients.ToStringSafeEnumerable()}] at position {parent.PositionHeld}");
            throw;
        }

        Log.Message($"ghostCategoriesSorted: [{ghostCategoriesSorted.Select(cat => $"[{cat.ToStringSafeEnumerable()}]").ToStringSafeEnumerable()}]");

        // iterate over a randomly sorted list of categories
        // gives more variety by avoiding things like FT_MeatRaw dominating the list of ingredients
        List<ThingDef> ings;
        if (ghostBools.Any(boo => boo == true))
        {
            for (int i = 0; i < ghostBools.Count; i++)
            {
                int j = i % ghostCategoriesSorted.Count;  // wrap around ghostCategories
                if (ghostBools[i])
                {
                    ings = [.. ghostCategoriesSorted[j].Where(thing => !Ingredients.Contains(thing))];
                    Log.Message($"[{ings.ToStringSafeEnumerable()}] are possible extra ghost ingredients");
                    AddGhostIngredientSingle(ings);
                }
            }
        }
        GeneratedGhostIngredients = true;
        return;
    }

    // add a single random ingredient from the list
    private void AddGhostIngredientSingle(List<ThingDef> ings)
    {
        if (ings.Count() == 0) return;
        int r = Rand.Range(0, ings.Count());
        parent.TryGetComp<CompIngredients>().RegisterIngredient(ings[r]);
        Log.Message($"added {ings[r]}, FoodKind is now {FoodUtility.GetFoodKind(parent)}");
    }

    // compile the flavor labels into one long displayed flavor label
    private void CompileFlavorLabels()
    {
        if (!flavorLabels.NullOrEmpty())
        {
            // don't ask
            StringBuilder stringBuilder = new();
            if (MealTags.Contains("hairy"))
            {
                GrammarRequest request = default;
                request.Includes.Add(RulePackDef.Named("FT_Tags"));
                stringBuilder.Append(GrammarResolver.Resolve("hairy", request));
            }

            stringBuilder.Append(GenText.CapitalizeAsTitle(flavorLabels[0]));
            GrammarRequest requestLabel = default;
            requestLabel.Includes.Add(RulePackDef.Named("FT_SideDishLabels"));
            if (flavorLabels.Count > 1)
            {
                Rand.PushState(FlavorSeed);
                for (int j = 1; j < flavorLabels.Count; j++)
                {
                    stringBuilder = new(string.Format(GrammarResolver.Resolve("label", requestLabel), stringBuilder.ToString(), GenText.CapitalizeAsTitle(flavorLabels[j])));
                }
                Rand.PopState();
            }
            finalFlavorLabel = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
    }

    // compile the flavor labels into one long displayed flavor label
    private void CompileFlavorDescriptions()
    {
        try
        {
            if (flavorDescriptions.NullOrEmpty())
            {
                return;
            }
            Rand.PushState(Find.World.info.Seed + Iteration.Value);
            RulePackDef sideDishDescriptions = RulePackDef.Named("FT_SideDishDescriptions");  // connector phrases for when meal has multiple FinalFlavorDefs
            StringBuilder stringBuilder = new();
            {
                GrammarRequest request = default;
                request.Includes.Add(sideDishDescriptions);
                stringBuilder.Append(CleanUpDescription(string.Format(GrammarResolver.Resolve("maindish", request), flavorLabels[0], flavorDescriptions[0])));
                for (int j = 1; j < flavorDescriptions.Count; j++)
                {
                    // connector clause with side dish label
                    // place the current flavor label in its placeholder spot within the sentence
                    stringBuilder.AppendWithSeparator(CleanUpDescription(string.Format(GrammarResolver.Resolve("sidedish", request), flavorLabels[j], flavorDescriptions[j])), "\n\n");
                }
            }
            finalFlavorDescription = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
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

    public void ExposeData()
    {
        return;
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
