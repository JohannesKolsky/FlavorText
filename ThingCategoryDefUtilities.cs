using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

// Verse.ThingCategoryNodeDatabase.FinilizeInit() is what adds core stuff to ThingCategoryDef.childCategories

//--TODO: poultry eggs show up in FT_Poultry
//--TODO: FT_Poultry and FT_Sheep aren't getting their relevant thingdefs
//--TODO: some drugs are being tested for some reason
//--TODO: VCE_Condiments are going all over
//--TODO: "FoodRaw" should be default "this is an ingredient that adds calories"; "Foods" covers condiments and such xxx this would require another level, which isn't worth it
//--TODO: VCE_bakes aren't finding the flavorDef despite matching ones found, even if it's just flour
//--TODO: fertilized eggs are ending up in FT_EggUnfertilized (cause "egg" keyword?)
//--TODO: VCE_canned meat isn't finding any flavor defs

//RELEASE: Flavor Text is still showing up in bills

//TODO: allow for adding multiple FT_Categories at a time?
//TODO: VCE_canned fruit is in FT_Foods
//TODO: VCE_canned eggs are in Foods
//TODO: VGE watermelon is in candy
//TODO: VGP defNames are sing: bean, lentil, beet
//TODO: RC2 Chili peppers are in FT_Foods
//TODO: condiments shouldn't be a full ingredient (FT_Foods -> FT_FoodRaw)
//TODO: get FT to put VCE_canned into meats/vegetables/fruits/etc
//TODO: if an ingredient has subingredients, use the subingredients instead of the main ingredient (VCE canned stuff, GAB pickled stuff, meals, etc)

namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for FlavorText-related ThingCategoryDefs
/// </summary>

[StaticConstructorOnStartup]
public static class ThingCategoryDefUtilities
{
    public static List<ThingCategoryDef> flavorCategories = [];  // list of all FlavorText related ThingCategoryDefs, aka FT_Categories

    public static ThingCategoryDef flavorRoot = ThingCategoryDef.Named("FT_Foods"); // topmost category used in FlavorText calculations; several unused FT_Categories on top, then Root

    public static bool tag;  // DEBUG

    public static Dictionary<ThingDef, Tuple<string, string, string, string>> ingredientInflections = [];

    static ThingCategoryDefUtilities()
    {
        Log.Message("FlavorTextUtilities static constructor");

        CompileCategories();  // get FlavorText-related ThingCategoryDefs
        GetFlavorCategoryChildren();  // assign all relevant ThingsDefs to a FlavorText ThingCategoryDef
        DefDatabase<ThingCategoryDef>.ResolveAllReferences();
        Debug();
        List<ThingDef> allingredients = flavorRoot.DescendantThingDefs.ToList();
        foreach (ThingDef ingredient in allingredients)
        {
            Tuple<string, string, string, string> inflection = GenerateIngredientInflections(ingredient);
            /*            if (!ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_MeatRaw"))) { Log.Message($"Found {plur}, {coll}, {sing}, and {adj}"); }*/
            // add to inflection dictionary
            if (!inflection.Item1.NullOrEmpty() && !inflection.Item2.NullOrEmpty() && !inflection.Item3.NullOrEmpty() && !inflection.Item4.NullOrEmpty())
            {
                Log.Message($"plur = {inflection.Item1}"); Log.Message($"coll = {inflection.Item2}"); Log.Message($"sing = {inflection.Item3}"); Log.Message($"adj = {inflection.Item4}");
                ingredientInflections.Add(ingredient, inflection);
            }
            else { Log.Error($"Failed to find an inflection for {ingredient.label}"); Log.Message($"plur = {inflection.Item1}"); Log.Message($"coll = {inflection.Item2}"); Log.Message($"sing = {inflection.Item3}"); Log.Message($"adj = {inflection.Item4}"); }
        }

        FlavorDef.SetSpecificities();  // get specificity for each FlavorDef; can't do this until now, needs previous 2 methods and a built DefDatabase
    }

