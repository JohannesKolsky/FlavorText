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
//DONE: you can remove all categories with 0 contained ThingDefs to boost GetFlavorText search speed
//DONE: create a function for searching child and parent categories
//DONE: hitting the blacklist isn't triggering the removal of the category and its descendants
//DONE: if defName and label are different, the meal is never categorized: e.g. DankPyon_Slop_Simple (stew)

//TODO: examine more items that may be food: anything that has nutrition (drugs, alcohol)
//TODO: something like DankPyon_MealRations only scores 4 and isn't CompFlavored // re-add inheriting parent category scores?

namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for FlavorText-related FlavorCategoryDefs
/// </summary>
[StaticConstructorOnStartup]
internal static class CategoryUtility
{
    private static bool tag;  // DEBUG

    private static Dictionary<ThingDef, List<FlavorCategoryDef>> thingParentCategories = [];
    private static Dictionary<ThingDef, List<FlavorCategoryDef>> mealsQualities = [];
    private const int goodScoreForCategorization = 5;

    internal static Dictionary<ThingDef, List<FlavorCategoryDef>> ThingParentCategories { get => thingParentCategories; set => thingParentCategories = value; }
    internal static Dictionary<ThingDef, List<FlavorCategoryDef>> MealsQualities { get => mealsQualities; set => mealsQualities = value; }

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

/*    private static void Reinitialize()
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
/*        var test = DefDatabase<FlavorDef>.GetNamed("FlavorText_Foods_Jjigae");
        Log.Warning($"{test.ToStringSafe()} had slots [{test.Ingredients.Select(slot => "[" + slot.AllowedCategories.ToStringSafe() + " : " + slot.AllowedThingDefs.ToStringSafeEnumerable() + "]").ToStringSafeEnumerable()}]");*/
        int count = FlavorCategoryDefOf.FT_Root.DescendantThingDefs.Count();
        Log.Warning($"found {count} food items");
        foreach (var thing in FlavorCategoryDefOf.FT_Root.DescendantThingDefs)
        {
            tag = thing.defName.ToLower().Contains("egg");
            if (tag) Log.Message($">{thing.defName} with parent categories [{ThingParentCategories[thing].Select(parent => $"{parent.ToStringSafe()}").ToStringSafeEnumerable()}]");
        }
    }

    // for FT_Categories, inherit mod extension variables from parent where appropriate
    private static void InheritParentData()
    {
        foreach (FlavorCategoryDef cat in FlavorCategoryDefOf.FT_Root.DescendantCategories)
        {
            if (cat.Parents.NullOrEmpty()) throw new NullReferenceException($"{cat.ToStringSafe()} parents were null or empty when attempting to inherit parent data");
            if (cat.SingularCollective == null)
            {
                if (cat.Parents.All(parent => parent.SingularCollective == cat.Parents[0].SingularCollective))
                {
                    cat.SingularCollective = cat.Parents[0].SingularCollective;
                }
                else throw new ArgumentException($"the parents of {cat.ToStringSafe()} did  not have matching singularCollective field values. The values were [{cat.Parents.Select(parent => parent.SingularCollective.ToStringSafe()).ToStringSafeEnumerable()}]");
            }
            //Log.Error($"{cat.ToStringSafe()} had singularCollective = {cat.singularCollective.ToStringSafe()}");

            // inherit alwaysUseOverride if null in child; ignore null parent values
            if (cat.AlwaysUseOverride == null)
            {
                List<FlavorCategoryDef> nonNullParents = [.. cat.parents.Where(parent => parent.AlwaysUseOverride != null)];
                if (nonNullParents.Any() && nonNullParents.All(p => p.AlwaysUseOverride == nonNullParents[0].AlwaysUseOverride))
                {
                    cat.AlwaysUseOverride = nonNullParents[0].AlwaysUseOverride;
                }
                else throw new ArgumentException($"for their alwaysUseOverride field, the parents of {cat.ToStringSafe()} had both true and false values, or had all null values. The values were [{cat.Parents.Select(parent => parent.AlwaysUseOverride.ToStringSafe()).ToStringSafeEnumerable()}]");
            }
            //Log.Message($"{cat.ToStringSafe()} had alwaysUseOverride = {cat.alwaysUseOverride.ToStringSafe()}");

            if (cat.InflectionsOverride == null && cat.AlwaysUseOverride == true)
            {
                if (cat.Parents.All(parent => parent.InflectionsOverride == cat.Parents[0].InflectionsOverride))
                {
                    cat.InflectionsOverride = cat.Parents[0].InflectionsOverride;
                }
                else throw new ArgumentException($"the parents of {cat.ToStringSafe()} did  not have matching InflectionsOverride field values. The values were [{cat.Parents.Select(parent => parent.InflectionsOverride.ToStringSafe()).ToStringSafeEnumerable()}]");
            }
            //Log.Message($"{cat.ToStringSafe()} had inflectionsOverride = [{cat.inflectionsOverride.ToStringSafeEnumerable()}]");

            cat.Parents.ForEach(parent => cat.blacklist.AddRangeUnique(parent.blacklist));  // inherit blacklists of parents
            cat.Parents.ForEach(parent => cat.BlacklistedMods.AddRangeUnique(parent.BlacklistedMods));  // inherit blacklisted mods of parents
        }
    }

    // assign all ThingDefs and FlavorCategoryDefs in "Foods" to the best FT_ThingCategory
    // add CompFlavor to appropriate meals
    private static void AssignToFlavorCategories()
    {
        // TODO: FT_Root is null on Reinitialize
        // look in FlavorCategories and add any predefined ThingDefs and ThingCategoryDef contents to the FlavorCategory
        AbsorbChildrenFromXML();

        // add everything else in vanilla "Foods" to the item FlavorCategoryDef dictionary
        foreach (ThingDef food in ThingCategoryDef.Named("Foods").DescendantThingDefs.Distinct())
        {
            try
            {
                if (!food.IsHumanFood()) continue;
                if (ThingParentCategories.ContainsKey(food)) continue;
                ThingParentCategories.Add(food, []);
                Dictionary<FlavorCategoryDef, int> newParents = null;
                List<FlavorCategoryDef> bestParentsList = null;
                
                newParents = GetBestFlavorCategory(food, FlavorCategoryDefOf.FT_Items);
                if (newParents.Count == 0) continue;  // skip if no parents found (e.g. ThingDef had null ModContentPack)
                int bestScore = newParents.Max(element => element.Value);
                if (bestScore >= 2 * goodScoreForCategorization)  // accept all parent categories with a high enough score
                {
                    bestParentsList = [.. newParents.Where(element => element.Value >= 2 * goodScoreForCategorization).Select(element => element.Key)];
                }
                else  // else accept the highest scored parent category
                {
                    bestParentsList = [.. newParents.Where(element => element.Value == bestScore).Select(element => element.Key)];
                    if (bestParentsList.Count > 1)
                    {
                        int deepestNestDepth = bestParentsList.Max(p => p.nestDepth);
                        bestParentsList = [.. bestParentsList.Where(p => p.nestDepth == deepestNestDepth)];
                    }
                }

                if (!bestParentsList.NullOrEmpty())
                {
                    foreach (FlavorCategoryDef newParent in bestParentsList)
                    {
                        ThingParentCategories[food].AddDistinct(newParent);
                        newParent.ChildThingDefs.AddDistinct(food);
                    }
                }
                
                if (ThingParentCategories.TryGetValue(food).Empty()) throw new ArgumentOutOfRangeException($"list of FlavorCategories for {food} in the ThingCategories dictionary was empty after searching all FlavorCategories.");
            }
            catch (Exception)
            {
                Log.Error($"{food?.ToStringSafe()} from mod {food?.modContentPack?.PackageId?.ToStringSafe()} had an error");
                throw;
            }
        }

        // if ThingDef should have CompFlavor
        foreach (var meal in FlavorCategoryDefOf.FT_MealsWithCompFlavor.DescendantThingDefs)
        {
            if (!meal.HasComp<CompIngredients>()) throw new InvalidOperationException($"{meal.ToStringSafe()} was categorized into FT_MealsWithCompFlavor but did not have CompIngredients");
            {
                meal.comps.Add(new CompProperties_Flavor());

                // add it to the database of meal qualities
                List<FlavorCategoryDef> qualityCats = [.. ThingParentCategories[meal].Where(cat => FlavorCategoryDefOf.FT_MealsQualities.ThisAndDescendants.Contains(cat))];
                foreach (var qualityCat in qualityCats)
                {
                    if (MealsQualities.ContainsKey(meal)) MealsQualities[meal].Add(qualityCat);
                    else MealsQualities.Add(meal, [qualityCat]);
                    ThingParentCategories[meal].Remove(qualityCat);
                }

                //TODO: is this redundant with flavorDef.mealKinds?
                // move meal quality categories to a special dictionary; if this means the meal has no regular categories left, add it to FT_MealsNormal
                if (ThingParentCategories[meal].Empty() || (FlavorTextSettings.laxRecipeMatching && ThingParentCategories[meal].Contains(FlavorCategoryDefOf.FT_MealsCooked)))
                {
                    ThingParentCategories[meal].Add(FlavorCategoryDefOf.FT_MealsNormal);
                    FlavorCategoryDefOf.FT_MealsNormal.ChildThingDefs.Add(meal);
                }
            }
        }


        List<ThingDef> allMealSourceBuildings = [.. DefDatabase<ThingDef>.AllDefs.Where(b => b.building is { isMealSource: true })];
        foreach (ThingDef building in allMealSourceBuildings)
        {
            try
            {
                if (ThingParentCategories.ContainsKey(building)) continue;
                ThingParentCategories.Add(building, []);
                var categories = ThingParentCategories.TryGetValue(building) ?? throw new NullReferenceException($"list of FlavorCategories for {building} in the ThingCategories dictionary was null.");
                if (categories.Empty())
                {
                    var newParents = GetBestFlavorCategory(building, FlavorCategoryDefOf.FT_CookingStations);
                    if (newParents.Count > 0)
                    {
                        var newParent = newParents.MaxBy(element => element.Value).Key;
                        ThingParentCategories[building].AddDistinct(newParent);
                        newParent.ChildThingDefs.Add(building);
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
        
        foreach (var childCat in root.ChildCategories)
        {
            PruneInactiveFlavorCategoriesRecursive(childCat);
        }
        
        if (root.DescendantThingDefs.Count == 0)
        {
            root.Parents.Clear();
            root.ChildCategories.Clear();
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
            foreach (var child in flavorCategory.ThingDefsToAbsorb)
            {
                if (ThingParentCategories.ContainsKey(child)) ThingParentCategories[child].AddDistinct(flavorCategory);
                else ThingParentCategories.Add(child, [flavorCategory]);
                flavorCategory.ChildThingDefs.Add(child);
            }
            foreach (var thingCategory in flavorCategory.thingCategoryDefsToAbsorb)
            {
                foreach (var descendant in thingCategory.DescendantThingDefs)
                {
                    if (ThingParentCategories.ContainsKey(descendant))
                    {
                        var parents = ThingParentCategories[descendant];
                        if (!parents.Any(parent => flavorCategory.ThisAndDescendants.Contains(parent))) ThingParentCategories[descendant].Add(flavorCategory);
                    }
                    else ThingParentCategories.Add(descendant, [flavorCategory]);
                    flavorCategory.ChildThingDefs.AddDistinct(descendant);
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
        //tag = searchedDef.defName.ToLower().Contains("lunch");
        if (tag) { Log.Message("------------------------"); Log.Warning($"Finding correct Flavor Category for {searchedDef.defName}"); }

        List<string> splitNames = ExtractNames(searchedDef);
        int categoryScore = 0;
        Dictionary<FlavorCategoryDef, int> bestFlavorCategories = [];
        var splitNamesBlackList = splitNames;  // blacklist always stays based on original Def defName and label
        var categoriesToSearch = topLevelCategory.ThisAndDescendants.ToList();
        if (searchedDef.modContentPack == null || searchedDef.modContentPack.PackageId == null) { Log.Warning($"{searchedDef.ToStringSafe()} did not have an associated ModContentPack or PackageId. Report this to that mod's creator."); return []; }
        List<FlavorCategoryDef> categoriesToSkip = [.. categoriesToSearch.Where(cat => cat.BlacklistedMods.Contains(searchedDef.modContentPack.PackageId))];  // skip categories that have that mod blacklisted

        try
        {
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
            if (tag) { Log.Message($"bestFlavorCategories for {searchedDef.defName} were [{bestFlavorCategories.Select(kvp => kvp.Key.ToStringSafe()).ToStringSafeEnumerable()}]"); }

            // if the best category was FT_MealsWithCompFlavor but its score wasn't high enough or Dynamic Meal Incorporation setting is off, put the thing in FT_FoodMeals
            if (bestFlavorCategories.Count > 0)
            {
                var bestCategory = bestFlavorCategories.MaxBy(element => element.Value);
                {
                    if (FlavorCategoryDefOf.FT_MealsWithCompFlavor.ContainedInThisOrDescendant(bestCategory.Key))
                    {
                        if (!searchedDef.HasComp<CompIngredients>() || bestCategory.Value < minMealsWithCompFlavorScore || !FlavorTextSettings.dynamicMealIncorporation)
                        {
                            bestFlavorCategories.Remove(bestCategory.Key);
                            bestFlavorCategories.SetOrAdd(FlavorCategoryDef.Named("FT_FoodMeals"), bestCategory.Value);
                            Log.Message($"{searchedDef.defName} had score of {bestCategory.Value.ToStringSafe()}");
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
                ThingCategoryDef topLevelThingCategoryDef = !topLevelCategory.SisterCategories.Empty()
                    ? topLevelCategory.SisterCategories.First()
                    : null;

                List<ThingCategoryDef> defParents = searchedDef.thingCategories?.Where(cat => cat != null && cat.Parents.Contains(topLevelThingCategoryDef)).ToList();
                while (!defParents.NullOrEmpty())
                {
                    if (tag) Log.Warning($"{searchedDef.ToStringSafe()} had parent categories [{defParents.ToStringSafeEnumerable()}]");

                    foreach (ThingCategoryDef defParent in defParents)
                    {
                        splitNames = ExtractNames(defParent);

                        foreach (var flavorCategory in categoriesToSearch)
                        {
                            // if the current flavor category being tested has a sister category, give it a flat score of 6
                            var sisterCategories = flavorCategory.SisterCategories;
                            if (sisterCategories != null && sisterCategories.Contains(defParent))
                            {
                                categoryScore = 6;
                            }

                            // otherwise do the normal keyword tests
                            else if (!bestFlavorCategories.ContainsKey(flavorCategory))
                            {
                                GetKeywordScores(flavorCategory);
                            }

                            if (categoryScore > 0)
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
            List<string> keywords = flavorCategory.Keywords;
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
    private static FlavorCategoryDef FindLowestCommonCategory(List<FlavorCategoryDef> categoryList)
    {

        // compare the corresponding elements of each sublist, starting with the one with the fewest elements
        // if they are no longer equal, then the previous element was the lowest common category
        FlavorCategoryDef commonCategory = FlavorCategoryDefOf.FT_Foods;

        if (!categoryList.NullOrEmpty())
        {
            var categoryListWithParents = categoryList.Select(cat => cat.ThisAndAncestors.ToList()).ToList();

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


