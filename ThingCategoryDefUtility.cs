using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

// Verse.ThingCategoryNodeDatabase.FinalizeInit() is what adds core stuff to ThingCategoryDef.childCategories

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
//--RELEASE: Flavor Text is still showing up in bills
//DONE: make patch that adds CompFlavor more precise
//DONE: sunflower seeds show up as sunflower
//DONE: allow for adding multiple FT_Categories at a time?
//DONE: VGE watermelon is in candy
//DONE: RC2 Chili peppers are in FT_Foods
//DONE: keep canned and pickled and such; problem is atm not deleting those causes "meat" to be deleted
//DONE: can you get link FT_MealsFlavor to FT_FoodMeals and not have this funkiness where you assign to the second and then check if it also belongs in the first?

namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for FlavorText-related ThingCategoryDefs
/// </summary>

[StaticConstructorOnStartup]
public static class ThingCategoryDefUtility
{

    internal static ThingCategoryDef FlavorRoot = ThingCategoryDef.Named("FT_Foods"); // topmost category used for getting flavor text; couple unused FT_Categories on top, then FT_Root

    internal static ThingCategoryDef MealsFlavor = ThingCategoryDef.Named("FT_MealsFlavor");

    private static bool tag;  // DEBUG

    internal static Dictionary<ThingDef, Tuple<string, string, string, string>> IngredientInflections = [];

    static ThingCategoryDefUtility()
    {
        Stopwatch stopwatch = new Stopwatch();
        stopwatch.Start();
        try
        {
            InheritParentModExtensions(); // FT_Categories inherit some data from parents
            SetNestLevelRecursive(ThingCategoryDef.Named("FT_Root").treeNode,
                0); // FT_Root is isolated so set its category nest levels manually
            //SetNestLevelRecursive(ThingCategoryDef.Named("FT_MealsFlavor").treeNode,
            //    0); // FT_MealsFlavor is also isolated
            
            AssignToFlavorCategories(); // assign all relevant ThingsDefs to a FlavorText ThingCategoryDef
           
            // can't do this until now, needs previous method and a built DefDatabase
            DefDatabase<ThingCategoryDef>.ResolveAllReferences();
            DefDatabase<FlavorDef>.ResolveAllReferences();


            FlavorDef.SetCategoryData(); // get total specificity for each FlavorDef; get other static data
            GetIngredientInflections();
            Debug();

        }
        catch (Exception ex)
        {
            Log.Error($"Error when setting up FT_ThingCategoryDefs for Flavor Text. Error: {ex}");
        }

        stopwatch.Stop();
        TimeSpan elapsed = stopwatch.Elapsed;
        if (Prefs.DevMode)
        {
            Log.Warning("[Flavor Text] ThingCategoryDefUtility ran in " + elapsed.ToString("ss\\.fffff") + " seconds");
        }

    }

    private static void GetIngredientInflections()
    {
        List<ThingDef> allIngredients = FlavorRoot.DescendantThingDefs.ToList();
        foreach (ThingDef ingredient in allIngredients)
        {
            Tuple<string, string, string, string> inflection = GenerateIngredientInflections(ingredient);
            // add to inflection dictionary
            if (!inflection.Item1.NullOrEmpty() && !inflection.Item2.NullOrEmpty() && !inflection.Item3.NullOrEmpty() && !inflection.Item4.NullOrEmpty())
            {
                /*Log.Message($"plur = {inflection.Item1}"); Log.Message($"coll = {inflection.Item2}"); Log.Message($"sing = {inflection.Item3}"); Log.Message($"adj = {inflection.Item4}");*/
                IngredientInflections.Add(ingredient, inflection);
            }
            else
            {
                Log.Error($"Failed to find an inflection for {ingredient.label}");
                Log.Message($"plur = {inflection.Item1}");
                Log.Message($"coll = {inflection.Item2}");
                Log.Message($"sing = {inflection.Item3}");
                Log.Message($"adj = {inflection.Item4}");
                throw new Exception("Failed to find an inflection for an FT_ThingCategoryDef during loading.");
            }
        }
    }

