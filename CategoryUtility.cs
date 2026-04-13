using FlavorText;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using Verse;
using static FlavorText.CategoryUtility;

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

//TODO: examine more items that may be food: anything that has nutrition (drugs, alcohol)
//TODO: if defName and label are different, the meal is never categorized: e.g. DankPyon_Slop_Simple (stew)
//TODO: something like DankPyon_MealRations only scores 4 and isn't CompFlavored // re-add inheriting parent category scores?
//TODO: hitting the blacklist isn't triggering the removal of the category and its descendants
//TODO: create a function for searching child and parent categories
//TODO: you can remove all categories with 0 contained ThingDefs to boost GetFlavorText search speed

namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for FlavorText-related FlavorCategoryDefs
/// </summary>
[StaticConstructorOnStartup]
internal static class CategoryUtility
{
    private static bool tag;  // DEBUG

    internal static Dictionary<ThingDef, List<FlavorCategoryDef>> ThingParentCategories = [];
    internal static Dictionary<ThingDef, List<FlavorCategoryDef>> MealsQualities = [];
    internal const int goodScoreForCategorization = 5;
    static CategoryUtility()
    {
        Stopwatch stopwatch = new();
        stopwatch.Start();
        try
        {
            Initialize();

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

    internal static void Initialize()
    {
        FlavorCategoryDef.FinalizeInit();
        FlavorCategoryDef.SetNestLevelRecursive(FlavorCategoryDefOf.FT_Root, 0);
        InheritParentData(); // FT_Categories inherit some data from parents

        AssignToFlavorCategories(); // assign all relevant ThingsDefs to a FlavorText FlavorCategoryDef
        // can't do this until now, needs previous method and a built DefDatabase
        DefDatabase<FlavorCategoryDef>.ResolveAllReferences();
        PruneInactiveFlavorCategoriesRecursive(FlavorCategoryDefOf.FT_Root); // remove links to all FlavorCategoryDefs that don't have a descendant ThingDef
        DefDatabase<FlavorDef>.ResolveAllReferences();

        FlavorDef.SetStaticData(); // get total specificity for each FlavorDef; get other static data
        InflectionUtility.AssignIngredientInflections();
        //Debug();
    }

/*    internal static void Reinitialize()
    {
        XmlInheritance.Clear();
        DefDatabase<FlavorCategoryDef>.Clear();
        DefDatabase<FlavorDef>.Clear();
        FlavorTextMod.flavorTextSettings.Mod.Content.ClearDefs();
        List<LoadableXmlAsset> flavorTextXML = [.. FlavorTextMod.flavorTextSettings.Mod.Content.LoadDefs(hotReload: true)];
        Dictionary<XmlNode, LoadableXmlAsset> assetlookup = [];
        XmlDocument xmlDocument = LoadedModManager.CombineIntoUnifiedXML(flavorTextXML, assetlookup);
        LoadedModManager.ParseAndProcessXML(xmlDocument, assetlookup, hotReload: true);
        XmlInheritance.Clear();
        Log.Warning($"initializing CategoryUtility");
        Initialize();  //TODO: parents are still null where parents were removed before
        Log.Warning("finished initializing CategoryUtility");
        
    }*/

    private static void Debug()
    {
        int count = FlavorCategoryDefOf.FT_Foods.ThisAndDescendants.Count();
        Log.Warning($"found {count} food items");
        foreach (var thing in DefDatabase<ThingDef>.AllDefs.Where(thing => DefDatabase<FlavorCategoryDef>.GetNamed("FT_Foods").ContainedInThisOrDescendant(thing)))
        {
            tag = ThingParentCategories[thing].Contains(FlavorCategoryDefOf.FT_Foods);
            if (tag)
            {
                Log.Message($">{thing.defName} with parent categories [{ThingParentCategories[thing].Select(parent => $"\n{parent.ToStringSafe()}] had child ThingDefs [{parent.DescendantThingDefs.ToStringSafeEnumerable()}]").ToStringSafeEnumerable()}");
            }
            tag = false;
        }

        //foreach (var cat in FlavorCategoryDefOf.FT_Foods.ThisAndDescendants)
        //{
        //    Log.Warning($"found {cat.ToStringSafe()} with parents [{cat.parents.ToStringSafeEnumerable()}] and child categories [{cat.childCategories.ToStringSafeEnumerable()}]");
        //}

        //Log.Message($"[{FlavorCategoryDef.Named("FT_MealsKinds").childThingDefs.ToStringSafeEnumerable()}]");
        //Log.Message($"[{ThingParentCategories.TryGetValue(ThingDef.Named("Meat_Cow")).ToStringSafeEnumerable()}]");

/*        foreach (var cat in DefDatabase<FlavorCategoryDef>.AllDefs.Where(catDef => catDef.defName.Contains("FT_Meat")))
        {
            Log.Warning(cat.defName);
            foreach (ThingDef thingDef in cat.DescendantThingDefs)
            {
                Log.Message($"{thingDef.defName}");
            }
        }*/
    }

    // for FT_Categories, inherit mod extension variables from parent where appropriate
    public static void InheritParentData()
    {
        foreach (FlavorCategoryDef cat in FlavorCategoryDefOf.FT_Foods.ThisAndDescendants)
        {
            if (cat.parents == null) { Log.Error($"{cat.ToStringSafe()} had null parent when attempting to inherit parent data"); continue; }
            if (cat.singularCollective == null)
            {
                if (cat.parents.All(parent => parent.singularCollective == cat.parents[0].singularCollective))
                {
                    cat.singularCollective = cat.parents[0].singularCollective;
                }
                else throw new ArgumentException($"the parents of {cat.ToStringSafe()} did  not have matching singularCollective field values. The values were [{cat.parents.Select(parent => parent.singularCollective.ToStringSafe()).ToStringSafeEnumerable()}]");
            }
            //Log.Error($"{cat.ToStringSafe()} had singularCollective = {cat.singularCollective.ToStringSafe()}");

            // inherit alwaysUseOverride if null in child
            if (cat.alwaysUseOverride == null)
            {
                if (cat.parents.All(parent => parent.alwaysUseOverride == cat.parents[0].alwaysUseOverride))
                {
                    cat.alwaysUseOverride = cat.parents[0].alwaysUseOverride;
                }
                else throw new ArgumentException($"the parents of {cat.ToStringSafe()} did  not have matching alwaysUseOverride field values. The values were [{cat.parents.Select(parent => parent.alwaysUseOverride.ToStringSafe()).ToStringSafeEnumerable()}]");
            }

            cat.parents.ForEach(parent => cat.blacklist.AddRangeUnique(parent.blacklist));  // inherit blacklists of parents
            cat.parents.ForEach(parent => cat.blacklistedMods.AddRangeUnique(parent.blacklistedMods));  // inherit blacklisted mods of parents
            //if (cat.inflectionsOverride.Empty()) Log.Warning($"inflectionsOverride was empty for {cat.ToStringSafe()}");
        }
    }

    // assign all ThingDefs and FlavorCategoryDefs in "Foods" to the best FT_ThingCategory
    // add CompFlavor to appropriate meals
    private static void AssignToFlavorCategories()
    {
        // TODO: FT_Root is null on Reinitialize
        // look in FlavorCategories and add any predefined ThingDefs and ThingCategoryDef contents to the FlavorCategory
        AbsorbChildrenFromXML();

        // add everything in vanilla "Foods" to the item FlavorCategoryDef dictionary
        foreach (var food in ThingCategoryDef.Named("Foods").DescendantThingDefs.Distinct())
        {
            ThingParentCategories.AddDistinct(food, []);
        }

        foreach (ThingDef food in ThingParentCategories.Keys)
        {
            try
            {
                //tag = food.defName.ToLower().Contains("hornet") || food.defName.ToLower().Contains("honey");
                var categories = ThingParentCategories.TryGetValue(food) ?? throw new NullReferenceException($"list of FlavorCategories for {food} in the ThingCategories dictionary was null.");
                Dictionary<FlavorCategoryDef, int> newParents = null;
                List<FlavorCategoryDef> bestParentsList = null;
                if (categories.Empty())
                {
                    if (tag) Log.Warning($"figuring out best FlavorCategory for {food} from mod {food?.modContentPack?.PackageId?.ToStringSafe()}");
                    newParents = GetBestFlavorCategory(food, FlavorCategoryDefOf.FT_Foods);
                    int bestScore = newParents.Max(element => element.Value);
                    if (tag) Log.Error($"bestScore was {bestScore.ToStringSafe()}");
                    if (bestScore >= 2 * goodScoreForCategorization)  // accept all parent categories with a high enough score
                    {
                        bestParentsList = [.. newParents.Where(element => element.Value >= 2 * goodScoreForCategorization).Select(element => element.Key)];
                    }
                    else bestParentsList = [.. newParents.Where(element => element.Value == bestScore).Select(element => element.Key)];  // else accept the highest scored parent category

                    if (!bestParentsList.NullOrEmpty())
                    {
                        foreach (FlavorCategoryDef newParent in bestParentsList)
                        {
                            ThingParentCategories[food].AddDistinct(newParent);
                            newParent.childThingDefs.AddDistinct(food);
                            if (tag) Log.Message($"ThingParentCategories was [{ThingParentCategories[food].ToStringSafeEnumerable()}]");
                        }
                    }
                }
                categories = ThingParentCategories.TryGetValue(food) ?? throw new NullReferenceException($"list of FlavorCategories for {food} in the ThingCategories dictionary was null after searching all FlavorCategories.");
                if (categories.Empty()) throw new ArgumentOutOfRangeException($"list of FlavorCategories for {food} in the ThingCategories dictionary was empty after searching all FlavorCategories.");

                // if ThingDef should have CompFlavor, postpend a new one
                // move meal quality categories to a special dictionary; if this means the meal has no regular categories left, add it to FT_MealsNonSpecial
                if (food.HasComp<CompIngredients>() && categories.Any(cat => FlavorCategoryDefOf.FT_MealsWithCompFlavor.ThisAndDescendants.Contains(cat)))
                {
                    if (tag) Log.Message($"Adding CompFlavor to {food}");
                    food.comps.Add(new CompProperties_Flavor());
                    var qualityCats = categories.Where(cat => FlavorCategoryDefOf.FT_MealsQualities.ThisAndDescendants.Contains(cat)).ToList();
                    foreach (var qualityCat in qualityCats)
                    {
                        if (MealsQualities.ContainsKey(food)) MealsQualities[food].Add(qualityCat);
                        else MealsQualities.Add(food, [qualityCat]);
                        ThingParentCategories[food].Remove(qualityCat);
                    }

                    //TODO: is this redundant with flavorDef.mealKinds?
                    if (ThingParentCategories[food].Empty() || (FlavorTextSettings.laxRecipeMatching && ThingParentCategories[food].Contains(FlavorCategoryDefOf.FT_MealsCooked)))
                    {
                        ThingParentCategories[food].Add(FlavorCategoryDefOf.FT_MealsNormal);
                        FlavorCategoryDefOf.FT_MealsNormal.childThingDefs.Add(food);
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
                if (!ThingParentCategories.ContainsKey(building)) ThingParentCategories.Add(building, []);
                var categories = ThingParentCategories.TryGetValue(building) ?? throw new NullReferenceException($"list of FlavorCategories for {building} in the ThingCategories dictionary was null.");
                if (categories.Empty())
                {
                    //Log.Warning($"{building?.ToStringSafe()} from mod {building?.modContentPack?.PackageId.ToStringSafe()}");
                    var newParents = GetBestFlavorCategory(building, FlavorCategoryDefOf.FT_CookingStations);
                    if (newParents.Count > 0)
                    {
                        var newParent = newParents.MaxBy(element => element.Value).Key;
                        ThingParentCategories[building].AddDistinct(newParent);
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


    // remove references to FlavorCategoryDefs that don't have any descendant ThingDefs
    private static void PruneInactiveFlavorCategoriesRecursive(FlavorCategoryDef root)
    {
        if (root.DescendantThingDefs.Any())
        {
            foreach (var childCat in root.childCategories)
            {
                PruneInactiveFlavorCategoriesRecursive(childCat);
            }
        }
        else
        {
            root.parents = null;
            root.childCategories.Clear();
        }
    }


    // add to their parent FlavorCategoryDefs all ThingDefs and ThingCategoryDef children that were explicitly assigned in XML
    private static void AbsorbChildrenFromXML()
    {
        // make a thingDefs of which ThingDefs belong in which FlavorCategoryDefs
        var allFlavorCategoryDefs = FlavorCategoryDef.Named("FT_Root").ThisAndDescendants;
        allFlavorCategoryDefs = allFlavorCategoryDefs.Reverse();  // by reversing, you start at the lowest categories and work your way up  // this allows absorbing specific ThingDefs before the whole group in a higher Flavor Category
        foreach (var flavorCategory in allFlavorCategoryDefs)
        {
            foreach (var child in flavorCategory.thingDefsToAbsorb)
            {
                if (ThingParentCategories.ContainsKey(child)) ThingParentCategories[child].AddDistinct(flavorCategory);
                else ThingParentCategories.Add(child, [flavorCategory]);
                flavorCategory.childThingDefs.Add(child);
                //Log.Message($"absorbing direct ThingDef {child} into {ThingCategories[child].ToStringSafeEnumerable()}...");
            }
            foreach (var thingCategory in flavorCategory.thingCategoryDefsToAbsorb)
            {
                //Log.Warning($"absorbing ThingCategoryDef {thingCategory} into {flavorCategory}...");
                foreach (var descendant in thingCategory.DescendantThingDefs)
                {
                    if (ThingParentCategories.ContainsKey(descendant))
                    {
                        var parents = ThingParentCategories[descendant];
                        if (!parents.Any(parent => flavorCategory.ThisAndDescendants.Contains(parent))) ThingParentCategories[descendant].Add(flavorCategory);
                    }
                    else ThingParentCategories.Add(descendant, [flavorCategory]);
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

    private static Dictionary<FlavorCategoryDef, int> GetBestFlavorCategory(ThingDef searchedDef, FlavorCategoryDef topLevelCategory, int minMealsWithCompFlavorScore = goodScoreForCategorization)
    {
        //tag = searchedDef.defName.ToLower().Contains("stew");
        if (tag) { Log.Message("------------------------"); Log.Warning($"Finding correct Flavor Category for {searchedDef.defName}"); }

        List<string> splitNames = ExtractNames(searchedDef);
        int categoryScore = 0;
        Dictionary<FlavorCategoryDef, int> bestFlavorCategories = [];
        var splitNamesBlackList = splitNames;  // blacklist always stays based on original Def defName and label
        var categoriesToSearch = topLevelCategory.ThisAndDescendants.ToList();
        List<FlavorCategoryDef> categoriesToSkip = [.. FlavorCategoryDefOf.FT_Root.ThisAndDescendants.Where(cat => !cat.blacklistedMods.Contains(searchedDef.modContentPack.PackageId))];
        Log.Warning($"{searchedDef.ToStringSafe()} will skip categories [{categoriesToSkip.ToStringSafeEnumerable()}] due to being from a blacklisted mod");

        try
        {
            if (tag) { Log.Message($"Getting BestFlavorCategory for {searchedDef.defName}"); }

            // look in each category and record its score if above 0
            for (var i = 0; i < categoriesToSearch.Count; i++)
            {
                var flavorCategory = categoriesToSearch[i];
                if (!categoriesToSkip.Contains(flavorCategory) && !bestFlavorCategories.ContainsKey(flavorCategory))
                {
                    GetKeywordScores(flavorCategory);
                    if (categoryScore > 0) bestFlavorCategories.Add(flavorCategory, categoryScore);
                }
            }

            // if the best category was FT_MealsWithCompFlavor but its score wasn't high enough or Dynamic Meal Incorporation setting is off, put the thing in FT_FoodMeals
            if (bestFlavorCategories.Count > 0)
            {
                var bestCategory = bestFlavorCategories.MaxBy(element => element.Value);
                {
                    if (FlavorCategoryDefOf.FT_MealsWithCompFlavor.ContainedInThisOrDescendant(bestCategory.Key))
                    {
                        if (bestCategory.Value < minMealsWithCompFlavorScore || !FlavorTextSettings.dynamicMealIncorporation)
                        {
                            bestFlavorCategories.Remove(bestCategory.Key);
                            bestFlavorCategories.SetOrAdd(FlavorCategoryDef.Named("FT_FoodMeals"), bestCategory.Value);
                        }
                    }
                }
            }

            //TODO: can you allow getting a CompFlavor via this method, maybe if the match is strong enough combined with the defName/label?
            // if you couldn't find any categories, try using the Def's original parent categories as the search keywords
            // this strategy forbids allowing the item to get a CompFlavor, to avoid overriding specialized modded meals
            if (bestFlavorCategories.Count == 0)
            {
                categoriesToSearch.RemoveAll(cat => FlavorCategoryDefOf.FT_MealsWithCompFlavor.ThisAndDescendants.Contains(cat));
                ThingCategoryDef topLevelThingCategoryDef = !topLevelCategory.sisterCategories.Empty()
                    ? topLevelCategory.sisterCategories.First()
                    : null;

                var defParents = searchedDef.thingCategories?.Where(cat => cat != null && cat.Parents.Contains(topLevelThingCategoryDef)).ToList();
                while (true)
                {
                    if (defParents.NullOrEmpty()) break;
                    if (tag) Log.Warning($"{searchedDef.defName} had parent categories [{defParents.ToStringSafeEnumerable()}]");

                    foreach (ThingCategoryDef defParent in defParents)
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
            if (tag) Log.Warning($"keywords for {flavorCategory}");
            List<string> keywords = flavorCategory.keywords;
            foreach (string keyword in keywords)
            {
                categoryScore += ScoreKeyword(splitNames, keyword);
            }

            if (categoryScore < 3) return;

            // check blacklist, if score is too low after doing so, remove that FlavorCategory and its descendants from the list of categories to search
            if (tag) Log.Warning($"blacklist for {flavorCategory}");
            List<string> blacklist = flavorCategory.blacklist;
            foreach (string black in blacklist)
            {
                categoryScore -= 2 * ScoreKeyword(splitNames, black);
            }
            if (categoryScore >= 3) return;
            foreach (var cat in flavorCategory.ThisAndDescendants)
            {
                categoriesToSkip.AddDistinct(cat);
            }
        }
    }

    // see how well the keyword fits into splitNames: element matches keyword exactly, element starts or ends with keyword, element contains keyword, keyword phrase is present in combined splitNames
    private static int ScoreKeyword(List<string> splitNames, string keyword)
    {
        bool tag2 = false; //splitNames.Contains("octopus") && keyword.Contains("egg");
        int keywordScore = 0;

        foreach (string name in splitNames)
        {
            // contains: +1 to score each time if the keyword matches any part of an element in splitNames (e.g. 2x 'ump' in [pumpkin, orange, smoothie, sugar, pumpkins]
            if (tag2) Log.Message($"checking if {name} contains {keyword}");
            if (!name.Contains(keyword)) continue;
            keywordScore += 1;
            if (tag) Log.Message($"+1 to {name} contains {keyword}");

            // start/end: +2 to score each time if the keyword matches the start or end of an element in splitNames (e.g. 2x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins]
            if (tag2) Log.Message($"checking if {name} starts/ends with {keyword}");
            if (!(name.StartsWith(keyword) || name.EndsWith(keyword))) continue;
            keywordScore += 1;
            if (tag) Log.Message($"+1 to {name} starts/ends with {keyword}");

            // exact: +3 to score each time the keyword matches an element exactly in splitNames (e.g. 1x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins])
            if (tag2) Log.Message($"checking if {name} == {keyword}");
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
                if (tag2) Log.Message($"checking if {joinedNames} has substring {keyword}");
                if (joinedNames.Substring(i, keyword.Length) == keyword)
                {
                    count++;
                    if (tag) Log.Message($"+6 to {joinedNames} substring {keyword}");
                }
            }
            keywordScore += 6 * count;
        }
        tag2 = false;
        return keywordScore;
    }

    // calculate the lowest category containing all the categories in the given list
    // no need to include disallowed categories b/c those should always be a subcategory of a valid category
    internal static FlavorCategoryDef FindLowestCommonCategory(List<FlavorCategoryDef> categoryList)
    {

        // compare the corresponding elements of each sublist, starting with the one with the fewest elements
        // if they are no longer equal, then the previous element was the lowest common category
        FlavorCategoryDef commonCategory = FlavorCategoryDefOf.FT_Foods;

        if (!categoryList.NullOrEmpty())
        {
            var categoryListWithParents = categoryList.Select(cat => cat.ThisAndParents.ToList()).ToList();

            int min = (from List<FlavorCategoryDef> sublist in categoryListWithParents select sublist.Count).Min();
            var first = categoryListWithParents[0].ToList();
            for (int i = 0; i < min; i++)
            {
                if (categoryListWithParents.All(subList => subList[subList.Count - 1 - i] == first[first.Count - 1 - i]))
                {
                    commonCategory = first[first.Count - 1 - i];
                    continue;
                }
                break;
            }
        }
        return commonCategory;
    }
}


