using LinqToDB.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

// Verse.ThingCategoryNodeDatabase.FinalizeInit() is what adds core stuff to ThingCategoryDef.childCategories

//--TODO: poultry eggs show up in FT_Poultry
//--TODO: FT_Poultry and FT_Sheep aren't getting their relevant thingdefs
//--TODO: some drugs are being tested for some reason
//--TODO: VCE_Condiments are going all over
//--TODO: "FoodRaw" should be default "this is an ingredient that adds calories"; "Foods" covers condiments and such xxx this would require another level, which isn't worth it
//--TODO: VCE_bakes aren't finding the flavorDef despite matching ones found, even if it's just flour
//--TODO: fertilized eggs are ending up in FT_EggUnfertilized (cause "egg" keyword?)
//--TODO: VCE_canned meat isn't finding any flavor defs
//--TODO: VCE_canned fruit is in FT_Meals
//--TODO: VCE_canned eggs are in FT_Meals
//--TODO: get FT to put VCE_canned into meats/vegetables/fruits/etc
//--TODO: VGP defNames are sing: bean, lentil, beet
//xxTODO: if an ingredient has subingredients, use the subingredients instead of the main ingredient (VCE canned stuff, GAB pickled stuff, meals, etc)
//--RELEASE: Flavor Text is still showing up in bills
//--TODO: make patch that adds CompFlavor more precise
//--TODO: sunflower seeds show up as sunflower
//--TODO: allow for adding multiple FT_Categories at a time?
//--TODO: VGE watermelon is in candy
//--TODO: RC2 Chili peppers are in FT_Foods

namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for FlavorText-related ThingCategoryDefs
/// </summary>

[StaticConstructorOnStartup]
public static class ThingCategoryDefUtilities
{

    public static ThingCategoryDef flavorRoot = ThingCategoryDef.Named("FT_Foods"); // topmost category used in FlavorText calculations; couple unused FT_Categories on top, then FT_Root

    public static bool tag = false;  // DEBUG

    public static Dictionary<ThingDef, Tuple<string, string, string, string>> ingredientInflections = [];

    static ThingCategoryDefUtilities()
    {
        InheritParentModExtensions();  // FT_Categories inherit some data from parents
        SetNestLevelRecursive(ThingCategoryDef.Named("FT_Root").treeNode, 0);  // FT_Root is isolated so set its category nest levels manually
        SetNestLevelRecursive(ThingCategoryDef.Named("FT_MealsFlavor").treeNode, 0); // FT_MealsFlavor is also isolated
        AssignToFlavorCategories();  // assign all relevant ThingsDefs to a FlavorText ThingCategoryDef
        DefDatabase<ThingCategoryDef>.ResolveAllReferences();
        SetCategorySpecificities();  // get specificity for each FT_ThingCategory; can't do this until now, needs previous 2 methods and a built DefDatabase
        FlavorDef.SetStaticData(); // get total specificity for each FlavorDef; get other static data
        GetIngredientInflections();
        Debug();
    }

    private static void GetIngredientInflections()
    {
        List<ThingDef> allingredients = flavorRoot.DescendantThingDefs.ToList();
        foreach (ThingDef ingredient in allingredients)
        {
            Tuple<string, string, string, string> inflection = GenerateIngredientInflections(ingredient);
            // add to inflection dictionary
            if (!inflection.Item1.NullOrEmpty() && !inflection.Item2.NullOrEmpty() && !inflection.Item3.NullOrEmpty() && !inflection.Item4.NullOrEmpty())
            {
                /*                Log.Message($"plur = {inflection.Item1}"); Log.Message($"coll = {inflection.Item2}"); Log.Message($"sing = {inflection.Item3}"); Log.Message($"adj = {inflection.Item4}");*/
                ingredientInflections.Add(ingredient, inflection);
            }
            else { Log.Error($"Failed to find an inflection for {ingredient.label}"); Log.Message($"plur = {inflection.Item1}"); Log.Message($"coll = {inflection.Item2}"); Log.Message($"sing = {inflection.Item3}"); Log.Message($"adj = {inflection.Item4}"); }
        }
    }

