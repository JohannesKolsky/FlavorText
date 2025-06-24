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
[StaticConstructorOnStartup]
public static class CategoryUtility
{

    private static bool tag;  // DEBUG

    internal static Dictionary<ThingDef, List<FlavorCategoryDef>> ThingCategories = [];
    internal static Dictionary<ThingDef, List<FlavorCategoryDef>> MealsQualities = [];
    static CategoryUtility()
    {
/*        Stopwatch stopwatch = new();
        stopwatch.Start();*/
        try
        {
            FlavorCategoryDef.FinalizeInit();
            FlavorCategoryDef.SetNestLevelRecursive(FlavorCategoryDef.Named("FT_Root"), 0);
            InheritSingularCollectiveIfNull(); // FT_Categories inherit some data from parents

            AssignToFlavorCategories(); // assign all relevant ThingsDefs to a FlavorText FlavorCategoryDef

            // can't do this until now, needs previous method and a built DefDatabase
            DefDatabase<FlavorCategoryDef>.ResolveAllReferences();
            DefDatabase<FlavorDef>.ResolveAllReferences();

            FlavorDef.SetStaticData(); // get total specificity for each FlavorDef; get other static data
            InflectionUtility.AssignIngredientInflections();
            Debug();

        }
        catch (Exception ex)
        {
            Log.Error($"Error when setting up FlavorCategoryDefs for Flavor Text. Error: {ex}");
        }

/*        stopwatch.Stop();
        TimeSpan elapsed = stopwatch.Elapsed;
        if (Prefs.DevMode)
        {
            Log.Warning("[Flavor Text] FlavorCategoryDefUtility ran in " + elapsed.ToString("ss\\.fffff") + " seconds");
        }*/

    }

