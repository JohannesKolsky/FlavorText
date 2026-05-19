using PipeSystem;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using System.Reflection;
using static FlavorText.CategoryUtility;
using static FlavorText.DietKind;

//--TODO: recipe parent hierarchy
//DONE: spreadsheet descriptions are misaligned
//--TODO: blank ingredient option
//DONE: resolve question of how to deal with twisted/vegetarian/etc meals: they are separate categorizatons so they should be separate fields in each FlavorDef; but what about stuff like [FT_Meat_Twisted, FT_Fungus] vs [FT_Meat_Twisted/FT_Fungus]?
//DONE: candy has meat FoodKind allowed
//DONE: single condiments fail in bakes; is this b/c of a diet issue or a mealKind issue?
//DONE: more general categories like MealsCooked for mealKinds, so you don't have to list all of them for stuff like Mud Cookies or condiment creations
//--TODO: use default disallowed ingredients for SimpleMeal to exclude human meat, insect meat, etc from meals; -- picks up Insect Jelly
//DONE: how are condiments and diet handled in SetDiet? seems like it won't assign a diet to a condiment-only FlavorDef
//DONE: with full modded list, paste FlavorDefs are appearing for simple meals
//DONE: stuff like FT_SugarCandy has allowedDietKind hypercarnivore and carnivore
//DONE: Fried_Foods doesn't have Diet.omnivore

//TODO: soylent green appears 99% of the time for cannibal paste, because 1 vs 50^2 specificity

namespace FlavorText;

internal class DietKind
{
    // basic diet types based on possible ingredients
    // this is exclusive: omnivore requires a plant ingredient, vegetarian requires an animal ingredient
    // this order is strict, because a subrange of this can be used in searches
	internal enum Diet { hyperCarnivore, carnivore, omnivore, vegetarian, vegan, animalProduct, animalFree}

	internal static readonly Dictionary<Diet, List<FlavorCategoryDef>> dietExcludedCategories = new()
    {
     {Diet.hyperCarnivore, [FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw] },
     {Diet.carnivore, [FlavorCategoryDefOf.FT_PlantFoodRaw]},
     {Diet.omnivore, []},
     {Diet.vegetarian, [FlavorCategoryDefOf.FT_MeatRaw]},
     {Diet.animalProduct, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_PlantFoodRaw] },
     {Diet.vegan, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw]},
     {Diet.animalFree, [FlavorCategoryDefOf.FT_AnimalProductRaw] }
    };

	internal static readonly Dictionary<Diet, List<FlavorCategoryDef>> dietIncludedCategories = new()
    {
     {Diet.hyperCarnivore, [FlavorCategoryDefOf.FT_MeatRaw]},
     {Diet.carnivore, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw]},
     {Diet.vegetarian, [FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw] },
     {Diet.vegan, [FlavorCategoryDefOf.FT_PlantFoodRaw]},
     {Diet.omnivore, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw] },
     {Diet.animalProduct, [FlavorCategoryDefOf.FT_AnimalProductRaw] },
     {Diet.animalFree, [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_PlantFoodRaw] }
    };

	internal static readonly List<FlavorCategoryDef> NormalDietCategories = [FlavorCategoryDefOf.FT_MeatRaw, FlavorCategoryDefOf.FT_AnimalProductRaw, FlavorCategoryDefOf.FT_PlantFoodRaw];

	internal static readonly List<FlavorCategoryDef> SketchyDietCategories = [FlavorCategoryDefOf.FT_Fungus, FlavorCategoryDefOf.FT_Meat_Human, FlavorCategoryDefOf.FT_Meat_Insect, FlavorCategoryDefOf.FT_Meat_Twisted];

	internal static IEnumerable<FlavorCategoryDef> GetExcludedFlavorCategoriesFromDiet(Diet dietTuple)
    {
        return dietExcludedCategories[dietTuple];
    }
    internal static IEnumerable<FlavorCategoryDef> GetIncludedFlavorCategoriesFromDiet(Diet dietTuple)
    {
        return dietIncludedCategories[dietTuple];
    }
}

