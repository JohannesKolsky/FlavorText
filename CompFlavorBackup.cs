

// FlavorText, Version=0.2.9527.817, Culture=neutral, PublicKeyToken=null
// FlavorText.CompFlavor
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using FlavorText;
using RimWorld;
using Verse;
using Verse.Grammar;

public class CompFlavor : ThingComp
{
    public class MeatComparer : IComparer<ThingDef>
    {
        public int Compare(ThingDef ing1, ThingDef ing2)
        {
            if (ing1 != null && ing1.thingCategories == null && ing2 != null && ing2.thingCategories == null)
            {
                return 0;
            }
            if (ing1 != null && ing1.thingCategories == null)
            {
                return -1;
            }
            if (ing2 != null && ing2.thingCategories == null)
            {
                return 1;
            }
            List<int> list = new List<int>(2);
            List<int> list2 = list;
            if (1 == 0)
            {
            }
            if (ing1 == null)
            {
                goto IL_0133;
            }
            int item;
            if (CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Blood")))
            {
                item = 0;
            }
            else if (CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Twisted")))
            {
                item = 1;
            }
            else if (CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Human")))
            {
                item = 3;
            }
            else if (CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_Meat_Insect")))
            {
                item = 6;
            }
            else
            {
                if (!CategoryUtility.ThingCategories[ing1].Contains(FlavorCategoryDef.Named("FT_MeatRaw")))
                {
                    goto IL_0133;
                }
                item = 9;
            }
            goto IL_0139;
        IL_0214:
            if (1 == 0)
            {
            }
            List<int> list3;
            int item2;
            list3.Add(item2);
            List<int> ranking = list;
            return ranking[1] - ranking[0];
        IL_0133:
            item = 12;
            goto IL_0139;
        IL_0139:
            if (1 == 0)
            {
            }
            list2.Add(item);
            list3 = list;
            if (1 == 0)
            {
            }
            if (ing2 == null)
            {
                goto IL_020e;
            }
            if (CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Blood")))
            {
                item2 = 0;
            }
            else if (CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Twisted")))
            {
                item2 = 1;
            }
            else if (CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Human")))
            {
                item2 = 3;
            }
            else if (CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_Meat_Insect")))
            {
                item2 = 6;
            }
            else
            {
                if (!CategoryUtility.ThingCategories[ing2].Contains(FlavorCategoryDef.Named("FT_MeatRaw")))
                {
                    goto IL_020e;
                }
                item2 = 9;
            }
            goto IL_0214;
        IL_020e:
            item2 = 12;
            goto IL_0214;
        }
    }

    private bool tag;

    private bool generatedCoreFlavorDef = true;

    public bool TriedFlavorText;

    private List<string> FlavorLabels = new List<string>();

    private string FinalFlavorLabel;

    private List<string> FlavorDescriptions = new List<string>();

    private string FinalFlavorDescription;

    private List<FlavorDef> FinalFlavorDefs = new List<FlavorDef>();

    public ThingDef CookingStation;

    public int? HourOfDay = null;

    public int? TickCreated = null;

    public int? CookID = null;

    public float? IngredientsHitPointPercentage;

    public List<string> MealTags = new List<string>();

    internal DietTuple mealDietKind;

    internal List<FlavorCategoryDef> excludedCategories;

    internal int? iteration = null;

    public List<ThingDef> Ingredients => (from def in base.parent.TryGetComp<CompIngredients>().ingredients.FindAll((ThingDef i) => i != null && FlavorCategoryDefOf.FT_Foods.ContainedInThisOrDescendant(i))
                                          orderby def.defName.GetHashCode()
                                          select def).ToList();

    public CompIngredients CompIngredients => base.parent.TryGetComp<CompIngredients>();

    public CompProperties_Flavor Props => (CompProperties_Flavor)base.props;

    public override string TransformLabel(string label)
    {
        this.TryGetFlavorText();
        if (base.parent.stackCount == 1 || FlavorTextSettings.flavorTextForStacks)
        {
            return (!this.FinalFlavorLabel.NullOrEmpty()) ? (this.FinalFlavorLabel + " (" + base.TransformLabel(label) + ")") : base.TransformLabel(label);
        }
        return base.TransformLabel(label);
    }

    public override string GetDescriptionPart()
    {
        return (!this.FinalFlavorDescription.NullOrEmpty()) ? this.FinalFlavorDescription : base.GetDescriptionPart();
    }

    public override string CompInspectStringExtra()
    {
        if (!this.FinalFlavorLabel.NullOrEmpty())
        {
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendLine((base.parent.stackCount != 1 && !FlavorTextSettings.flavorTextForStacks) ? this.FinalFlavorLabel : base.TransformLabel(base.parent.def.label));
            return stringBuilder.ToString().TrimEndNewlines();
        }
        return base.CompInspectStringExtra();
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Defs.Look(ref this.CookingStation, "cookingStation");
        Scribe_Values.Look(ref this.HourOfDay, "hourOfDay");
        Scribe_Values.Look(ref this.TickCreated, "tickCreated");
        Scribe_Values.Look(ref this.iteration, "iteration");
        Scribe_Collections.Look(ref this.MealTags, "tags", LookMode.Undefined);
        if (Scribe.mode == LoadSaveMode.PostLoadInit && this.MealTags == null)
        {
            this.MealTags = new List<string>();
        }
        try
        {
            Scribe_Collections.Look(ref this.FinalFlavorDefs, "flavorDefs", LookMode.Undefined);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (this.FinalFlavorDefs == null)
                {
                    this.FinalFlavorDefs = new List<FlavorDef>();
                }
                else if (this.FinalFlavorDefs.Any((FlavorDef def) => def == null || DefDatabase<FlavorDef>.GetNamedSilentFail(def.defName.ToString()) == null))
                {
                    this.FinalFlavorDefs = new List<FlavorDef>();
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
            this.TriedFlavorText = false;
            this.TryGetFlavorText(this.FinalFlavorDefs);
        }
    }

    public override void PostSplitOff(Thing piece)
    {
        try
        {
            base.PostSplitOff(piece);
            if (piece != base.parent)
            {
                CompFlavor otherCompFlavor = piece.TryGetComp<CompFlavor>();
                otherCompFlavor.TriedFlavorText = this.TriedFlavorText;
                otherCompFlavor.FinalFlavorDefs = this.FinalFlavorDefs;
                otherCompFlavor.FlavorLabels = this.FlavorLabels;
                otherCompFlavor.FlavorDescriptions = this.FlavorDescriptions;
                otherCompFlavor.FinalFlavorLabel = this.FinalFlavorLabel;
                otherCompFlavor.FinalFlavorDescription = this.FinalFlavorDescription;
                otherCompFlavor.CookingStation = this.CookingStation;
                otherCompFlavor.HourOfDay = this.HourOfDay;
                otherCompFlavor.TickCreated = this.TickCreated;
                otherCompFlavor.MealTags = this.MealTags;
                otherCompFlavor.iteration = this.iteration;
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
            if (!this.TriedFlavorText)
            {
                this.TryGetFlavorText();
            }
            CompFlavor otherFlavorComp = otherStack.TryGetComp<CompFlavor>();
            this.FlavorLabels = new List<string>();
            this.FinalFlavorLabel = null;
            this.FlavorDescriptions = new List<string>();
            this.FinalFlavorDescription = null;
            this.FinalFlavorDefs = new List<FlavorDef>();
            int valueOrDefault = this.TickCreated.GetValueOrDefault();
            if (!this.TickCreated.HasValue)
            {
                this.TickCreated = GenTicks.TicksAbs;
            }
            Rand.PushState(Find.World.info.Seed + this.iteration.Value);
            this.CookingStation = Rand.Element(this.CookingStation, otherFlavorComp.CookingStation);
            this.HourOfDay = Rand.Element(this.HourOfDay, otherFlavorComp.HourOfDay);
            this.TickCreated = Rand.Element(this.TickCreated, otherFlavorComp.TickCreated);
            this.iteration = Rand.Element(this.iteration, otherFlavorComp.iteration);
            try
            {
                List<string> mealTags = this.MealTags;
                List<string> mealTags2 = otherFlavorComp.MealTags;
                List<string> list = new List<string>(mealTags.Count + mealTags2.Count);
                list.AddRange(mealTags);
                list.AddRange(mealTags2);
                List<string> mergedTags = list;
                mergedTags.RemoveAll((string mealTag) => mergedTags.Count((string t) => t == mealTag) < 2 && Rand.Range(0, 10) == 0);
                this.MealTags = mergedTags.Distinct().ToList();
                using List<string>.Enumerator enumerator = otherFlavorComp.MealTags.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    GenCollection.AddDistinct(obj: enumerator.Current, list: this.MealTags);
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
            this.TriedFlavorText = false;
            this.TryGetFlavorText();
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
        if (this.TriedFlavorText)
        {
            return;
        }
        if (!this.iteration.HasValue)
        {
            CompFlavorUtility.Iterate();
            this.iteration = CompFlavorUtility.Iterations;
        }
        this.TriedFlavorText = true;
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        try
        {
            this.FlavorLabels = new List<string>();
            this.FinalFlavorLabel = null;
            this.FlavorDescriptions = new List<string>();
            this.FinalFlavorDescription = null;
            this.FinalFlavorDefs = new List<FlavorDef>();
            if (this.Ingredients != null && (!this.Ingredients.Empty() || FlavorTextSettings.numAllowedMissingIngredients != 0))
            {
                int valueOrDefault = this.TickCreated.GetValueOrDefault();
                if (!this.TickCreated.HasValue)
                {
                    this.TickCreated = GenTicks.TicksAbs;
                }
                Rand.PushState(Find.World.info.Seed + this.iteration.Value);
                Random r = new Random();
                valueOrDefault = this.HourOfDay.GetValueOrDefault();
                if (!this.HourOfDay.HasValue)
                {
                    this.HourOfDay = r.Next(0, 24);
                }
                if (this.CookingStation == null)
                {
                    List<ThingDef> allCookingStations = FlavorCategoryDef.Named("FT_CookingStations").DescendantThingDefs.Distinct().ToList();
                    this.CookingStation = allCookingStations[r.Next(allCookingStations.Count)];
                }
                Rand.PopState();
                this.GetFlavorText(flavorDefsToSearch);
            }
        }
        catch (Exception ex)
        {
            string flavorSummary = string.Concat(string.Concat($"Unable to find a matching FlavorDef for meal {base.parent.ThingID} at {base.parent.PositionHeld}. Please report." + $"\n{FlavorDef.ActiveFlavorDefs.Count()} FlavorDefs are loaded", $"\n{flavorDefsToSearch?.Count} FlavorDefs were passed into TryGetFlavorDef from a saved game to search within"), $"\n{FlavorDef.ValidFlavorDefs(base.parent).ToList().Count} FlavorDefs match the meal type");
            for (int i = 0; i < this.Ingredients.Count; i++)
            {
                flavorSummary = string.Concat(flavorSummary + string.Format(arg1: this.Ingredients[i].defName, format: "\ningredient {0} was {1}", arg0: i), "\ningredient was in the following Flavor Categories");
                foreach (FlavorCategoryDef item in CategoryUtility.ThingCategories[this.Ingredients[i]])
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

    private void GetFlavorText(List<FlavorDef> flavorDefsToSearch)
    {
        if (FlavorTextSettings.numAllowedMissingIngredients > 0)
        {
            this.excludedCategories = this.Props.defaultGhostExcludedCategories.ToList();
            CompIngredients compIngredients = base.parent.TryGetComp<CompIngredients>();
            try
            {
                if (FoodUtility.GetFoodKind(base.parent) == FoodKind.Meat)
                {
                    if (compIngredients.Props.noIngredientsFoodKind == FoodKind.Meat)
                    {
                        this.mealDietKind = DietKind.carnivore;
                    }
                    else
                    {
                        this.mealDietKind = DietKind.omnivore;
                    }
                }
                else if (FoodUtility.GetFoodKind(base.parent) == FoodKind.NonMeat)
                {
                    this.mealDietKind = DietKind.vegan;
                }
                else
                {
                    if (FoodUtility.GetFoodKind(base.parent) != FoodKind.Any)
                    {
                        throw new ArgumentException("Error when getting food kind for meal. FoodKind: " + FoodUtility.GetFoodKind(base.parent).ToStringSafe());
                    }
                    this.mealDietKind = DietKind.vegetarian;
                }
                using (IEnumerator<FlavorCategoryDef> enumerator = DietKind.GetFlavorCategoriesFromDiet(this.mealDietKind).GetEnumerator())
                {
                    while (enumerator.MoveNext())
                    {
                        GenCollection.AddDistinct(obj: enumerator.Current, list: this.excludedCategories);
                    }
                }
                this.generatedCoreFlavorDef = this.Ingredients.Count() > 0;
            }
            catch (Exception)
            {
                throw;
            }
        }
        List<List<ThingDef>> ingredientChunks = new List<List<ThingDef>>(1)
        {
            new List<ThingDef>()
        };
        if (this.Ingredients.Count() > 0)
        {
            ingredientChunks = (from chunk in CompFlavor.Chunk(this.Ingredients)
                                select chunk.OrderByDescending((ThingDef m) => m, new MeatComparer()).ToList()).ToList();
        }
        List<(FlavorDef def, List<int> index)> bestFlavors = new List<(FlavorDef, List<int>)>();
        if (!flavorDefsToSearch.NullOrEmpty())
        {
            try
            {
                flavorDefsToSearch = FlavorDef.ValidFlavorDefs(base.parent, flavorDefsToSearch).ToList();
                if (!flavorDefsToSearch.Empty())
                {
                    bestFlavors = ((!ingredientChunks.Empty()) ? ingredientChunks.Select((List<ThingDef> ingredientChunk) => this.GetBestFlavorDef(ingredientChunk, flavorDefsToSearch)).ToList() : new List<(FlavorDef, List<int>)>(1) { this.GetBestFlavorDef(new List<ThingDef>(), flavorDefsToSearch) });
                }
            }
            catch (Exception ex2) when (ex2 is NullReferenceException || ex2 is InvalidOperationException)
            {
                if (Prefs.DevMode)
                {
                    Log.Warning("Saved Flavor Text no longer matches for a meal, it is probably from an older version of FlavorText. Will attempt to get new Flavor Text.");
                }
                bestFlavors = new List<(FlavorDef, List<int>)>();
            }
        }
        if (bestFlavors.Empty())
        {
            List<FlavorDef> validFlavorDefsForMealType = FlavorDef.ValidFlavorDefs(base.parent).ToList();
            if (validFlavorDefsForMealType.NullOrEmpty())
            {
                throw new InvalidOperationException("Attempted to get list of all valid Flavor Defs for meal type '" + base.parent.def.defName + "' in [" + CategoryUtility.ThingCategories.TryGetValue(base.parent.def).ToStringSafeEnumerable() + "] but there were none. Please report.");
            }
            bestFlavors = ((!ingredientChunks.Empty()) ? ingredientChunks.Select((List<ThingDef> ingredientChunk) => this.GetBestFlavorDef(ingredientChunk, validFlavorDefsForMealType)).ToList() : new List<(FlavorDef, List<int>)>(1) { this.GetBestFlavorDef(new List<ThingDef>(), validFlavorDefsForMealType) });
            if (bestFlavors.Empty())
            {
                throw new InvalidOperationException("Could not find any best Flavor Defs for meal " + base.parent.ThingID);
            }
        }
        for (int i = 0; i < bestFlavors.Count; i++)
        {
            if (bestFlavors?[i].def != null && bestFlavors[i].index != null)
            {
                this.FinalFlavorDefs.Add(bestFlavors[i].def);
                (FlavorDef, List<int>) flavor = bestFlavors[i];
                List<ThingDef> ingredientGroup = ingredientChunks[i];
                string flavorLabel = this.FormatFlavorString(bestFlavors[i], ingredientGroup, bestFlavors[i].def.label);
                if (flavorLabel.NullOrEmpty())
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"FormatFlavorString failed to get a formatted flavor label for ingredient group {i} containing [{ingredientGroup.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                    }
                    throw new FormatException();
                }
                this.FlavorLabels.Add(flavorLabel);
                string flavorDescription = this.FormatFlavorString(bestFlavors[i], ingredientGroup, bestFlavors[i].def.description);
                if (flavorDescription.NullOrEmpty())
                {
                    if (Prefs.DevMode)
                    {
                        Log.Error($"FormatFlavorString failed to get a formatted flavor description for ingredient group {i} containing [{ingredientGroup.ToStringSafeEnumerable()}], cancelling the search. Please report.");
                    }
                    throw new FormatException();
                }
                this.FlavorDescriptions.Add(flavorDescription);
                continue;
            }
            throw new NullReferenceException($"A chosen FlavorDef with index of {i} is null, cancelling the search. Please report.");
        }
        if (this.FlavorLabels.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Labels for meal " + base.parent.ThingID + " was empty. Please report.");
        }
        if (this.FlavorDescriptions.Empty())
        {
            throw new InvalidOperationException("The list of Flavor Descriptions for meal " + base.parent.ThingID + " was empty. Please report.");
        }
        this.CompileFlavorLabels();
        this.CompileFlavorDescriptions();
        if (this.FinalFlavorLabel.NullOrEmpty())
        {
            throw new NullReferenceException("The final compiled and formatted flavor label was null or empty despite getting valid Flavor Defs [" + this.FinalFlavorDefs.ToStringSafeEnumerable() + "]. Please report.");
        }
    }

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
            List<(FlavorDef def, List<int> indices)> matchingFlavors = new List<(FlavorDef, List<int>)>();
            foreach (FlavorDef flavorDef in flavorDefsToSearch)
            {
                List<int> matchedIndices = GetMatchIndices(ingredients, flavorDef);
                if (!matchedIndices.NullOrEmpty())
                {
                    matchingFlavors.Add((flavorDef, matchedIndices));
                }
            }
            if (matchingFlavors.Count > 0)
            {
                matchingFlavors = matchingFlavors.OrderByDescending(((FlavorDef def, List<int> indices) entry) => entry.def.specificity).ToList();
                (FlavorDef def, List<int> indices) bestFlavor;
                if (FlavorTextSettings.randomizedRecipeOuput)
                {
                    Rand.PushState(Find.World.info.Seed + this.iteration.Value);
                    bestFlavor = matchingFlavors.RandomElementByWeight(((FlavorDef def, List<int> indices) matchingFlavor) => matchingFlavor.def.specificity);
                    Rand.PopState();
                }
                else
                {
                    bestFlavor = matchingFlavors.First();
                }
                if (bestFlavor.def != null && bestFlavor.indices != null)
                {
                    return bestFlavor;
                }
                throw new NullReferenceException("Failed to find a matching Flavor Def. The best Flavor Def [" + bestFlavor.def.ToStringSafe() + "] or its list of indices [" + bestFlavor.indices.ToStringSafeEnumerable() + "] was null.");
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
        List<int> GetMatchIndices(List<ThingDef> ingredients, FlavorDef flavorDef)
        {
            try
            {
                if (flavorDef == null)
                {
                    if (Prefs.DevMode)
                    {
                        Log.Warning("Found a null FlavorDef in list of FinalFlavorDefs to search for a meal. Probably deprecated from an older version of FlavorText. Skipping...");
                    }
                    return null;
                }
                if (ingredients.Count > flavorDef.ingredients.Count || flavorDef.ingredients.Count > ingredients.Count + FlavorTextSettings.numAllowedMissingIngredients)
                {
                    return null;
                }
                if (flavorDef.allowedDiets.Empty())
                {
                    if (ingredients.Empty())
                    {
                        return null;
                    }
                }
                else if (!flavorDef.allowedDiets.Contains(this.mealDietKind))
                {
                    return null;
                }
                List<int> matchedIndices = Enumerable.Repeat(-1, flavorDef.ingredients.Count()).ToList();
                List<(ThingDef def, int index)> availableIngredients = ingredients.Select((ThingDef value, int i) => (value: value, i: i)).ToList();
                if (ingredients.Count() == 0)
                {
                    if (flavorDef.ingredients.Any((IngredientSlot slot) => slot.categories.All((FlavorCategoryDef cat) => cat.ThisAndParents.Intersect(this.excludedCategories).Count() > 0)))
                    {
                        return null;
                    }
                }
                else
                {
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

    private string FormatFlavorString((FlavorDef def, List<int> index) flavorTuple, List<ThingDef> ingredients, string flavorString)
    {
        try
        {
            Rand.PushState(Find.World.info.Seed + this.iteration.Value);
            int n = 0;
            List<NamedArgument> placeholderList = new List<NamedArgument>();
            List<ThingDef> ghostIngredients = new List<ThingDef>();
            Log.Warning("Found [" + ingredients.ToStringSafeEnumerable() + "] in meal with FlavorDef " + flavorTuple.def.defName.ToStringSafe() + "] and indices [" + flavorTuple.index.ToStringSafeEnumerable() + "]");
            for (int i = 0; i < flavorTuple.def.ingredients.Count; i++)
            {
                IngredientSlot slot = flavorTuple.def.ingredients[i];
                int ingIndex = flavorTuple.index[i];
                int formattingIndex = flavorTuple.def.formattingIndices[i];
                List<string> inflections = new List<string>();
                if (ingIndex == -1)
                {
                    List<FlavorCategoryDef> ghostCategories = slot.categories.SelectMany((FlavorCategoryDef cat) => cat.ThisAndChildren.Where((FlavorCategoryDef childCat) => !childCat.inflectionsOverride.NullOrEmpty() && childCat.ThisAndParents.Intersect(this.excludedCategories).Count() == 0)).ToList();
                    if (ghostCategories.NullOrEmpty())
                    {
                        Log.Error($"Error when generating ghost ingredients for {flavorTuple.def.ToStringSafe()}, slot {i} with categories [{slot.categories.ToStringSafeEnumerable()}]. The restrictions [{this.excludedCategories.ToStringSafeEnumerable()}] prevented any ghost ingredients from being generated.");
                        throw new NullReferenceException();
                    }
                    if (!this.generatedCoreFlavorDef)
                    {
                        IEnumerable<FlavorCategoryDef> coreCats = DietKind.GetFlavorCategoriesFromDiet(this.mealDietKind);
                        List<FlavorCategoryDef> coreGhostCategories = ghostCategories.Where((FlavorCategoryDef ghostCat) => ghostCat.ThisAndParents.Intersect(coreCats).Count() > 0).ToList();
                        if (!coreGhostCategories.Empty())
                        {
                            this.generatedCoreFlavorDef = true;
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
                while (true)
                {
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
        static string RemoveRepeatedWords(string inflection, Match placeholderWithContext)
        {
            if (inflection == "")
            {
                return inflection;
            }
            List<string> inflectionSplit = inflection.Split(' ').ToList();
            if (placeholderWithContext.Groups.Count != 3)
            {
                throw new ArgumentOutOfRangeException("The number of capture groups from Regex.Match for " + inflection + " was not 3.");
            }
            if (Remove.RemoveDiacritics(placeholderWithContext.Groups[1].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit.First()).ToLower())
            {
                inflectionSplit.RemoveAt(0);
            }
            if (Remove.RemoveDiacritics(placeholderWithContext.Groups[2].Value).ToLower() == Remove.RemoveDiacritics(inflectionSplit.Last()).ToLower())
            {
                inflectionSplit.RemoveLast();
            }
            inflection = string.Join(" ", inflectionSplit);
            return inflection;
        }
    }

    private void CompileFlavorLabels()
    {
        if (!this.FlavorLabels.NullOrEmpty())
        {
            StringBuilder stringBuilder = new StringBuilder();
            if (this.MealTags.Contains("hairy"))
            {
                GrammarRequest request = default(GrammarRequest);
                request.Includes.Add(RulePackDef.Named("FT_Tags"));
                stringBuilder.Append(GrammarResolver.Resolve("hairy", request));
            }
            for (int j = 0; j < this.FlavorLabels.Count; j++)
            {
                stringBuilder.AppendWithSeparator(j switch
                {
                    1 => "with ",
                    0 => "",
                    _ => "and ",
                } + GenText.CapitalizeAsTitle(this.FlavorLabels[j]), " ");
            }
            this.FinalFlavorLabel = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
        }
    }

    private void CompileFlavorDescriptions()
    {
        try
        {
            if (this.FlavorDescriptions.NullOrEmpty())
            {
                return;
            }
            Rand.PushState(Find.World.info.Seed + this.iteration.Value);
            RulePackDef sideDishClauses = RulePackDef.Named("FT_SideDishClauses");
            StringBuilder stringBuilder = new StringBuilder();
            for (int j = 0; j < this.FlavorDescriptions.Count; j++)
            {
                if (j == 0)
                {
                    stringBuilder.Append(CompFlavor.CleanUpDescription(this.FlavorDescriptions[j]));
                }
                if (j > 0)
                {
                    GrammarRequest request = default(GrammarRequest);
                    request.Includes.Add(sideDishClauses);
                    stringBuilder.AppendWithSeparator(CompFlavor.CleanUpDescription(string.Format(GrammarResolver.Resolve("sidedish", request), this.FlavorLabels[j], this.FlavorDescriptions[j])), "\n\n");
                }
            }
            this.FinalFlavorDescription = Find.ActiveLanguageWorker.PostProcessed(stringBuilder.ToString().TrimEndNewlines());
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
}