    private static void Debug()
    {
        /*foreach (var thing in DefDatabase<ThingDef>.AllDefs.Where(thing => DefDatabase<ThingCategoryDef>.GetNamed("FT_MeatRaw").ContainedInThisOrDescendant(thing)))
        {
            Log.Message($">{thing.defName} is in categories:");
            foreach (ThingCategoryDef category in thing.thingCategories)
            {
                Log.Message($"{category.defName}");
            }
        }*/
        
        /*        foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.ToList())
                {
                    if (thingDef.HasComp<CompFlavor>() && ThingCategoryDef.Named("FT_MealsFlavor").ContainedInThisOrDescendant(thingDef))
                    {
                        Log.Warning($">>>{thingDef.defName} has CompFlavor and is in FT_MealsFlavor");
                    }
                }*/
        
        /*foreach (var cat in DefDatabase<ThingCategoryDef>.AllDefs.Where(catDef => catDef.defName.Contains("FT_Meat")))
        {
            Log.Warning(cat.defName);
            foreach (ThingDef thingDef in cat.DescendantThingDefs)
            {
                Log.Message($"{thingDef.defName}");
            }
        }*/
    }

    // for FT_Categories, inherit mod extension variables from parent where appropriate
    public static void InheritParentModExtensions()
    {
        foreach (ThingCategoryDef cat in FlavorRoot.ThisAndChildCategoryDefs)
        {
            // inherit singularCollective field value from parent if it is null in child
            cat.GetModExtension<FlavorCategoryModExtension>().SingularCollective ??= cat.parent.GetModExtension<FlavorCategoryModExtension>().SingularCollective;
        }
    }

    // recalculate the nest depth of everything in FT_Root, since this wasn't automatically calculated for some reason (b/c they're modded?)
    private static void SetNestLevelRecursive(TreeNode_ThingCategory node, int nestDepth)
    {
        nestDepth += 1;
        foreach (ThingCategoryDef childCategory in node.catDef.childCategories)
        {
            childCategory.treeNode.nestDepth = nestDepth;
            SetNestLevelRecursive(childCategory.treeNode, nestDepth);
        }
    }