    private static void Debug()
    {
        /*        foreach (var thing in DefDatabase<ThingDef>.AllDefs.Where(thing => DefDatabase<FlavorCategoryDef>.GetNamed("FT_Foods").ContainedInThisOrDescendant(thing)))
                {
                    Log.Warning($">{thing.defName} is in categories:");
                    foreach (FlavorCategoryDef category in ThingCategories[thing])
                    {
                        Log.Message($"{category.defName}");
                    }
                }*/
/*
        Log.Message($"[{FlavorCategoryDef.Named("FT_MealsKinds").childThingDefs.ToStringSafeEnumerable()}]");
        Log.Message($"[{ThingCategories.TryGetValue(ThingDef.Named("Meat_Cow")).ToStringSafeEnumerable()}]");*/

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
                //tag = food.defName.ToLower().Contains("pastry");
                var categories = ThingCategories.TryGetValue(food) ?? throw new NullReferenceException($"list of FlavorCategories for {food} in the ThingCategories dictionary was null.");
                Dictionary<FlavorCategoryDef, int> newParents = null;
                List<FlavorCategoryDef> newParentsSorted = null;
                if (categories.Empty())
                {
                    if (tag) Log.Warning($"figuring out best FlavorCategory for {food} from mod {food?.modContentPack?.PackageId?.ToStringSafe()}");
                    List<string> splitNames = ExtractNames(food);
                    newParents = GetBestFlavorCategory(splitNames, food, FlavorCategoryDefOf.FT_Foods);
                    newParentsSorted = [.. newParents.OrderByDescending(element => element.Value).Select(element => element.Key)];

                    if (!newParentsSorted.Empty())
                    {
                        if (tag) Log.Message(newParents.ToStringSafeEnumerable());
                        var newParent = newParentsSorted.First();
                        ThingCategories[food].AddDistinct(newParent);
                        newParent.childThingDefs.Add(food);
                    }
                }
                categories = ThingCategories.TryGetValue(food) ?? throw new NullReferenceException($"list of FlavorCategories for {food} in the ThingCategories dictionary was null on the second try.");
                if (categories.Empty()) throw new ArgumentOutOfRangeException($"list of FlavorCategories for {food} in the ThingCategories dictionary was empty on the second try.");

                if (tag) Log.Warning($"testing {food} with categories {categories.ToStringSafeEnumerable()}");

                // if ThingDef should have CompFlavor, postpend a new one
                // move meal quality categories to a special dictionary; if this means the meal has no regular categories left, add it to FT_MealsCooked
                if (food.HasComp<CompIngredients>() && categories.Any(cat => FlavorCategoryDefOf.FT_MealsWithCompFlavor.ThisAndChildCategoryDefs.Contains(cat)))
                {
                    food.comps.Add(new CompProperties_Flavor());
                    var qualityCats = categories.Where(cat => FlavorCategoryDefOf.FT_MealsQualities.ThisAndChildCategoryDefs.Contains(cat)).ToList();
                    foreach (var qualityCat in qualityCats)
                    {
                        if (MealsQualities.ContainsKey(food)) MealsQualities[food].Add(qualityCat);
                        else MealsQualities.Add(food, [qualityCat]);
                        ThingCategories[food].Remove(qualityCat);
                    }
                    if (ThingCategories[food].Empty())
                    {
                        ThingCategories[food].Add(FlavorCategoryDefOf.FT_MealsNonSpecial);
                        FlavorCategoryDefOf.FT_MealsNonSpecial.childThingDefs.Add(food);
                    }
                }
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
                var categories = ThingCategories.TryGetValue(building) ?? throw new NullReferenceException($"list of FlavorCategories for {building} in the ThingCategories dictionary was null.");
                if (categories.Empty())
                {
                    //Log.Warning($"{building?.ToStringSafe()} from mod {building?.modContentPack?.PackageId.ToStringSafe()}");
                    List<string> splitNames = ExtractNames(building);
                    var newParents = GetBestFlavorCategory(splitNames, building, FlavorCategoryDefOf.FT_CookingStations);
                    if (newParents.Count > 0)
                    {
                        var newParent = newParents.MaxBy(element => element.Value).Key;
                        ThingCategories[building].AddDistinct(newParent);
                        newParent.childThingDefs.Add(building);
                    }
                }
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
        foreach (var flavorCategory in allFlavorCategoryDefs)
        {
            foreach (var child in flavorCategory.thingDefsToAbsorb)
            {
                if (ThingCategories.ContainsKey(child)) ThingCategories[child].AddDistinct(flavorCategory);
                else ThingCategories.Add(child, [flavorCategory]);
                flavorCategory.childThingDefs.Add(child);
                //Log.Message($"absorbing direct ThingDef {child} into {ThingCategories[child].ToStringSafeEnumerable()}...");
            }
            foreach (var thingCategory in flavorCategory.thingCategoryDefsToAbsorb)
            {
                //Log.Warning($"absorbing ThingCategoryDef {thingCategory} into {flavorCategory}...");
                foreach (var descendant in thingCategory.DescendantThingDefs)
                {
                    if (ThingCategories.ContainsKey(descendant)) 
                    {
                        var parents = ThingCategories[descendant];
                        if (!parents.Any(parent => flavorCategory.ThisAndChildCategoryDefs.Contains(parent))) ThingCategories[descendant].Add(flavorCategory);
                    } 
                    else ThingCategories.Add(descendant, [flavorCategory]);
                    flavorCategory.childThingDefs.AddDistinct(descendant);
                    //Log.Message($"absorbed descendant ThingDef {descendant} into {ThingCategories[descendant].ToStringSafeEnumerable()}...");
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

    private static Dictionary<FlavorCategoryDef, int> GetBestFlavorCategory(List<string> splitNames, ThingDef searchedDef, FlavorCategoryDef topLevelCategory, int minMealsWithCompFlavorScore = 5)
    {
        //tag = searchedDef.defName.ToLower().Contains("ball");
        if (tag) { Log.Message("------------------------"); Log.Warning($"Finding correct Flavor Category for {searchedDef.defName}"); }

        int categoryScore = 0;
        Dictionary<FlavorCategoryDef, int> bestFlavorCategories = [];
        var splitNamesBlackList = splitNames;  // blacklist always stays based on original Def defName and label
        var categoriesToSearch = topLevelCategory.ThisAndChildCategoryDefs.ToList();
        List<FlavorCategoryDef> categoriesToSkip = [];

        try
        {
            if (tag) { Log.Message($"Getting BestFlavorCategory for {searchedDef.defName}"); }

            for (var i = 0; i < categoriesToSearch.Count; i++)
            {
                var flavorCategory = categoriesToSearch[i];
                if (!categoriesToSkip.Contains(flavorCategory) && !bestFlavorCategories.ContainsKey(flavorCategory))
                {
                    GetKeywordScores(flavorCategory);
                    if (categoryScore >= 1) bestFlavorCategories.Add(flavorCategory, categoryScore);
                }
            }

            // if the best category was in FT_MealsWithCompFlavor but its score wasn't high enough or Dynamic Meal Incorporation setting is off, replace it with FT_FoodMeals
            if (bestFlavorCategories.Count > 0)
            {
                var bestCategory = bestFlavorCategories.MaxBy(element => element.Value);
                {
                    if (bestCategory.Key.ThisAndParents.Contains(FlavorCategoryDefOf.FT_MealsWithCompFlavor))
                    {
                        if (bestCategory.Value < minMealsWithCompFlavorScore || !FlavorTextSettings.dynamicMealIncorporation) 
                        {
                            bestFlavorCategories.Remove(bestCategory.Key);
                            bestFlavorCategories.Add(FlavorCategoryDef.Named("FT_FoodMeals"), bestCategory.Value);
                        }
                    }
                }
            }
            // if you couldn't find any categories, try using the Def's vanilla parent categories as the search keywords
            // this strategy forbids allowing the item to get a CompFlavor, to avoid overriding specialized modded meals
            if (bestFlavorCategories.Count == 0)
            {
                categoriesToSearch.RemoveAll(cat => FlavorCategoryDefOf.FT_MealsWithCompFlavor.ThisAndChildCategoryDefs.Contains(cat));
                ThingCategoryDef topLevelThingCategoryDef = !topLevelCategory.sisterCategories.Empty()
                    ? topLevelCategory.sisterCategories.First()
                    : null;

                var defParents = searchedDef.thingCategories?.Where(cat => cat != null && cat.Parents.Contains(topLevelThingCategoryDef)).ToList();
                while (true)
                {
                    if (defParents.NullOrEmpty()) break;
                    if (tag) Log.Warning($"{searchedDef.defName} had parent categories [{defParents.ToStringSafeEnumerable()}]");

                    foreach (ThingCategoryDef defParent in defParents!)
                    {
                        splitNames = ExtractNames(defParent);

                        foreach (var flavorCategory in categoriesToSearch)
                        {
                            // if the current flavor category being tested has a sister category, give it a flat score of 6
                            var sisterCategories = flavorCategory.sisterCategories;
                            if (sisterCategories != null && sisterCategories.Contains(defParent))
                            {
                                categoryScore = 6;
                            }

                            // otherwise do the normal keyword tests
                            else if (!bestFlavorCategories.ContainsKey(flavorCategory))
                            {
                                GetKeywordScores(flavorCategory);
                            }

                            if (categoryScore >= 1) 
                                bestFlavorCategories.AddDistinct(flavorCategory, categoryScore);
                        }
                    }

                    defParents = [.. defParents
                    .Where(cat => cat != topLevelThingCategoryDef)
                    .Select(cat => cat.parent)
                    .Where(parent => parent != null)];
                    defParents.RemoveDuplicates();
                }
            }

            if (bestFlavorCategories.Count() > 0) return bestFlavorCategories;

            bestFlavorCategories.Add(topLevelCategory, 1);
            //Log.Warning($"Could not find appropriate FT_FlavorCategoryDef for {searchedDef.defName}, using {topLevelCategory.defName} instead with score of 1.");
            return bestFlavorCategories;
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
            //if (tag) Log.Warning($"keywords for {flavorCategory}");
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


