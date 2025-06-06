using LinqToDB.Common;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

// Verse.ThingCategoryNodeDatabase.FinalizeInit() is what adds core stuff to FlavorCategoryDef.childCategories

//DONE: poultry eggs show up in FT_Poultry
//DONE: FT_Poultry and FT_Sheep aren't getting their relevant thingdefs
//DONE: some drugs are being tested for some reason
//DONE: VCE_Condiments are going all over
//DONE: "FoodRaw" should be default "this is an ingredient that adds calories"; "Foods" covers condiments and such xxx this would require another level, which isn't worth it
//DONE: VCE_bakes aren't finding the flavorDef despite matching ones found, even if it's just flour
//DONE: fertilized eggs are ending up in FT_EggUnfertilized (cause "egg" keyword?)
//DONE: VCE_canned meat isn't finding any flavor defs
//DONE: VCE_canned fruit is in FT_Meals
//DONE: VCE_canned eggs are in FT_Meals
//DONE: get FT to put VCE_canned into meats/vegetables/fruits/etc
//DONE: VGP defNames are sing: bean, lentil, beet
//xxTODO: if an ingredient has subingredients, use the subingredients instead of the main ingredient (VCE canned stuff, GAB pickled stuff, meals, etc) //xx canned human meat in a meal is not seen as human meat
//RELEASED: Flavor Text is still showing up in bills
//DONE: make patch that adds CompFlavor more precise
//DONE: sunflower seeds show up as sunflower
//DONE: allow for adding multiple FT_Categories at a time?
//DONE: VGE watermelon is in candy
//DONE: RC2 Chili peppers are in FT_Foods
//DONE: keep canned and pickled and such; problem is atm not deleting those causes "meat" to be deleted
//DONE: can you get link FT_MealsFlavor to FT_FoodMeals and not have this funkiness where you assign to the second and then check if it also belongs in the first?

namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for FlavorText-related FlavorCategoryDefs
/// </summary>

[DefOf]
public static class FlavorCategoryDefOf
{
    public static FlavorCategoryDef FT_Root;
    public static FlavorCategoryDef FT_Foods; // topmost category used for meal ingredients; couple unused FT_Categories on top, then FT_Root
    public static FlavorCategoryDef FT_Buildings;
    public static FlavorCategoryDef FT_MealsFlavor;
}

[StaticConstructorOnStartup]
public static class CategoryUtility
{

    private static bool tag;  // DEBUG

    internal static Dictionary<ThingDef, List<FlavorCategoryDef>> ThingCategories = [];
    static CategoryUtility()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        try
        {
            InheritSingularCollectiveIfNull(); // FT_Categories inherit some data from parents
            FlavorCategoryDef.FinalizeInit();
            FlavorCategoryDef.SetNestLevelRecursive(FlavorCategoryDef.Named("FT_Root"), 0);

            AssignToFlavorCategories(); // assign all relevant ThingsDefs to a FlavorText FlavorCategoryDef

            // can't do this until now, needs previous method and a built DefDatabase
            DefDatabase<FlavorCategoryDef>.ResolveAllReferences();
            DefDatabase<FlavorDef>.ResolveAllReferences();

            FlavorDef.SetCategoryData(); // get total specificity for each FlavorDef; get other static data
            InflectionUtility.AssignIngredientInflections();
            Debug();

        }
        catch (Exception ex)
        {
            Log.Error($"Error when setting up FlavorCategoryDefs for Flavor Text. Error: {ex}");
        }