    // assign all ThingDefs and ThingCategoryDefs in "Foods" to the best FT_ThingCategory
    // add CompFlavor to appropriate meals
    public static void AssignToFlavorCategories()
    {
        // look at all sister categories and add their contents to FT_ThingCategoryDefs
        List<ThingCategoryDef> flavorCategoryDefs = FlavorRoot.ThisAndChildCategoryDefs.ToList();
        flavorCategoryDefs.RemoveDuplicates();
        foreach (var flavorCategoryDef in flavorCategoryDefs)
        {
            var categoriesToAbsorb = flavorCategoryDef.GetModExtension<FlavorCategoryModExtension>().CategoriesToAbsorb;

            categoriesToAbsorb
                .ForEach(cat => cat.childThingDefs
                    .ForEach(child => child.thingCategories.Add(flavorCategoryDef)));
        }
        
        // assign everything in vanilla "Foods" to an FT_Category
        List<ThingDef> flavorThingDefs = ThingCategoryDef.Named("Foods").DescendantThingDefs.ToList();
        flavorThingDefs.RemoveDuplicates();

        foreach (ThingDef food in flavorThingDefs)
        {
            if (food.thingCategories.Any(cat => cat.defName.StartsWith("FT_")))
            {
                Log.Message($"{food.defName} was already assigned to an FT_ThingCategoryDef among [{food.thingCategories.ToStringSafeEnumerable()}], skipping...");
                continue;
            }
            

            //tag = food.defName.ToLower().Contains("");
            List<string> splitNames = ExtractNames(food);
            ThingCategoryDef newParent = GetBestFlavorCategory(splitNames, food, FlavorRoot);

            if (tag) { Log.Message($"!!! found new parent {newParent.defName}"); }

            food.thingCategories ??= [];
            if (newParent != null)
            {
                if (!food.thingCategories.Contains(newParent)) 
                { 
                    food.thingCategories.Add(newParent);
                    food.ResolveReferences();
                }
                if (!newParent.childThingDefs.Contains(food)) { newParent.childThingDefs.Add(food); }

                // if ThingDef should have CompFlavor, postpend a new one
                if (food.HasComp<CompIngredients>() && (newParent == MealsFlavor || newParent.Parents.Contains(MealsFlavor)))
                {
                    food.comps.Add(new CompProperties_Flavor());
                }
            }

            tag = false;
        }
        // repeat for buildings, but use Buildings as the root, and don't add any CompFlavors
        List<ThingDef> cookingStationThingDefs = DefDatabase<ThingDef>.AllDefs.Where(b => b.building is { isMealSource: true }).ToList();  // all buildings that are a meal source
        cookingStationThingDefs.RemoveDuplicates();

        foreach (ThingDef building in cookingStationThingDefs)
        {
            //tag = building.defName.ToLower().Contains("pot");
            List<string> splitNames = ExtractNames(building);
            ThingCategoryDef newParent = GetBestFlavorCategory(splitNames, building, ThingCategoryDef.Named("FT_Buildings"));
            
            building.thingCategories ??= [];
            if (newParent != null)
            {
                if (!building.thingCategories.Contains(newParent)) { building.thingCategories.Add(newParent); }
                if (!newParent.childThingDefs.Contains(building)) { newParent.childThingDefs.Add(building); }
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

    private static ThingCategoryDef GetBestFlavorCategory(List<string> splitNames, ThingDef searchedDef, ThingCategoryDef topLevelCategory, int minMealsFlavorScore = 6)
    {
        if (tag) { Log.Message("------------------------"); Log.Warning($"Finding NewParent for {searchedDef.defName}"); }

        int categoryScore = 0;
        int bestCategoryScore = 0;
        ThingCategoryDef bestFlavorCategory = null;
        var splitNamesBlackList = splitNames;  // blacklist always stays based on original Def defName and label
        var categoriesToSearch = topLevelCategory.ThisAndChildCategoryDefs.ToList();
        
        try
        {
            if (tag) { Log.Message($"Getting BestFlavorCategory for {searchedDef.defName}"); }

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < categoriesToSearch.Count; i++)
            {
                var flavorCategory = categoriesToSearch[i];
                GetKeywordScores(flavorCategory);
                CompareCategoryScores(flavorCategory);
            }

            // if the best category was FT_MealsFlavor but its score wasn't high enough, choose FT_FoodMeals as the best category instead
            if (bestFlavorCategory == MealsFlavor && bestCategoryScore < minMealsFlavorScore)
            {
                bestFlavorCategory = ThingCategoryDef.Named("FT_FoodMeals");
            }

            // if nothing matched, try using the Def's vanilla parent categories as the search keywords
            if (bestFlavorCategory == null)
            {
                //{ Log.Error($"No category found for {searchedDef.defName}, looking at its parent categories"); }
                
                var topLevelVanillaCategory = topLevelCategory.GetModExtension<FlavorCategoryModExtension>().VanillaSisterCategory ?? throw new NullReferenceException($"{topLevelCategory.defName} had no vanilla sister category. Please report.");
                var defParents = searchedDef.thingCategories?.Where(cat => cat != null && cat.Parents.Contains(topLevelVanillaCategory)).ToList();
                while (true)
                {
                    if (defParents.NullOrEmpty()) break;
                    //{ Log.Warning($"{searchedDef.defName} had parent categories [{defParents.ToStringSafeEnumerable()}]"); }

                    foreach (ThingCategoryDef defParent in defParents)
                    {
                       
                        splitNames = ExtractNames(defParent);
                        
                        foreach (var flavorCategory in categoriesToSearch)
                        {
                            // if the current flavor category being tested has a sister category, give it a flat score of 3
                            var sisterCategories = flavorCategory.GetModExtension<FlavorCategoryModExtension>().CategoriesToAbsorb;
                            if (flavorCategory.GetModExtension<FlavorCategoryModExtension>().VanillaSisterCategory != null)
                            {
                                sisterCategories = sisterCategories.Prepend(flavorCategory.GetModExtension<FlavorCategoryModExtension>().VanillaSisterCategory).ToList();
                            }

                            if (sisterCategories.Contains(defParent))
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

                    defParents = defParents
                        .Where(cat => cat != topLevelVanillaCategory)
                        .Select(cat => cat.parent)
                        .Where(parent => parent != null)
                        .ToList();
                    defParents.RemoveDuplicates();
                }
                if (bestFlavorCategory != null & Prefs.DevMode) Log.Warning($"Using its parent categories, found new best flavor category {bestFlavorCategory.defName} for Def {searchedDef.defName}");
            }

            if (bestFlavorCategory != null) return bestFlavorCategory;
            
            bestFlavorCategory = topLevelCategory;
            Log.Warning($"Could not find appropriate FT_ThingCategoryDef for {searchedDef.defName}, using {topLevelCategory.defName} instead.");
            return bestFlavorCategory;
        }
        catch (Exception ex)
        {
            Log.Error($"error testing {searchedDef.defName}: {ex}");
            return null;
        }

        void GetKeywordScores(ThingCategoryDef flavorCategory)
        {
                // get a score based on how well the flavorCategory keywords match the searchedDef's names
                categoryScore = 0;
                List<string> keywords = flavorCategory.GetModExtension<FlavorCategoryModExtension>().Keywords;
                foreach (string keyword in keywords)
                {
                    categoryScore += ScoreKeyword(splitNames, keyword);
                }
                List<string> blacklist = flavorCategory.GetModExtension<FlavorCategoryModExtension>().Blacklist;
                foreach (string black in blacklist)
                {
                    // if blacklist was triggered, remove flavorCategory and all its descendants from the categories to search in
                    if (ScoreKeyword(splitNamesBlackList, black) >= 3)
                    {
                        categoriesToSearch.RemoveAll(cat => cat == flavorCategory || cat.Parents.Contains(flavorCategory));
                        return;
                    }
                }
            
        }

        void CompareCategoryScores(ThingCategoryDef flavorCategory)
        {
            if (categoryScore >= 1)
            {
                if (tag) { Log.Message($"Found matching category {flavorCategory} with score of {categoryScore} and nest depth of {flavorCategory.treeNode.nestDepth}"); }
                if (bestFlavorCategory == null)
                {
                    bestCategoryScore = categoryScore;
                    bestFlavorCategory = flavorCategory;
                    if (tag) { Log.Message($"->Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.treeNode.nestDepth}"); }
                }
                else if (flavorCategory.Parents.Contains(bestFlavorCategory))  // if subcategory, add scores together
                {
                    bestCategoryScore += categoryScore;
                    bestFlavorCategory = flavorCategory;
                    if (tag) { Log.Message($"->Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.treeNode.nestDepth}"); }
                }
                else if (categoryScore > bestCategoryScore || categoryScore == bestCategoryScore && flavorCategory.treeNode.nestDepth > bestFlavorCategory.treeNode.nestDepth)
                {
                    bestCategoryScore = categoryScore;
                    bestFlavorCategory = flavorCategory;
                    if (tag) { Log.Message($"->Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.treeNode.nestDepth}"); }
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

            // start/end: +2 to score each time if the keyword matches the start or end of an element in splitNames (e.g. 2x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins]
            if (!(name.StartsWith(keyword) || name.EndsWith(keyword))) continue;
            keywordScore += 1;
            
            // exact: +3 to score each time the keyword matches an element exactly in splitNames (e.g. 1x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins])
            if (name != keyword) continue;
            keywordScore += 1;
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
                }
            }
            keywordScore += 6 * count;
        }
        return keywordScore;
    }

    // get singular and plural forms of each ingredient
    public static Tuple<string, string, string, string> GenerateIngredientInflections(ThingDef ingredient)
    {
        tag = false;
        /*if (ingredient.defName.ToLower().Contains("egg")) { tag = true; }*/
        // plural form // a dish made of CABBAGES that are diced and then stewed in a pot
        // collective form, singular/plural ending depending in real-life ingredient size // stew with CABBAGE  // stew with PEAS
        // singular form // a slice of BRUSSELS SPROUT
        // adjectival form // PEANUT brittle

        GetInflections(ingredient, out var plur, out var coll, out var sing, out var adj);

        Tuple<string, string, string, string> inflections = Tuple.Create(plur, coll, sing, adj);
        return inflections;

        // determine correct inflections for plural, collective, singular, and adjectival forms of the ingredient's label
        static void GetInflections(ThingDef ingredient, out string plur, out string coll, out string sing, out string adj)
        {
            if (ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_MeatRaw")))
            {
                switch (ingredient.defName)
                {
                    case "Meat_Twisted":
                        {
                            plur = "twisted flesh";
                            coll = plur;
                            sing = coll;
                            adj = "twisted";
                            return;
                        }
                    case "Meat_Human":
                        {
                            plur = "long pork";
                            coll = plur;
                            sing = coll;
                            adj = "cannibal";
                            return;
                        }
                    case "Meat_Megaspider":
                        {
                            plur = "bug guts";
                            coll = plur;
                            sing = "bug";
                            adj = sing;
                            return;
                        }
                }
            }
            if (ingredient.defName == "RawFungus")
            {
                plur = "mushrooms";
                coll = plur;
                sing = "mushroom";
                adj = sing;
                return;
            }

            // manual VG Vegetable Garden, since their defNames are singular
            if (ingredient.defName == "Rawmushroom")
            {
                plur = "mushrooms";
                coll = plur;
                sing = "mushroom";
                adj = sing;
                return;
            }
            if (ingredient.defName == "Rawbean")
            {
                plur = "beans";
                coll = plur;
                sing = "bean";
                adj = sing;
                return;
            }
            if (ingredient.defName == "Rawsnowbeet")
            {
                plur = "beets";
                coll = plur;
                sing = "beet";
                adj = sing;
                return;
            }
            if (ingredient.defName == "RawOlive")
            {
                plur = "olives";
                coll = plur;
                sing = "olive";
                adj = sing;
                return;
            }
            if (ingredient.defName == "RawRedLentil")
            {
                plur = "lentils";
                coll = plur;
                sing = "lentil";
                adj = sing;
                return;
            }

            if (ingredient.defName == "VCE_RawSunflower")  // sunflower seeds, not sunflowers
            {
                plur = "sunflower seeds";
                coll = plur;
                sing = "sunflower seed";
                adj = sing;
                return;
            }

            if (ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_BrusselsSprout"))) // capitalize Brussels sprouts
            {
                plur = "Brussels sprouts";
                coll = plur;
                sing = "Brussels sprout";
                adj = sing;
            }

            if (ingredient.defName == "VCE_RawSpices")
            {
                plur = "spices";
                coll = plur;
                sing = "spice";
                adj = "spicy";
            }

            // if ingredient isn't a special def from above, attempt to generate accurate inflections
            else
            {
                List<(string, string)> singularPairs = [("ies$", "y"), ("sses$", "ss"), ("us$", "us"), ("([aeiouy][cs]h)es$", "$1"), ("([o])es$", "$1"), ("([^s])s$", "$1")];  // English conversions from plural to singular noun endings

                string label = ingredient.label;
                if (tag) { Log.Warning($">>>starting label is {label}"); }
                string labelCapDiacritic = label;
                string defNameCompare = ingredient.defName;
                if (tag) { Log.Warning($">>>starting defName is {defNameCompare}"); }
                defNameCompare = Regex.Replace(defNameCompare, "([_])", " ");  // remove spacer chars

                // remove diacritics and capitalization
                string labelCompare = Remove.RemoveDiacritics(labelCapDiacritic);
                labelCompare = labelCompare.ToLower();
                defNameCompare = Remove.RemoveDiacritics(defNameCompare);
                defNameCompare = Regex.Replace(defNameCompare, "(?<=[a-zA-Z])([A-Z][a-z]+)", " $1");  // split up name based on capitalized words
                defNameCompare = Regex.Replace(defNameCompare, "(?<=[a-z])([A-Z]+)", " $1");  // split up names based on unbroken all-caps sequences
                if (tag) { Log.Message($"split up defName, is now {defNameCompare}"); }
                defNameCompare = defNameCompare.ToLower();

                // remove unnecessary bits
                List<string> delete = ["meal", "leaf", "leaves", "stalks*", "seeds*", "cones*", "eggs*", "meat"];  // bits to delete

                // don't remove "meat" from certain generic meat labels
                List<string> excludedCombinations = ["canned meat", "pickled meat", "dried meat", "dehydrated meat", "salted meat", "trimmed meat", "cured meat", "prepared meat", "marinated meat", "bone meat"];
                foreach (string combination in excludedCombinations)
                {
                    if (labelCompare == combination)
                    {
                        delete.Remove("meat");
                        break;
                    }
                }

                // remove bits
                string temp;
                foreach (string del in delete)
                {
                    temp = Regex.Replace(labelCompare, $@"(?i)\b{del}\b", "").Trim();  // delete complete words from labelCompare that match those in "delete"
                    if (Regex.IsMatch(temp, "[a-zA-Z]")) { labelCompare = temp; }  // accept deletion from labelCompare if letters remain
                    if (tag) { Log.Message($"deleted bit from labelCompare, is now {labelCompare}"); }

                    temp = Regex.Replace(defNameCompare, $@"(?i)\b{del}\b", "").Trim();  // delete complete words from defNameCompare that match those in "delete"
                    if (Regex.IsMatch(temp, "[a-zA-Z]")) { defNameCompare = temp; }  // accept deletion from defNameCompare if letters remain
                    if (tag) { Log.Message($"deleted bit from defNameCompare, is now {defNameCompare}"); }
                }

                //TODO: this is separate b/c it doesn't work with the list for some reason; probably some conflict between how C# and Regex read strings
                temp = Regex.Replace(labelCompare, @"\(.*\)", "").Trim();
                if (Regex.IsMatch(temp, "[a-zA-Z]")) { labelCompare = temp; }  // accept deletion from label if letters remain
                if (tag) { Log.Message($">removed parentheses to make: {labelCompare}"); }

                // formulate inflections by comparing label and defName; if you're unable to, use the label
                string root = LongestCommonSubstring(defNameCompare, labelCompare);  // e.g. VCE_RawPumpkin + mammoth gold pumpkins => pumpkin
                if (tag) { Log.Warning($"Longest common substring was {root}"); }

                // eggs are a special case b/c label isn't plural and it's a different order from the defName
                if (ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_Egg")))
                {
                    plur = $"{root} eggs";
                    coll = plur;
                    sing = $"{root} egg";
                    adj = sing;
                    return;
                }

                //try to get plural form from label // you can't just rely on checking -s endings b/c meat will never end in -s  // you can't just rely on label b/c it might have unnecessary words (e.g. "mammoth gold" pumpkins)
                if (root != null && !delete.Contains(root) && Regex.IsMatch(labelCompare, $"\\b{root}"))  // make sure root isn't some generic term like "meat", and that it starts at the start of a word
                {

                    // extend the overlap to the end of a word in label // mammoth gold pumpkins + pumpkin => pumpkins
                    Match match = Regex.Match(labelCompare, "(?i)" + root + "[^ ]*");
                    plur = match.Value;
                    if (tag) { Log.Message($"plural matched = {plur}"); }
                    int head = labelCompare.IndexOf(root, StringComparison.Ordinal);
                    plur = labelCapDiacritic.Substring(head, plur.Length);  // get diacritics and capitalization back

                    if (tag) { Log.Message($"plural final = {plur}"); }

                    // if the 2 forms differ by more than 2 letters, discard them and use reduced label
                    if (root.Length == 0 || root.Length < plur.Length - 2)
                    {
                        if (tag) { Log.Message($"root was {root}"); }
                        plur = labelCapDiacritic;
                        if (tag) { Log.Message($"plural fallback = {plur}"); }
                    }


                }

                // use reduced label
                else
                {
                    plur = labelCapDiacritic;
                    if (tag) { Log.Message($"plural fallback2 = {plur}"); }
                }

                // try to get singular form from plural form
                // done this way so that plural form matches singular form if label and defName aren't similar (e.g. VCE_Oranges = mandarins when other mods are installed)
                sing = plur;
                foreach (var pair in singularPairs)
                {
                    if (Regex.IsMatch(sing, pair.Item1))
                    {
                        sing = Regex.Replace(sing, pair.Item1, pair.Item2);
                        break;
                    }
                }
                if (tag) { Log.Message($"sing = {sing}"); }

                // try to get collective form (either based on singular or plural depending on FT_Category)
                ThingCategoryDef parentCategory = ingredient.thingCategories.Find(cat => cat.defName.StartsWith("FT_"));
                bool? singularCollective = parentCategory.GetModExtension<FlavorCategoryModExtension>().SingularCollective;
                coll = singularCollective == true ? sing : plur;
                if (tag) { Log.Message($"coll = {coll}"); }

                // try to get adjectival form (based on singular)
                adj = sing;
            }
        }

        static string LongestCommonSubstring(string string1, string string2)
        {
            try
            {
                {
                    // find the overlap
                    int[,] a = new int[string1.Length + 1, string2.Length + 1];
                    int row = 0;    // s1 index
                    int col = 0;    // s2 index

                    for (var i = 0; i < string1.Length; i++)
                        for (var j = 0; j < string2.Length; j++)
                            if (string1[i] == string2[j])
                            {
                                int len = a[i + 1, j + 1] = a[i, j] + 1;
                                if (len > a[row, col])
                                {
                                    row = i + 1;
                                    col = j + 1;
                                }
                            }

                    string root = string1.Substring(row - a[row, col], a[row, col]).Trim();
                    if (tag) { Log.Message($"Longest common substring for *{string1}* and *{string2}* was *{root}*"); }
                    return root;

                }

            }

            catch (Exception ex)
            {
                Log.Error($"Error finding inflections of ${string2}: {ex}");
                throw;
            }
        }
    }

    // look in the FinalFlavorDefs for any that have a category that isn't defined in FlavorText; make it into a new category and assign it keywords and such
    /*        static void GenerateAdHocCategories()
            {
                foreach (FlavorDef flavorDef in FinalFlavorDefs)
                {
                    foreach (IngredientCount ing in flavorDef.ingredient)
                    {
                        List<string> filterCatStrings = flavorDef.GetFilterCategories(ing.filter);
                        foreach (string filterCatString in filterCatStrings)
                        {
                            ThingCategoryDef adHocThingCategoryDef = (false ? (DefDatabase<ThingCategoryDef>.GetNamed(filterCatString, errorOnFail: false) ?? new ThingCategoryDef()) : new ThingCategoryDef());
                            adHocThingCategoryDef.defName = filterCatString;
                            adHocThingCategoryDef.label = filterCatString;
                            // generate parent
                            adHocThingCategoryDef.GetModExtension<FlavorCategoryModExtension>().keywords.Add(filterCatString);
                        }
                    }
                }
            }*/
}