/// <summary>
///     Effectively recipes
///     show what combination of ingredients/categories are needed for each particular flavor label
/// </summary>
/// 
public class FlavorDef : Def
{
    private static bool tag;  // debug tag

    private List<int> formattingIndices = [];  // this tells you which ingredient slot matches with which placeholder index for formatting the flavor label and description; this is needed because the ingredient slots are reordered according to specificity during game load

    private float specificity;  // how specific is this FlavorDef: how many ingredient choices are there, does it need to be a certain meal type, etc?

    internal List<Diet> allowedDiets = []; // what types of food are generally allowed by this FlavorDef (vegan, vegetarian, carn)

    private List<FlavorCategoryDef> requiredSketchyIngredients = []; // whether the FlavorDef requires something like fungus or insect meat

    public List<FlavorCategoryDef> mealKinds = [];  // what types of meals are allowed to have this FlavorDef; empty means all

    public List<FlavorCategoryDef> mealQualities = [];

    private List<FlavorCategoryDef> cookingStations = [];  // which buildings are allowed to cook this FlavorDef; empty means all

    private IntRange hoursOfDay = new(0, 23);  // what hours of the day this FlavorDef can be completed during, defaults to all day (0-23)

    private FloatRange ingredientsHitPointPercentage = new(0, 1); // allowed range of percentage of hit points of each ingredient group (ignoring quantity in group), defaults to all (0-1)

    // all FlavorDefs that can be used with the current modlist
    private static IEnumerable<FlavorDef> activeFlavorDefs;
	internal static IEnumerable<FlavorDef> ActiveFlavorDefs => activeFlavorDefs ??= DefDatabase<FlavorDef>.AllDefs
                    .Where(flavorDef => flavorDef != null)
                        .Where(flavorDef => flavorDef.Ingredients
                            .All(ingredientSlot => ingredientSlot.AllowedThingDefs.Any()));

    public string varietyTexture;
    public string VarietyTexture { get {return varietyTexture; } }

    public List<IngredientSlot> ingredients = [];
    internal List<IngredientSlot> Ingredients { get => ingredients; set => ingredients = value; }
    internal List<int> FormattingIndices { get => formattingIndices; set => formattingIndices = value; }
    internal float Specificity { get => specificity; set => specificity = value; }
    internal List<FlavorCategoryDef> RequiredSketchyIngredients { get => requiredSketchyIngredients; set => requiredSketchyIngredients = value; }
    internal List<FlavorCategoryDef> MealKinds { get => mealKinds; set => mealKinds = value; }
    internal List<FlavorCategoryDef> MealQualities { get => mealQualities; set => mealQualities = value; }
    internal List<FlavorCategoryDef> CookingStations { get => cookingStations; set => cookingStations = value; }
    internal IntRange HoursOfDay { get => hoursOfDay; set => hoursOfDay = value; }
    internal FloatRange IngredientsHitPointPercentage { get => ingredientsHitPointPercentage; set => ingredientsHitPointPercentage = value; }

    internal static readonly List<FlavorCategoryDef> activeMealKinds = [];


    internal static readonly Dictionary<Diet, List<FlavorDef>> DietIndex = [];

    internal static void SetStaticData()
    {
        try
        {
            RemoveUnusedSlotCategories();
            SetAllowedIngredients();
            SetActiveMealKinds();
            SetDiets();
            MakeDietIndex();
            SetSpecificities();
            SortSlots();
        }
        catch (Exception ex)
        {
            Log.Error($"Error when setting static data for Flavor Defs, error: {ex}");
        }
    }