    static void Debug()
    {
/*        foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefs.ToList())
        {
            if (DefDatabase<ThingCategoryDef>.GetNamed("FT_Fire").ContainedInThisOrDescendant(thing))
            {
                {
                    Log.Message($">{thing.defName} is in categories:");
                    foreach (ThingCategoryDef category in thing.thingCategories)
                    {
                        Log.Message($"{category.defName}");
                    }
                }
            }
        }*/
        /*        foreach (ThingDef thingDef in DefDatabase<ThingDef>.AllDefs.ToList())
                {
                    if (thingDef.HasComp<CompFlavor>() && ThingCategoryDef.Named("FT_MealsFlavor").ContainedInThisOrDescendant(thingDef))
                    {
                        Log.Warning($">>>{thingDef.defName} has CompFlavor and is in FT_MealsFlavor");
                    }
                }*/

    }

    // for FT_Categories, inherit mod extension variables from parent where appropriate
    public static void InheritParentModExtensions()
    {
        foreach (ThingCategoryDef cat in flavorRoot.ThisAndChildCategoryDefs)
        {
            // inherit singularCollective field value from parent if it is null in child
            if (cat.GetModExtension<FlavorCategoryModExtension>().singularCollective == null)
            {
                cat.GetModExtension<FlavorCategoryModExtension>().singularCollective = cat.parent.GetModExtension<FlavorCategoryModExtension>().singularCollective;
            }
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
        List<RecipeDef> allRecipes = DefDatabase<RecipeDef>.AllDefs.ToList();

        List<ThingDef> flavorThingDefs = ThingCategoryDef.Named("Foods").DescendantThingDefs.ToList();  // all descendant ThingDefs in "Foods"
        flavorThingDefs.RemoveDuplicates();

        foreach (ThingDef ingredient in flavorThingDefs)
        {
            /*            tag = false;
                        if (ingredient.defName.ToLower().Contains("paste")) { tag = true; }*/
            List<string> splitNames = ExtractNames(ingredient);
            ThingCategoryDef newParent = NewParentRecursive(splitNames, ingredient, ingredient.thingCategories, flavorRoot);
            if (tag) { Log.Message($"!!! found new parent {newParent.defName}"); }
            if (newParent != null)
            {
                if (!ingredient.thingCategories.Contains(newParent)) { ingredient.thingCategories.Add(newParent); }
                if (!newParent.childThingDefs.Contains(ingredient)) { newParent.childThingDefs.Add(ingredient); }

                newParent = AddCompFlavor(ingredient, splitNames, newParent);  // check if the ThingDef fit into FT_MealsFlavor ThingCategory, and if so, add a CompFlavor to it
            }
        }

        // repeat for buildings, but use Buildings as the root and don't worry about adding CompFlavor
        List<ThingDef> cookingStationThingDefs = DefDatabase<ThingDef>.AllDefs.Where(b => b.category == ThingCategory.Building).ToList();  // all descendant ThingDefs with "Building" enum category (not ThingCategoryDef Buildings)
        cookingStationThingDefs.RemoveDuplicates();

        foreach (ThingDef building in cookingStationThingDefs)
        {
            List<string> splitNames = ExtractNames(building);
            ThingCategoryDef newParent = NewParentRecursive(splitNames, building, building.thingCategories, ThingCategoryDef.Named("FT_Buildings"));
            building.thingCategories ??= [];
            if (newParent != null)
            {
                if (!building.thingCategories.Contains(newParent)) { building.thingCategories.Add(newParent); }
                if (!newParent.childThingDefs.Contains(building)) { newParent.childThingDefs.Add(building); }
            }
        }

        // if it's in FT_FoodMeals and belongs in FT_MealsFlavor, give a CompFlavor
        //TODO: this picks up RC2_JoyDrinks atm
        static ThingCategoryDef AddCompFlavor(ThingDef ingredient, List<string> splitNames, ThingCategoryDef newParent)
        {
            if (ThingCategoryDef.Named("FT_FoodMeals").ThisAndChildCategoryDefs.Contains(newParent))
            {
                if (tag) { Log.Message($"Testing if {ingredient.defName} fits in MealsFlavor"); foreach (string name in splitNames) { Log.Message(name); } }
                ThingCategoryDef mealsFlavor = ThingCategoryDef.Named("FT_MealsFlavor");
                newParent = GetBestFlavorCategory(splitNames, ingredient, mealsFlavor.ThisAndChildCategoryDefs.ToList(), minimumAcceptedScore: 6);
                if (mealsFlavor.ThisAndChildCategoryDefs.Contains(newParent))
                {
                    if (tag) { Log.Message($"!!! adding to {newParent}"); }
                    if (!ingredient.thingCategories.Contains(newParent)) { ingredient.thingCategories.Add(newParent); }
                    if (!newParent.childThingDefs.Contains(ingredient)) { newParent.childThingDefs.Add(ingredient); }
                    CompProperties_Flavor compProperties = new();
                    ingredient.comps.Add(compProperties);  // postpend CompFlavor to meal
                }
            }
            return newParent;
        }
    }


    // get the # of descendant ThingDefs in each FT_ThingCategory; gives an idea of how specific the category is (for more precise meal renaming)
    private static void SetCategorySpecificities()
    {
        foreach (ThingCategoryDef flavorCategory in flavorRoot.ThisAndChildCategoryDefs)
        {
            List<ThingDef> descendants = flavorCategory.DescendantThingDefs.ToList();
            descendants.RemoveDuplicates();
            flavorCategory.GetModExtension<FlavorCategoryModExtension>().specificity += descendants.Count;
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

    // figure out what the best FT_Category is for a ThingDef or ThingCategoryDef; thing may already be in it; returns null if no new fitting FT_Category found
    public static ThingCategoryDef NewParentRecursive(List<string> splitNames, Def searchedDef, List<ThingCategoryDef> parentCategories, ThingCategoryDef topLevelCategory)
    {
        if (tag) { Log.Message("------------------------"); Log.Warning($"Finding NewParent for {searchedDef.defName}"); }
        ThingCategoryDef bestFlavorCategory = GetBestFlavorCategory(splitNames, searchedDef, topLevelCategory.ThisAndChildCategoryDefs.ToList());

        // if nothing matched, try using the ThingDefs parent categories to find a match
        if (bestFlavorCategory == null && !parentCategories.NullOrEmpty())  // don't do this if recursive flag is disabled
        {
            if (tag) { Log.Error($"No category found for {searchedDef.defName}, looking at its parent categories"); }
            foreach (ThingCategoryDef parentCategory in parentCategories)
            {
                if (tag) { Log.Message($"Found parent category {parentCategory.defName}"); }
                var ext = topLevelCategory.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories;
                if (parentCategory != null && !ext.NullOrEmpty() && ext[0].ThisAndChildCategoryDefs.Contains(parentCategory))  // if it's in the vanilla sister category of the highest FT_Category
                {
                    if (tag) { Log.Message("valid category, testing..."); }
                    List<string> splitNames2 = ExtractNames(parentCategory);
                    ThingCategoryDef bestFlavorCategory2 = NewParentRecursive(splitNames2, parentCategory, [parentCategory.parent], topLevelCategory);
                    if (bestFlavorCategory2 != null)
                    {
                        bestFlavorCategory = bestFlavorCategory2;
                        if (tag) { Log.Error($"Using parent categories, found new best category {bestFlavorCategory.defName}"); }
                        break;
                    }
                }
            }
        }

        // if you still didn't find a match, use flavorRoot
        bestFlavorCategory ??= topLevelCategory;
        return bestFlavorCategory;
    }

    // based on the defName and label, which FT_Category does this Def fit best into?
    private static ThingCategoryDef GetBestFlavorCategory(List<string> splitNames, Def searchedDef, List<ThingCategoryDef> categoriesToSearch, int minimumAcceptedScore = 1)
    {
        try
        {
            /*        tag = false;
                    if (searchedDef.defName.ToLower().Contains("fert")) { tag = true; }*/
            if (tag) { Log.Message($"Getting BestFlavorCategory for {searchedDef.defName}"); }
            ThingCategoryDef bestFlavorCategory = null;
            int categoryScore;
            int bestCategoryScore = 0;

            foreach (ThingCategoryDef flavorCategory in categoriesToSearch)
            {
                // if searchedDef is a category and is a sister category of flavorCategory, you're done
                if (flavorCategory.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories != null && flavorCategory.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories.Contains(searchedDef))
                {
                    bestFlavorCategory = flavorCategory;
                    break;
                }

                // get a score based on how well the flavorCategory keywords match the searchedDef's names
                categoryScore = 0;
                List<string> keywords = flavorCategory.GetModExtension<FlavorCategoryModExtension>().keywords;
                foreach (string keyword in keywords)
                {
                    categoryScore += ScoreKeyword(splitNames, keyword);
                }

                List<string> blacklist = flavorCategory.GetModExtension<FlavorCategoryModExtension>().blacklist;
                foreach (string black in blacklist)
                {
                    categoryScore -= ScoreKeyword(splitNames, black);
                }

                if (categoryScore >= minimumAcceptedScore && flavorCategory != null)
                {
                    if (tag) { Log.Message($"Found matching category {flavorCategory} with score of {categoryScore} and nest depth of {flavorCategory.treeNode.nestDepth}"); }
                    if (categoryScore > bestCategoryScore || (categoryScore == bestCategoryScore && flavorCategory.treeNode.nestDepth > bestFlavorCategory.treeNode.nestDepth))  // if you exceeded the best score so far, save the new score and category; ties are broken by nest depth
                    {
                        bestCategoryScore = categoryScore;
                        bestFlavorCategory = flavorCategory;
                        if (tag) { Log.Message($"Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.treeNode.nestDepth}"); }
                    }
                }

            }

            return bestFlavorCategory;
        }
        catch (Exception ex)
        {
            Log.Error($"error testing {searchedDef.defName}: {ex}");
            return null;
        }
    }

    // see how well the keyword fits into splitNames: element matches keyword exactly, element starts or ends with keyword, element contains keyword, keyword phrase is present in combined splitNames
    private static int ScoreKeyword(List<string> splitNames, string keyword)
    {
        int keywordScore = 0;
        int count = 0;

        foreach (string name in splitNames)
        {
            // exact: +3 to score each time the keyword matches an element exactly in splitNamesCopy (e.g. 1x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins])
            if (name == keyword) { keywordScore += 3; continue; }
            // start/end: +2 to score each time if the keyword matches the start or end of an element in splitNamesCopy (e.g. 2x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins]
            if (name.StartsWith(keyword) || name.EndsWith(keyword)) { keywordScore += 2; continue; }
            // contains: +1 to score each time if the keyword matches any part of an element in splitNamesCopy (e.g. 2x 'ump' in [pumpkin, orange, smoothie, sugar, pumpkins]
            if (name.Contains(keyword)) { keywordScore += 1; continue; }
        }

        // contains keyword phrase: +6 to score each time the keyword matches a substring of splitNamesCopy when they're all combined with spaces (e.g. 1x 'sugar pumpkin' in "pumpkin orange smoothie sugar pumpkins")
        // this effectively checks for multi-word keywords if nothing else matched
        if (keywordScore == 0)
        {
            string temp = string.Join(" ", splitNames);
            count = Regex.Matches(temp, keyword).Count;
            keywordScore += 6 * count;
        }

        return keywordScore;
    }

    // get singular and plural forms of each ingredient
    public static Tuple<string, string, string, string> GenerateIngredientInflections(ThingDef ingredient)
    {
        tag = false;
        if (ingredient.defName.ToLower().Contains("(")) { tag = true; }
        string plur;  // plural form // a dish made of CABBAGES that are diced and then stewed in a pot
        string coll;  // collective form, singular/plural ending depending in IRL ingredient size // stew with CABBAGE  // stew with PEAS
        string sing;  // singular form // a slice of BRUSSELS SPROUT
        string adj;  // adjectival form // PEANUT brittle

        GetInflections(ingredient, out plur, out coll, out sing, out adj);

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
            if (ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_Egg")))
            {
                plur = "eggs";
                coll = "egg";
                sing = "egg";
                adj = sing;
                return;
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
                string labelFirstDeletions = label;
                string defNameCompare = ingredient.defName;
                if (tag) { Log.Warning($">>>starting defName is {defNameCompare}"); }
                defNameCompare = Regex.Replace(defNameCompare, "([_])", " ");  // remove spacer chars

                // remove unnecessary bits
                List<string> delete = ["raw", "canned", "pickled", "dried", "dehydrated", "salted", "prepared", "trimmed", "meal", "leaf", "leaves", "stalks*", "meat", "seeds*"];  // unnecessary bits to delete // TODO: keep canned and pickled and such; problem is atm not deleting those causes "meat" to be deleted  //TODO: parentheses
                foreach (string del in delete)
                {
                    string temp = Regex.Replace(labelFirstDeletions, $"(?i)\\b{del}\\b", "");  // delete complete words that match those in "delete"
                    temp = temp.Trim();
                    if (Regex.IsMatch(temp, "[a-zA-Z]")) { labelFirstDeletions = temp; }  // accept deletion if letters remain
                    if (tag) { Log.Message($"deleted bit from label, is now {labelFirstDeletions}"); }
                    string temp2 = Regex.Replace(defNameCompare, $"(?i)\\b{del}\\b", "");  // delete complete words from defName that match those in "delete"
                    temp2 = temp2.Trim();
                    if (Regex.IsMatch(temp, "[a-zA-Z]")) { defNameCompare = temp2; }  // accept deletion from defName if letters remain
                    if (tag) { Log.Message($"deleted bit from defName, is now {defNameCompare}"); }
                }

                string labelCompare = Remove.RemoveDiacritics(labelFirstDeletions);
                labelCompare = labelCompare.ToLower();
                labelCompare = labelCompare.Trim();

                defNameCompare = Remove.RemoveDiacritics(defNameCompare);
                defNameCompare = Regex.Replace(defNameCompare, "(?<=[a-zA-Z])([A-Z][a-z]+)", " $1");  // split up name based on capitalized words
                defNameCompare = Regex.Replace(defNameCompare, "(?<=[a-z])([A-Z]+)", " $1");  // split up names based on unbroken all-caps sequences
                if (tag) { Log.Message($"split up defName, is now {defNameCompare}"); }
                defNameCompare = defNameCompare.ToLower();
                defNameCompare = defNameCompare.Trim();

                // formulate inflections by comparing label and defName; if you're unable to, use the label

                string root = GetLongestCommonSubstring(defNameCompare, labelCompare);  // VCE_RawPumpkin + mammoth gold pumpkins => pumpkin
                if (tag) { Log.Warning($"Longest common substring was {root}"); }

                //try to get plural form by comparing defName and label  // you can't just rely on checking -s endings b/c meat will never end in -s  // you can't just rely on label b/c it might have unnecessary words (e.g. "mammoth gold" pumpkins)
                if (root != null && Regex.IsMatch(labelCompare, $"\\b{root}"))  // make sure root starts at the start of a word
                {

                    // extend the overlap to the end of a word in label // mammoth gold pumpkins + pumpkin => pumpkins
                    Match match = Regex.Match(labelCompare, "(?i)" + root + "[^ ]*");
                    plur = match.Value;
                    if (tag) { Log.Message($"plural matched = {plur}"); }
                    int head = labelCompare.IndexOf(root);
                    plur = labelFirstDeletions.Substring(head, plur.Length);  // get diacritics and capitalization back

                    if (tag) { Log.Message($"plural final = {plur}"); }

                    // if the 2 forms differ by more than 2 letters, discard them and use ingredient.label
                    if (root.Length == 0 || root.Length < plur.Length - 2)
                    {
                        if (tag) { Log.Message($"root was {root}"); }
                        plur = ingredient.label;
                        if (tag) { Log.Message($"plural fallback = {plur}"); }
                    }


                }
                else
                {
                    plur = ingredient.label;
                    if (tag) { Log.Message($"plural fallback2 = {plur}"); }
                }

                // try to get singular form from plural form
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
                bool? singularCollective = parentCategory.GetModExtension<FlavorCategoryModExtension>().singularCollective;
                coll = singularCollective == true ? sing : plur;
                if (tag) { Log.Message($"coll = {coll}"); }

                // try to get adjectival form (based on singular)
                adj = sing;
                return;
            }
        }

        static string GetLongestCommonSubstring(string string1, string string2)
        {
            try
            {
                {
                    if (!string1.NullOrEmpty() && !string2.NullOrEmpty())
                    {
                        // find the overlap
                        string root = LongestCommonSubstring(string2, string1);
                        root.Trim();
                        /*                        Log.Message($"Longest common substring was {root}");*/
                        return root;
                    }
                    return null;
                }

            }

            catch (Exception ex)
            {
                Log.Error($"Error finding inflections of ${string2}: {ex}");
                return null;
            }

            // find common substring, comparing label to defName
            // returned substring ignores uppercase when comparing
            static string LongestCommonSubstring(string string1, string string2)
            {
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

                return string1.Substring(row - a[row, col], a[row, col]);
            }
        }
    }

    // look in the FlavorDefs for any that have a category that isn't defined in FlavorText; make it into a new category and assign it keywords and such
    /*        static void GenerateAdHocCategories()
            {
                foreach (FlavorDef flavorDef in FlavorDefs)
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