        stopwatch.Stop();
        TimeSpan elapsed = stopwatch.Elapsed;
        if (Prefs.DevMode)
        {
            Log.Warning("[Flavor Text] FlavorCategoryDefUtility ran in " + elapsed.ToString("ss\\.fffff") + " seconds");
        }

    }

    private static void Debug()
    {
        /*        foreach (var thing in DefDatabase<ThingDef>.AllDefs.Where(thing => DefDatabase<FlavorCategoryDef>.GetNamed("FT_Foods").ContainedInThisOrDescendant(thing)))
                {
                    Log.Warning($">{thing.defName} is in categories:");
                    foreach (FlavorCategoryDef category in ItemCategories[thing])
                    {
                        Log.Message($"{category.defName}");
                    }
                }*/

        /*        foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.ToList())
                {
                    if (thingDef.HasComp<CompFlavor>() && FlavorCategoryDef.Named("FT_MealsFlavor").ContainedInThisOrDescendant(thingDef))
                    {
                        Log.Warning($">>>{thingDef.defName} has CompFlavor and is in FT_MealsFlavor");
                    }
                }*/

        /*foreach (var cat in DefDatabase<FlavorCategoryDef>.AllDefs.Where(catDef => catDef.defName.Contains("FT_Meat")))
        {
            Log.Warning(cat.defName);
            foreach (ThingDef thingDef in cat.DescendantThingDefs)
            {
                Log.Message($"{thingDef.defName}");
            }
        }*/
    }

    // for FT_Categories, inherit mod extension variables from parent where appropriate
    public static void InheritSingularCollectiveIfNull()
    {
        foreach (FlavorCategoryDef cat in FlavorCategoryDefOf.FT_Foods.ThisAndChildCategoryDefs)
        {
            // inherit singularCollective field value from parent if it is null in child
            cat.singularCollective ??= cat.parent.singularCollective;
        }
    }

    // assign all ThingDefs and FlavorCategoryDefs in "Foods" to the best FT_ThingCategory
    // add CompFlavor to appropriate meals
    public static void AssignToFlavorCategories()
    {
        // look in FlavorCategories and add any predefined ThingDefs and ThingCategoryDef contents to the FlavorCategory
        AbsorbChildren();

        // add everything in vanilla "Foods" to the item FlavorCategoryDef dictionary
        foreach (var food in ThingCategoryDef.Named("Foods").DescendantThingDefs.Distinct())
        {
            ThingCategories.AddDistinct(food, []);
        }

        foreach (ThingDef food in ThingCategories.Keys)
        {
            try
            {
                //tag = food.defName.ToLower().Contains("mushroom");
                var categories = ThingCategories.TryGetValue(food) ?? throw new NullReferenceException($"list of FlavorCategories for {food} in the ItemCategories dictionary was null.");
                if (categories.Empty())
                {
                    //Log.Warning($"figuring out best FlavorCategory for {food} from mod {food?.modContentPack?.PackageId?.ToStringSafe()}");
                    List<string> splitNames = ExtractNames(food);
                    FlavorCategoryDef newParent = GetBestFlavorCategory(splitNames, food, FlavorCategoryDefOf.FT_Foods);

                    if (newParent != null)
                    {
                        ThingCategories[food].Add(newParent);
                        newParent.childThingDefs.Add(food);
                        categories = ThingCategories.TryGetValue(food);
                    }
                }

                // if ThingDef should have CompFlavor, postpend a new one
                if (food.HasComp<CompIngredients>() && (categories.Contains(FlavorCategoryDefOf.FT_MealsFlavor) || categories.Any(cat => cat.Parents.Contains(FlavorCategoryDefOf.FT_MealsFlavor))))
                {
                    food.comps.Add(new CompProperties_Flavor());
                }
                tag = false;
            }
            catch (Exception)
            {
                Log.Error($"{food?.ToStringSafe()} from mod {food?.modContentPack?.PackageId?.ToStringSafe()} had an error");
                throw;
            }
        }


        var allMealSourceBuildings = DefDatabase<ThingDef>.AllDefs.Where(b => b.building is { isMealSource: true }).ToList();
        foreach (ThingDef building in allMealSourceBuildings)
        {
            try
            {
                if (!ThingCategories.ContainsKey(building)) ThingCategories.Add(building, []);
                var categories = ThingCategories.TryGetValue(building) ?? throw new NullReferenceException($"list of FlavorCategories for {building} in the ItemCategories dictionary was null.");
                if (categories.Empty())
                {
                    //Log.Warning($"{building?.ToStringSafe()} from mod {building?.modContentPack?.PackageId.ToStringSafe()}");
                    List<string> splitNames = ExtractNames(building);
                    FlavorCategoryDef newParent = GetBestFlavorCategory(splitNames, building, FlavorCategoryDefOf.FT_Buildings);

                    if (newParent != null)
                    {
                        ThingCategories[building].AddDistinct(newParent);
                        newParent.childThingDefs.Add(building);
                    }
                }
                tag = false;
            }
            catch (Exception)
            {
                Log.Error($"{building?.ToStringSafe()} from mod {building?.modContentPack?.PackageId?.ToStringSafe()} had an error");
                throw;
            }
        }



    }

    // add to their parent FlavorCategoryDefs all ThingDefs and ThingCategoryDef children that were explicitly assigned in XML
    private static void AbsorbChildren()
    {
        // make a thingDefs of which ThingDefs belong in which FlavorCategoryDefs
        var allFlavorCategoryDefs = FlavorCategoryDef.Named("FT_Root").ThisAndChildCategoryDefs;
        allFlavorCategoryDefs = allFlavorCategoryDefs.Reverse();  // by reversing, you start at the lowest categories and work your way up  // this allows absorbing specific ThingDefs before the whole group in a higher Flavor Category
        var flavorThingDefsCopy = DefDatabase<ThingDef>.AllDefs.Select(t => t).ToList();
        foreach (var flavorCategory in allFlavorCategoryDefs)
        {
            foreach (var child in flavorCategory.thingDefsToAbsorb)
            {
                if (flavorThingDefsCopy.Contains(child))
                {
                    //Log.Message($"absorbing ThingDef {child} into {flavorCategory}...");
                    flavorThingDefsCopy.Remove(child);
                    if (ThingCategories.ContainsKey(child)) ThingCategories[child].AddDistinct(flavorCategory);
                    else ThingCategories.Add(child, [flavorCategory]);
                    flavorCategory.childThingDefs.Add(child);
                }
            }
            foreach (var thingCategory in flavorCategory.thingCategoryDefsToAbsorb)
            {
                //Log.Message($"absorbing ThingCategoryDef {thingCategory} into {flavorCategory}...");
                foreach (var descendant in thingCategory.DescendantThingDefs)
                {
                    if (flavorThingDefsCopy.Contains(descendant))
                    {
                        //Log.Message($"absorbing ThingDef {descendant} into {flavorCategory}...");
                        flavorThingDefsCopy.Remove(descendant);
                        if (ThingCategories.ContainsKey(descendant)) ThingCategories[descendant].AddDistinct(flavorCategory);
                        else ThingCategories.Add(descendant, [flavorCategory]);
                        flavorCategory.childThingDefs.AddDistinct(descendant);
                    }
                }
            }
        }
    }

    // split up the defName and label into single words and compile them; these will be searched to assign it a category
    private static List<string> ExtractNames(Def def)
    {
        if (tag) { Log.Warning($"Getting names for {def.defName}"); }
        List<string> splitNames = [];
        // try to find word boundaries in the defName and label and split it into those words
        string defNames = Regex.Replace(def.defName, "([_])|([-])", " ");
        defNames = Regex.Replace(defNames, "(?<=[a-zA-Z])([A-Z][a-z]+)", " $1");  // split up name based on capitalized words
        defNames = Regex.Replace(defNames, "(?<=[a-z])([A-Z]+)", " $1");  // split up names based on unbroken all-caps sequences

        defNames = defNames.ToLower();
        if (tag) { Log.Message($"defNames = {defNames}"); }
        string[] splitDefNames = defNames.Split(' ');
        foreach (string defName in splitDefNames) { splitNames.Add(defName); if (tag) { Log.Message(defName); } }

        string labels = Regex.Replace(def.label, "([-])", " ");
        if (tag) { Log.Message($"labels = {labels}"); }
        string[] splitLabels = labels.Split(' ');
        foreach (string label in splitLabels) { splitNames.Add(label); if (tag) { Log.Message(label); } }

        return splitNames;
    }

    private static FlavorCategoryDef GetBestFlavorCategory(List<string> splitNames, ThingDef searchedDef, FlavorCategoryDef topLevelCategory, int minMealsFlavorScore = 6)
    {
        if (tag) { Log.Message("------------------------"); Log.Warning($"Finding correct Flavor Category for {searchedDef.defName}"); }

        int categoryScore;
        int bestCategoryScore = 0;
        FlavorCategoryDef bestFlavorCategory = null;
        var splitNamesBlackList = splitNames;  // blacklist always stays based on original Def defName and label
        var categoriesToSearch = topLevelCategory.ThisAndChildCategoryDefs.ToList();
        List<FlavorCategoryDef> categoriesToSkip = [];

        try
        {
            if (tag) { Log.Message($"Getting BestFlavorCategory for {searchedDef.defName}"); }

            for (var i = 0; i < categoriesToSearch.Count; i++)
            {
                var flavorCategory = categoriesToSearch[i];
                if (!categoriesToSkip.Contains(flavorCategory))
                {
                    GetKeywordScores(flavorCategory);
                    CompareCategoryScores(flavorCategory);
                }
            }

            // if the best category was FT_MealsFlavor but its score wasn't high enough, choose FT_FoodMeals as the best category instead
            if (bestFlavorCategory == FlavorCategoryDefOf.FT_MealsFlavor && bestCategoryScore < minMealsFlavorScore)
            {
                bestFlavorCategory = FlavorCategoryDef.Named("FT_FoodMeals");
            }

            // if nothing matched, try using the Def's vanilla parent categories as the search keywords
            if (bestFlavorCategory == null)
            {
                //{ Log.Error($"No category found for {searchedDef.defName}, looking at its parent categories"); }

                ThingCategoryDef topLevelThingCategoryDef = !topLevelCategory.sisterCategories.Empty()
                    ? topLevelCategory.sisterCategories.FirstOrDefault()
                    : null;

                var defParents = searchedDef.thingCategories?.Where(cat => cat != null && cat.Parents.Contains(topLevelThingCategoryDef)).ToList();
                while (true)
                {
                    if (defParents.NullOrEmpty()) break;
                    //Log.Warning($"{searchedDef.defName} had parent categories [{defParents.ToStringSafeEnumerable()}]");

                    foreach (ThingCategoryDef defParent in defParents!)
                    {
                        splitNames = ExtractNames(defParent);

                        foreach (var flavorCategory in categoriesToSearch)
                        {
                            // if the current flavor category being tested has a sister category, give it a flat score of 3
                            var sisterCategories = flavorCategory.sisterCategories;
                            if (sisterCategories != null && sisterCategories.Contains(defParent))
                            {
                                categoryScore = 3;
                            }

                            // otherwise do the normal keyword tests
                            else
                            {
                                GetKeywordScores(flavorCategory);
                            }

                            CompareCategoryScores(flavorCategory);
                        }
                    }

                    defParents = [.. defParents
                        .Where(cat => cat != topLevelThingCategoryDef)
                        .Select(cat => cat.parent)
                        .Where(parent => parent != null)];
                    defParents.RemoveDuplicates();
                }
                //if (bestFlavorCategory != null & Prefs.DevMode) Log.Warning($"Using its parent categories, found new best flavor category {bestFlavorCategory.defName} for Def {searchedDef.defName}");
            }

            if (bestFlavorCategory != null) return bestFlavorCategory;

            bestFlavorCategory = topLevelCategory;
            //Log.Warning($"Could not find appropriate FT_FlavorCategoryDef for {searchedDef.defName}, using {topLevelCategory.defName} instead.");
            return bestFlavorCategory;
        }
        catch (Exception ex)
        {
            Log.Error($"error testing {searchedDef.defName}: {ex}");
            throw;
        }

        void GetKeywordScores(FlavorCategoryDef flavorCategory)
        {
            // get a score based on how well the flavorCategory keywords match the searchedDef's names
            categoryScore = 0;
            if (tag) Log.Warning($"keywords for {flavorCategory}");
            List<string> keywords = flavorCategory.keywords;
            foreach (string keyword in keywords)
            {
                categoryScore += ScoreKeyword(splitNames, keyword);
            }

            if (categoryScore < 3) return;

            // check blacklist, if score is too low after doing so, remove flavorCategory and its descendants from the list of categories to search
            if (tag) Log.Warning($"blacklist for {flavorCategory}");
            List<string> blacklist = flavorCategory.blacklist;
            foreach (string black in blacklist)
            {
                categoryScore -= 2 * ScoreKeyword(splitNames, black);
            }

            if (categoryScore >= 3) return;
            foreach (var cat in flavorCategory.ThisAndChildCategoryDefs)
            {
                categoriesToSkip.AddDistinct(cat);
            }
        }

        void CompareCategoryScores(FlavorCategoryDef flavorCategory)
        {
            if (categoryScore >= 1)
            {
                if (tag) { Log.Message($"Found matching category {flavorCategory} with score of {categoryScore} and nest depth of {flavorCategory.nestDepth}"); }
                if (bestFlavorCategory == null)
                {
                    bestCategoryScore = categoryScore;
                    bestFlavorCategory = flavorCategory;
                    if (tag) { Log.Message($"->Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.nestDepth}"); }
                }
                else if (flavorCategory.Parents.Contains(bestFlavorCategory))  // if subcategory, add scores together
                {
                    bestCategoryScore += categoryScore;
                    bestFlavorCategory = flavorCategory;
                    if (tag) { Log.Message($"->Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.nestDepth}"); }
                }
                else if (categoryScore > bestCategoryScore || categoryScore == bestCategoryScore && flavorCategory.nestDepth > bestFlavorCategory.nestDepth)
                {
                    bestCategoryScore = categoryScore;
                    bestFlavorCategory = flavorCategory;
                    if (tag) { Log.Message($"->Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.nestDepth}"); }
                }
            }
        }
    }

    // see how well the keyword fits into splitNames: element matches keyword exactly, element starts or ends with keyword, element contains keyword, keyword phrase is present in combined splitNames
    private static int ScoreKeyword(List<string> splitNames, string keyword)
    {
        int keywordScore = 0;

        foreach (string name in splitNames)
        {
            // contains: +1 to score each time if the keyword matches any part of an element in splitNames (e.g. 2x 'ump' in [pumpkin, orange, smoothie, sugar, pumpkins]
            if (!name.Contains(keyword)) continue;
            keywordScore += 1;
            if (tag) Log.Message($"+1 to {name} contains {keyword}");

            // start/end: +2 to score each time if the keyword matches the start or end of an element in splitNames (e.g. 2x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins]
            if (!(name.StartsWith(keyword) || name.EndsWith(keyword))) continue;
            keywordScore += 1;
            if (tag) Log.Message($"+1 to {name} starts with {keyword}");

            // exact: +3 to score each time the keyword matches an element exactly in splitNames (e.g. 1x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins])
            if (name != keyword) continue;
            keywordScore += 1;
            if (tag) Log.Message($"+1 to {name} == {keyword}");
        }
        // contains keyword phrase: +6 to score each time the keyword matches a substring of splitNames when they're all combined with spaces (e.g. 1x 'sugar pumpkin' in "pumpkin orange smoothie sugar pumpkins")
        // this effectively checks for multi-word keywords if nothing else matched
        if (keywordScore == 0)
        {
            int count = 0;
            string joinedNames = string.Join(" ", splitNames);
            for (int i = 0; i < joinedNames.Length - keyword.Length + 1; i++)
            {
                if (joinedNames.Substring(i, keyword.Length) == keyword)
                {
                    count++;
                    if (tag) Log.Message($"+6 to {joinedNames} substring {keyword}");
                }
            }
            keywordScore += 6 * count;
        }
        return keywordScore;
    }
}