    // remove categories that contain no descendant ThingDefs
    private static void RemoveUnusedSlotCategories()
    {
        foreach (var flavorDef in DefDatabase<FlavorDef>.AllDefs)
        {
            foreach (var slot in flavorDef.Ingredients)
            {
                List<FlavorCategoryDef> categoriesCopy = [.. slot.Categories];
                foreach (FlavorCategoryDef cat in categoriesCopy)
                {
                    if (cat.Parents.Empty() && cat.ChildCategories.Empty())
                    {
                        slot.Categories.Remove(cat);
                    }
                }

                List<FlavorCategoryDef> disallowedCategoriesCopy = [.. slot.DisallowedCategories];
                foreach (FlavorCategoryDef cat in disallowedCategoriesCopy)
                {
                    if (cat.Parents.Empty() && cat.ChildCategories.Empty())
                    {
                        slot.DisallowedCategories.Remove(cat);
                    }
                }
            }
        }
    }
    // register all allowed Defs for each ingredient slot in each Flavor Def
    private static void SetAllowedIngredients()
    {
        foreach (var flavorDef in DefDatabase<FlavorDef>.AllDefs)
        {
            foreach (var slot in flavorDef.Ingredients)
            {
                slot.AddAllowedCategoriesAndThingsRecursive(slot.Categories);
            }
        }
    }
    // remove mealKind categories that aren't being used (e.g. FT_MealsSoup if no mods add soup meals)
    // if this means a FlavorDef has no mealKind, add FT_MealsNonSpecial to it, so it can be used by normal meals and survival pack meals
    private static void SetActiveMealKinds()
    {
        List<FlavorCategoryDef> emptyMealKinds = [.. FlavorCategoryDefOf.FT_MealsKinds.ThisAndDescendants.Where(cat => !cat.DescendantThingDefs.Any())];
        foreach (var flavorDef in ActiveFlavorDefs)
        {
            // get lowest child categories of all active mealKinds in the def
            List<FlavorCategoryDef> defActiveMealKinds = [.. flavorDef.MealKinds.Except(emptyMealKinds)];


            if ((defActiveMealKinds.Empty() || FlavorTextSettings.laxRecipeMatching)
                && flavorDef.MealKinds.Any(FlavorCategoryDefOf.FT_MealsCooked.ContainedInThisOrDescendant))
            {
                flavorDef.MealKinds.Clear();
                flavorDef.MealKinds.AddRange(FlavorCategoryDefOf.FT_MealsNonSpecial.LowestChildCategories.Where(child => child.DescendantThingDefs.Any()));
            }
            else flavorDef.MealKinds.Clear();

            foreach (var kind in defActiveMealKinds)
            {
                foreach (var child in kind.LowestChildCategories)
                {
                    if (child.DescendantThingDefs.Any()) flavorDef.MealKinds.AddDistinct(child);
                }
            }

            flavorDef.MealKinds.ForEach(mealKind => activeMealKinds.AddDistinct(mealKind));
            //Log.Message($"{flavorDef.ToStringSafe()} had mealKinds [{flavorDef.MealKinds.ToStringSafeEnumerable()}] and defActiveMealKinds [{defActiveMealKinds.ToStringSafeEnumerable()}]");

        }
    }