    static void Debug()
    {
        /*   foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefs.ToList())
           {
               if (ThingCategoryDef.Named("FT_Foods").ContainedInThisOrDescendant(thing))
               {
                   if (thing.defName.ToLower().Contains("meat")) { continue; }
                   Log.Message($">{thing.defName} is in categories:");
                   foreach (ThingCategoryDef category in thing.thingCategories)
                   {
                       Log.Message($"{category.defName}");
                   }
               }
           }*/
        /*        List<ThingDef> allingredients = flavorRoot.DescendantThingDefs.ToList();
                foreach (ThingDef ingredient in allingredients)
                {
                    Log.Message($"Found {ingredient.defName}");
                }*/

    }

    // get singular and plural forms of each ingredient
    public static Tuple<string, string, string, string> GenerateIngredientInflections(ThingDef ingredient)
    {
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
                            sing = plur;
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
                coll = plur;
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

            // if ingredient isn't a special def from above, attempt to generate accurate inflections
            else
            {
                // raw boomaløpe prepared thighs meat
                // Meat_Raw_Boomalope_Thigh

                // boomaløpe thighs
                // boomalope thigh

                // boomalope thighs
                // boomalope thigh

                // boomalope thigh

                // boomalope thighs
                // boomalope thigh

                // boomaløpe thighs
                // boomaløpe thigh

                string label = ingredient.label;
                string labelCompareDiacritics = label;
                // remove unnecessary bits
                List<string> delete = ["raw", "canned", "preserved", "dried", "dehydrated", "prepared", "fruit", "meal", "leaf", "leaves", "stalks*", @"(" + "[^()]*" + @")", "meat"];  // unnecessary bits to delete; regex entry is for "(things in parentheses)"
                foreach (string del in delete)
                {
                    string temp = Regex.Replace(labelCompareDiacritics, $"(?i)(\\b{del}\\b)", "");  // delete complete words that match those in "delete"
                    if (Regex.IsMatch(temp, "[a-zA-Z]")) { labelCompareDiacritics = temp; }  // accept deletion if letters remain
                }

                string labelCompare = Remove.RemoveDiacritics(labelCompareDiacritics);
                labelCompare = labelCompare.Trim();

                string defNameCompare = ingredient.defName;
                defNameCompare = Regex.Replace(defNameCompare, "([_])", " ");  // remove spacer chars
                defNameCompare = Remove.RemoveDiacritics(defNameCompare);
                defNameCompare = Regex.Replace(defNameCompare, "([^ ]*[A-Z][a-z]*)", " $1");  // split up name based on capitalization
                defNameCompare = defNameCompare.ToLower();
                defNameCompare = defNameCompare.Trim();

                string root = GetLongestCommonSubstring(defNameCompare, labelCompare);  // VCE_RawPumpkin + mammoth gold pumpkins => pumpkin
                if (root != null && Regex.IsMatch(labelCompare, $"\\b{root}"))  // make sure root starts at the start of a word
                {

                    // extend the overlap to the end of a word in label // mammoth gold pumpkins + pumpkin => pumpkins
                    Match match = Regex.Match(labelCompare, "(?i)" + root + "[^ ]*");
                    plur = match.Value;
                    int head = labelCompare.IndexOf(root);
                    plur = labelCompareDiacritics.Substring(head, plur.Length);  // get diacritics back

                    // use the defName to get a collective label, since it will usually have the appropriate form (e.g. RawCabbage => collective "cabbage", while RawPeas => collective "peas")
                    // extend the overlap to the end of a word in defName // VCE Raw Pumpkin + pumpkin => pumpkin
                    Match match2 = Regex.Match(defNameCompare, "(?i)" + root + "[^ ]*");
                    coll = match2.Value;
                    int head2 = defNameCompare.IndexOf(root);
                    coll = defNameCompare.Substring(head2, coll.Length);  // get diacritics back

                    // if the 2 forms don't have enough in common, discard them and use ingredient.label
                    string root2 = GetLongestCommonSubstring(plur, coll);
                    if (root2.Length == 0 || root2.Length < plur.Length - 2 || root2.Length < coll.Length - 2)
                    {
                        plur = ingredient.label;
                        coll = plur;
                    }
                }
                else
                {
                    plur = ingredient.label;
                    coll = plur;
                }
                Log.Message($"plural was {plur}");

                Log.Message($"collective was {coll}");

                // try to get singular
                sing = plur;
                sing = Regex.Replace(sing, "ies$", "y");
                sing = Regex.Replace(sing, "sses$", "ss");
                sing = Regex.Replace(sing, "s$", "");

                // try to get adjectival
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
                        Log.Message($"Longest common substring was {root}");
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
                        if (Char.ToLower(string1[i]) == Char.ToLower(string2[j]))
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

    // get all FlavorText ThingCategoryDefs (they start with FT_)
    public static void CompileCategories()
    {
        Log.Message("Compiling flavor categories");
        List<ThingCategoryDef> allThingCategoryDefs = (List<ThingCategoryDef>)DefDatabase<ThingCategoryDef>.AllDefs;
        for (int i = 0; i < allThingCategoryDefs.Count; i++)
        {
            if (allThingCategoryDefs[i].defName.StartsWith("FT_"))
            {
                /*                Log.Message($"Found FT_Category called {allThingCategoryDefs[i].defName}"); */
                flavorCategories.Add(allThingCategoryDefs[i]);
            }
        }
    }

    // assign all ThingDefs and ThingCategoryDefs in "Foods" to the best FT_ThingCategory
    public static void GetFlavorCategoryChildren()
    {
        /*        CopyFromSisterCategories();*/
        ReorderChildren();

        /*        // each vanilla FT_Category has a corresponding vanilla Category; add the FT_Category as a parent category to all ThingDefs directly hosted in the vanilla Category
                // this gives a base to start from, ensuring at a minimum a lot of ThingDefs start in the FT_Category that corresponds to their vanilla Category
                static void CopyFromSisterCategories()  // TODO: recursive to make this dig out all ThingDefs
                {
                    foreach (ThingCategoryDef flavorCategory in flavorCategories)
                    {
                        List<ThingCategoryDef> flavorSisterCategories = flavorCategory.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories;
                        foreach (ThingCategoryDef flavorSisterCategory in flavorSisterCategories)
                        {
                            // copy child thingdefs
                            foreach (ThingDef childThingDef in flavorSisterCategory.childThingDefs)
                            {
                                if (!childThingDef.thingCategories.Contains(flavorCategory))
                                {
                                    childThingDef.thingCategories.Add(flavorCategory);
                                }
                            }
                        }
                    }
                }*/


        // look through all ThingDefs in flavorRoot and add them to FT_ThingCategoryDefs if they fit there
        static void ReorderChildren()
        {
            Log.Message("starting unlisted ThingDefs");
            List<RecipeDef> allRecipes = DefDatabase<RecipeDef>.AllDefs.ToList();

            List<ThingDef> foodThingDefs = ThingCategoryDef.Named("Foods").DescendantThingDefs.ToList();  // all descendant ThingDefs in "Foods"
            foodThingDefs.RemoveDuplicates();

            foreach (ThingDef ingredient in foodThingDefs)
            {
                tag = false;
                /*if (ingredient.defName.ToLower().Contains("jeru")) { tag = true; }*/

                HashSet<string> splitNames = ExtractNames(ingredient);
                ThingCategoryDef newParent = NewParent(splitNames, ingredient, ingredient.thingCategories);
                if (tag) { Log.Message($"!!! found new parent {newParent.defName}"); }
                if (newParent != null)
                {
                    if (!ingredient.thingCategories.Contains(newParent)) { ingredient.thingCategories.Add(newParent); }
                    if (!newParent.childThingDefs.Contains(ingredient)) { newParent.childThingDefs.Add(ingredient); }
                }
            }
        }
    }

    // split up the defName and label into single words and compile them; these will be searched to assign it a category
    private static HashSet<string> ExtractNames(Def def)
    {
        HashSet<string> splitNames = [];
        // try to find word boundaries in the defName and label and split it into those words
        string defNames = Regex.Replace(def.defName, "([_])|([-])", " ");
        defNames = Regex.Replace(defNames, "([A-Z][a-z]*)", " $1");
        defNames = defNames.ToLower();
        string[] splitDefNames = defNames.Split(' ');
        foreach (string defName in splitDefNames) { splitNames.Add(defName); }

        string labels = Regex.Replace(def.label, "([-])", " ");
        string[] splitLabels = labels.Split(' ');
        foreach (string label in splitLabels) { splitNames.Add(label); }

        return splitNames;
    }

    // figure out what the best FT_Category is; ingredient may already be in it; returns null if no new fitting FT_Category found
    private static ThingCategoryDef NewParent(HashSet<string> splitNames, Def searchedDef, List<ThingCategoryDef> parentCategories)
    {
        /*        Log.Message("------------------------");
                { Log.Message($"Finding NewParent for {searchedDef.defName}"); }*/
        ThingCategoryDef bestFlavorCategory = null;
        int categoryScore;
        int bestCategoryScore = 0;

        foreach (ThingCategoryDef flavorCategory in flavorCategories)
        {
            // if searchedDef is a category and is a sister category of flavorCategory, you're done
            if (flavorCategory.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories != null && flavorCategory.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories.Contains(searchedDef))
            {
                bestFlavorCategory = flavorCategory;
                break;
            }

            // get a score based on how well the flavorCategory keywords match the searchedDef's names
            else if (flavorRoot.ThisAndChildCategoryDefs.Contains(flavorCategory))  // only look at FT_ThingCategoryDefs that are children of the flavorRoot
            {
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

                if (categoryScore > 0 && flavorCategory != null)
                {
                    if (tag) { Log.Message($"Found matching category {flavorCategory} with score of {categoryScore}"); }
                    if (categoryScore > bestCategoryScore || (categoryScore == bestCategoryScore && flavorCategory.treeNode.nestDepth > bestFlavorCategory.treeNode.nestDepth))  // if you exceeded the best score so far, save the new score and category; ties are broken by nest depth
                    {
                        bestCategoryScore = categoryScore;
                        bestFlavorCategory = flavorCategory;
                        if (tag) { Log.Message($"Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} and nest depth of {bestFlavorCategory.treeNode.nestDepth}"); }
                    }
                }
            }
        }

        // if nothing matched, try using the ThingDefs parent categories to find a match
        if (bestFlavorCategory == null)
        {
            if (tag) { Log.Error($"No category found for {searchedDef.defName}, looking at its parent categories"); }
            foreach (ThingCategoryDef parentCategory in parentCategories)
            {
                if (tag) { Log.Message($"Found parent category {parentCategory.defName}"); }
                if (flavorRoot.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories[0].ThisAndChildCategoryDefs.Contains(parentCategory))  // if it's in the vanilla sister category of the highest FT_Category
                {
                    if (tag) { Log.Message("valid category, testing..."); }
                    HashSet<string> splitNames2 = ExtractNames(parentCategory);
                    ThingCategoryDef bestFlavorCategory2 = NewParent(splitNames2, parentCategory, [parentCategory.parent]);
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
        if (bestFlavorCategory == null)
        {
            bestFlavorCategory = flavorRoot;
            { Log.Error($"No suitable FT_ThingCategoryDef found for {searchedDef.defName}: placing it in {flavorRoot.defName}."); }
        }
        return bestFlavorCategory;

        static int ScoreKeyword(HashSet<string> splitNames, string keyword)
        {
            int keywordScore = 0;
            if (splitNames.Contains(keyword))  // +3 to score if the keyword matches an element exactly in splitNames (e.g. 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins])
            {
                keywordScore += 3;
            }
            foreach (string subName in splitNames)  // +2 to score each time the keyword matches the start or end of an element in splitNames (e.g. 2x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins]
            {
                if (subName.StartsWith(keyword) || subName.EndsWith(keyword))
                {
                    keywordScore += 2;
                }
            }
            if (keywordScore == 0)  // if the keyword hasn't matched anything yet (effectively, if it contains a space or will never match anything), +6 to score each time the keyword matches a substring of splitNames when they're all combined (e.g. 'sugar pumpkin' in "pumpkin orange smoothie sugar pumpkins")
            {
                string temp = string.Join(" ", splitNames);
                int count = Regex.Matches(temp, keyword).Count;
                keywordScore += 6 * count;
            }

            return keywordScore;
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