    private static void SetDiets()
    {
        foreach (var flavorDef in ActiveFlavorDefs)
        {
            // calculate if the FlavorDef could match a meat/vegan/vegetarian meal (can have multiple)
            if (flavorDef.Ingredients.Empty()) continue;
            List<List<FlavorCategoryDef>> slotAllowedCategories = [.. Enumerable.Repeat(new List<FlavorCategoryDef>(), flavorDef.Ingredients.Count())];
            List<List<FlavorCategoryDef>> slotSketchyCategories = [.. Enumerable.Repeat(new List<FlavorCategoryDef>(), flavorDef.Ingredients.Count())];

            for (int i = 0; i < flavorDef.Ingredients.Count; i++)
            {
                IngredientSlot slot = flavorDef.Ingredients[i];
                foreach (var cat in slot.Categories)
                {
                    // Meat, Animal, Plant, FoodRaw, Foods
                    // [Tomato, Meat, Foods, Chocolate]

                    // [Foods <Meat>]

                    if (!FlavorCategoryDefOf.FT_Ingredients.ContainedInThisOrDescendant(cat)) { Log.Error($"{cat.ToStringSafe()} had ancestors [{cat.ThisAndAncestors.ToStringSafeEnumerable()}]"); throw new ArgumentOutOfRangeException($"{cat.ToStringSafe()} was used for slot #{i.ToStringSafe()} of {flavorDef.ToStringSafe()}, but it is not a category under FT_Ingredients. Terminating FlavorDef setup.");}

                    // figure out whether cat belongs under plant/animal/meat
                    bool categorized = false;
                    foreach (FlavorCategoryDef dietCat in NormalDietCategories)
                    {
                        if (dietCat.ContainedInThisOrDescendant(cat) || cat.ContainedInThisOrDescendant(dietCat))
                        {
                            categorized = true;
                            //TODO: when you use List.Add, this adds the dietCat to EACH sublist, why??
                            if (!slotAllowedCategories[i].Contains(dietCat)) slotAllowedCategories[i] = [.. slotAllowedCategories[i], dietCat];
                        }
                    }

                    // do sketchy categories like FT_Fungus, FT_Meat_Insect
                    foreach (FlavorCategoryDef sketchyCat in SketchyDietCategories)
                    {
                        if (sketchyCat.ContainedInThisOrDescendant(cat))
                        {
                            slotSketchyCategories[i] = [.. slotSketchyCategories[i], sketchyCat];
                        }
                    }

                    // if the category was not categorized, that implies it is FT_Condiment or something, which can be plant+animal+meat, so just add them all and you're done
                    if (categorized == false)
                    {
                        slotAllowedCategories[i] = NormalDietCategories;
                        break;
                    }
                }

                //TODO: FT_Condiment as a disallowed category will have no effect; can this cause any issues?
                // remove diet categories that are within a disallowed category
                if (slot.DisallowedCategories.Any())
                {
                    List<FlavorCategoryDef> slotDietCopy = [.. slotAllowedCategories[i]];
                    foreach (var disallowedCat in slot.DisallowedCategories)
                    {
                        foreach (var slotDietCat in slotAllowedCategories[i])
                        {
                            if (disallowedCat.ContainedInThisOrDescendant(slotDietCat))
                            {
                                slotDietCopy.Remove(slotDietCat);
                            }
                        }
                    }
                    slotAllowedCategories[i] = slotDietCopy;
                }
            }

            foreach (var sketchy in SketchyDietCategories)
            {
                if (slotSketchyCategories.Any(diet => diet.Contains(sketchy))) flavorDef.RequiredSketchyIngredients.Add(sketchy);
            }

            // TODO: [M, M, MAP] will currently appear as omnivore but is not; this won't break anything, but makes TryGetFlavorText() less efficient

            if (slotAllowedCategories.All(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw))) flavorDef.allowedDiets.Add(Diet.hyperCarnivore);
            if (slotAllowedCategories.All(diet => diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw))) flavorDef.allowedDiets.Add(Diet.animalProduct);
            if (slotAllowedCategories.All(diet => diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw))) flavorDef.allowedDiets.Add(Diet.vegan);


            // these don't mandate a certain # slots b/c these are effectively backup options for TryGetFlavorText() if only condiments/drinks are used as ingredients

            if (slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw)) && slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw)) && slotAllowedCategories.All(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw) || diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw))) flavorDef.allowedDiets.Add(Diet.carnivore);

            if (slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw)) && slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw)) && slotAllowedCategories.All(diet => diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw) || diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw))) flavorDef.allowedDiets.Add(Diet.vegetarian);

            if (slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw)) && slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw)) && slotAllowedCategories.All(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw) || diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw))) flavorDef.allowedDiets.Add(Diet.animalFree);

            if (slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_MeatRaw)) && slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_AnimalProductRaw)) && slotAllowedCategories.Any(diet => diet.Contains(FlavorCategoryDefOf.FT_PlantFoodRaw))) flavorDef.allowedDiets.Add(Diet.omnivore);

            //Log.Message($"{flavorDef.defName.ToStringSafe()} had allowedDietKinds [{flavorDef.allowedDiets.ToStringSafeEnumerable()}] and slotDiets [{slotAllowedCategories.Select(slot => $"[{slot.ToStringSafeEnumerable()}]").ToStringSafeEnumerable()}]. NormalDietCategories were [{NormalDietCategories.ToStringSafeEnumerable()}]. Had {slotAllowedCategories.Count} slots");


        }
    }
    // create an dictionary that groups meals according to diet for quick access
    private static void MakeDietIndex()
    {
        foreach (Diet diet in Enum.GetValues(typeof(Diet)))
        {
            DietIndex.Add(diet, []);
        }
        foreach (var flavorDef in ActiveFlavorDefs)
        {
            foreach (var diet in flavorDef.allowedDiets)
            {
                DietIndex[diet].Add(flavorDef);
            }
        }

        Log.Warning($"DietIndex: [{DietIndex.Select(entry => $"{entry.Key.ToStringSafe()} had {entry.Value.Count} entries\n").ToStringSafeEnumerable()}]");
    }
    private static void SetSpecificities()
    {

        // get all FlavorDefs, excluding those which have an ingredient slot which has no allowed ThingDefs (i.e. no ThingDefs in the current modlist fit that slot)
        int totalCookingStations = FlavorCategoryDefOf.FT_CookingStations.DescendantThingDefs.Count();
        int totalMealTypes = FlavorCategoryDefOf.FT_MealsWithCompFlavor.DescendantThingDefs.Count();

        foreach (FlavorDef flavorDef in ActiveFlavorDefs)
        {
            if (flavorDef.MealKinds.NullOrEmpty())
            {
                Log.Error($"The FlavorDef {flavorDef.defName} did not have any MealKinds, it will never appear in-game. Please report.");
            }

            float restrictions = flavorDef.Ingredients.Sum(ing => Mathf.Sqrt(ing.AllowedThingDefs.Count()));  //sqrt to reduce impact of high ingredient counts

            // more specific if it has a required meal type, weighted to half-impact
            restrictions = restrictions * ((flavorDef.MealKinds.Sum(mealCategory => (float)mealCategory.DescendantThingDefs.Count()) / totalMealTypes) + 1) / 2;

            // more specific if it has a required cooking station, weighted to half-impact
            if (!flavorDef.CookingStations.NullOrEmpty())
            {
                restrictions = ((restrictions * flavorDef.CookingStations.Sum(station => (float)station.DescendantThingDefs.Count()) / totalCookingStations) + 1) / 2;
            }
            // more specific if it has a required cooking time of day, weighted to half-impact
            if (flavorDef.HoursOfDay != new IntRange(0, 23))
            {
                int timeLength = flavorDef.HoursOfDay.max - flavorDef.HoursOfDay.min;
                timeLength = (timeLength % 24) + 1;
                restrictions *= (((float)timeLength / 24) + 1) / 2;
            }

            if (flavorDef.IngredientsHitPointPercentage != new FloatRange(0, 1))
            {
                restrictions *= flavorDef.IngredientsHitPointPercentage.Span;
            }

            // higher restrictions: more broad (more ingredients, more cooking stations, etc)
            // higher specificity: more narrow
            // ingredients # is already sqrted by this stage
            if (restrictions > 0) flavorDef.Specificity = 10000 / restrictions;


            // get each category and its parents
            List<FlavorCategoryDef> allCategoriesInDef = [.. flavorDef.Ingredients
                    .SelectMany(slot => slot.Categories)
                    .Distinct()];

        }
    }


    // sort the ingredient slots according to specificity, which is necessary for ingredient matching
    private static void SortSlots()
    {
        foreach (var flavorDef in ActiveFlavorDefs)
        {
            IEnumerable<(IngredientSlot value, int index)> slotsSorted = flavorDef.Ingredients.Select((value, index) => (value, index))
                .OrderBy(item => item.value.AllowedThingDefs.Count());
            flavorDef.Ingredients = [.. slotsSorted.Select(slot => slot.value)];
            flavorDef.FormattingIndices = [.. slotsSorted.Select(slot => slot.index)];
        }
    }

	// all FinalFlavorDefs that fit the given meal type, quality, and extra parameters
	// can be restricted to a given list of flavorDefsToSearch, which is used when loading a saved meal that already has flavor text to reduce search time
	internal static IEnumerable<FlavorDef> ValidFlavorDefs(ThingWithComps meal, Diet diet, IEnumerable<FlavorDef> flavorDefsToSearch = null)
    {
        var compFlavor = meal.TryGetComp<CompFlavor>();

        // check if meal search can be expanded because of laxRecipeMatching
        // meal = MealSimple
        // ThingParentCategories = [FT_MealsNormal]
        // mealThingParentCategories = [FT_MealsNonSpecial]
        // flavorDef.mealKinds = [FT_MealsNonSpecial, FT_MealsSandwich]
        List<FlavorCategoryDef> mealThingParentCategories = [];
        foreach (var cat in ThingParentCategories[meal.def])
        {
            var temp = cat.ThisAndAncestors.FirstOrDefault(activeMealKinds.Contains);
            if (temp is not null) mealThingParentCategories.Add(temp);
        }
        bool mealCanBeAnyKind = FlavorTextSettings.laxRecipeMatching && mealThingParentCategories.Any(FlavorCategoryDefOf.FT_MealsNonSpecial.ThisAndDescendants.Contains);

        if (flavorDefsToSearch != null)
        {
            flavorDefsToSearch = flavorDefsToSearch.Intersect(DietIndex[diet])
            .Where(flavorDef =>
                ((mealCanBeAnyKind && flavorDef.MealKinds.Any(FlavorCategoryDefOf.FT_MealsNonSpecial.ThisAndDescendants.Contains)) || (!mealCanBeAnyKind && flavorDef.MealKinds.Any(mealKind => mealThingParentCategories.Contains(mealKind))))
                && (flavorDef.MealQualities.NullOrEmpty() || flavorDef.MealQualities.Any(mealQuality => mealQuality.ContainedInThisOrDescendant(meal.def)))
                && (flavorDef.CookingStations.NullOrEmpty() || flavorDef.CookingStations.Any(cat =>
                    cat.ContainedInThisOrDescendant(compFlavor.CookingStation)))
                && flavorDef.HoursOfDay.min <= compFlavor.HourOfDay &&
                        compFlavor.HourOfDay <= flavorDef.HoursOfDay.max);
            if (flavorDefsToSearch.Count() > 0) return flavorDefsToSearch;
        }
        
        flavorDefsToSearch = DietIndex[diet];
        return flavorDefsToSearch
        .Where(flavorDef =>
            ((mealCanBeAnyKind && flavorDef.MealKinds.Any(FlavorCategoryDefOf.FT_MealsNonSpecial.ThisAndDescendants.Contains)) || (!mealCanBeAnyKind && flavorDef.MealKinds.Any(mealKind => mealThingParentCategories.Contains(mealKind))))
            && (flavorDef.MealQualities.NullOrEmpty() || flavorDef.MealQualities.Any(mealQuality => mealQuality.ContainedInThisOrDescendant(meal.def)))
            && (flavorDef.CookingStations.NullOrEmpty() || flavorDef.CookingStations.Any(cat =>
                cat.ContainedInThisOrDescendant(compFlavor.CookingStation)))
            && flavorDef.HoursOfDay.min <= compFlavor.HourOfDay &&
                    compFlavor.HourOfDay <= flavorDef.HoursOfDay.max);
        

    }
}

public class IngredientSlot : IExposable
{
    public List<FlavorCategoryDef> categories = [];
    public List<FlavorCategoryDef> disallowedCategories = [];
    internal HashSet<ThingDef> allowedThingDefs = [];
    internal HashSet<FlavorCategoryDef> allowedCategories = [];  // all allowed categories

    internal List<FlavorCategoryDef> Categories { get => categories; set => categories = value; }
    internal List<FlavorCategoryDef> DisallowedCategories { get => disallowedCategories; set => disallowedCategories = value; }
    internal IEnumerable<ThingDef> AllowedThingDefs => allowedThingDefs;
    internal IEnumerable<FlavorCategoryDef> AllowedCategories => allowedCategories;

    internal void AddAllowedCategoriesAndThingsRecursive(IEnumerable<FlavorCategoryDef> cats)
    {
        foreach (var cat in cats)
        {
            if (DisallowedCategories.Contains(cat)) continue;
            allowedCategories.Add(cat);
            allowedThingDefs.AddRange(cat.ChildThingDefs);
            AddAllowedCategoriesAndThingsRecursive(cat.ChildCategories);
        }
    }

    public virtual void ExposeData()
    {
        Scribe_Collections.Look(ref allowedThingDefs, "allowedThingDefs");
    }
}

