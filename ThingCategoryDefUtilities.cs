﻿using System;
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

//TODO: allow for adding multiple FT_Categories at a time?
//TODO: VCE_canned fruit is in FT_Foods
//TODO: VCE_canned stuff doesn't seem to carry its ingredient over (might need to copy it manually)
//TODO: VCE_canned eggs are in Foods
//TODO: VGE watermelon is in candy
//RELEASE: Flavor Text is still showing up in bills

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

    public static Dictionary<ThingDef, Tuple<string, string, string>> ingredientInflections = [];

    static ThingCategoryDefUtilities()
    {
        Log.Warning("FlavorTextUtilities static constructor");

        CompileCategories();  // get FlavorText-related ThingCategoryDefs
        GetFlavorCategoryChildren();  // assign all relevant ThingsDefs to a FlavorText ThingCategoryDef
        DefDatabase<ThingCategoryDef>.ResolveAllReferences();
        Debug();
        GenerateIngredientInflections();
        FlavorDef.SetSpecificities();  // get specificity for each FlavorDef; can't do this until now, needs previous 2 methods and a built DefDatabase
    }

    static void Debug()
    {
        /*            foreach (ThingDef thing in DefDatabase<ThingDef>.AllDefs.ToList())
                    {
                        if (ThingCategoryDef.Named("FT_Foods").ContainedInThisOrDescendant(thing))
                        {
                            if (thing.defName.ToLower().Contains("meat")) { continue; }
                            *//*                    Log.Warning($">{thing.defName} is in categories:");
                                                foreach (ThingCategoryDef category in thing.thingCategories)
                                                {
                                                    Log.Message($"{category.defName}");
                                                }*//*
                        }
                    }*/
/*        List<ThingDef> allingredients = flavorRoot.DescendantThingDefs.ToList();
        foreach (ThingDef ingredient in allingredients)
        {
            Log.Message($"Found {ingredient.defName}");
        }*/

    }

    // get singular and plural forms of each ingredient
    static void GenerateIngredientInflections()
    {
        List<string> delete = ["raw", "canned", "pickled", "preserved", "dried", "dehyrdated", "fruit", "meal", @"(" + "[^()]*" + @")", "meat"];  // unnecessary bits to delete; regex entry is for "(things in parentheses)"
        List<ThingDef> allingredients = flavorRoot.DescendantThingDefs.ToList();
        foreach (ThingDef ingredient in allingredients)
        {
            string str = ingredient.label;
            // remove unnecessary bits
            foreach (string del in delete)
            {
                string temp = Regex.Replace(str, $"(?i)(\\b{del})|({del}\\b)", "");
                if (!temp.NullOrEmpty()) { str = temp; }
            }

            string adj;
            string sing;
            string plur;
            // get label variations
            if (ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_MeatRaw")))
            {
                switch (ingredient.defName)
                {
                    case "Meat_Twisted":
                        {
                            plur = "twisted flesh";
                            sing = plur;
                            adj = "twisted";
                            break;
                        }
                    case "Meat_Human":
                        {
                            plur = "long pork";
                            sing = plur;
                            adj = "cannibal";
                            break;
                        }
                    case "Meat_Megaspider":
                        {
                            plur = "bug guts";
                            sing = "bug";
                            adj = "bug";
                            break;
                        }
                    default:
                        {
                            plur = str;
                            sing = plur;
                            adj = sing;
                            break;
                        }
                }
            }
            else if (ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_Egg")))
            {
                plur = "eggs";
                sing = "egg";
                adj = "egg";
            }
            else if (ingredient.defName == "RawFungus")
            {
                plur = "mushrooms";
                sing = "mushroom";
                adj = "mushroom";
            }
            else
            {
                plur = str;
                sing = plur;
                sing = Regex.Replace(sing, "ies\\b", "y");
                sing = Regex.Replace(sing, "[aeiou]s\\b", "");
                sing = Regex.Replace(sing, "s\\b", "");
                adj = sing;
            }

            if (!ingredient.IsWithinCategory(ThingCategoryDef.Named("FT_MeatRaw"))) { Log.Message($"Found {plur}, {sing}, and {adj}"); }
            // add to inflection dictionary
            if (!plur.NullOrEmpty() && !sing.NullOrEmpty() && !adj.NullOrEmpty())
            {
                ingredientInflections.Add(ingredient, Tuple.Create(plur, sing, adj));
            }
            else { Log.Error($"Failed to find an inflection for {ingredient.label}"); Log.Message($"plur = {plur}"); Log.Message($"sing = {sing}"); Log.Message($"adj = {adj}"); }
            }

        /*
        List<ThingDef> allPlants = DefDatabase<ThingDef>.AllDefsListForReading;
        foreach (ThingDef plant in allPlants)
        {
            try
            {
                // compare labels of the plant and its product
                if (plant.plant != null && plant.plant.harvestedThingDef != null && flavorRoot.DescendantThingDefs.Contains(plant.plant.harvestedThingDef))
                {
                    string produce = plant.plant.harvestedThingDef.label;  // product will usually be plural
                    Log.Message($"produce name was {produce}");
                    string grower = plant.label;  // plant will usually be singular
                    Log.Message($"grower name was {grower}");

                    if (!produce.NullOrEmpty() && !grower.NullOrEmpty())
                    {

                        // find the overlap
                        string root = FindLongestCommonSubstring(produce, grower);
                        Log.Message($"Longest common substring was {root}");
                        if (!root.NullOrEmpty())
                        {
                            // find a word that contains the overlap but take the whole word
                            Match match = Regex.Match(grower, root + "[^ ]*");
                            Log.Message($"root was {root}");
                            string singular = root;
                            Log.Message($"singular was {singular}");
                            match = Regex.Match(produce, root + "[^ ]*");
                            string plural = match.Value;
                            Log.Message($"plural was {plural}");

                            Tuple<string, string> entry = null;
                            if (singular != null && plural != null) { entry = Tuple.Create(singular, plural); }
                            if (entry != null && !ingredientInflections.ContainsKey(plant.plant.harvestedThingDef)) { ingredientInflections.Add(plant.plant.harvestedThingDef, entry); }
                        }
                    }
                }

            }

            catch (Exception ex)
            {
                Log.Error($"Error finding inflections of ${plant.label} produce: {ex}");
            }
        }

        static string FindLongestCommonSubstring(string s1, string s2)
        {
            int[,] a = new int[s1.Length + 1, s2.Length + 1];
            int row = 0;    // s1 index
            int col = 0;    // s2 index

            for (var i = 0; i < s1.Length; i++)
                for (var j = 0; j < s2.Length; j++)
                    if (s1[i] == s2[j])
                    {
                        int len = a[i + 1, j + 1] = a[i, j] + 1;
                        if (len > a[row, col])
                        {
                            row = i + 1;
                            col = j + 1;
                        }
                    }

            return s1.Substring(row - a[row, col], a[row, col]);
        }*/

    }

    // get all FlavorText ThingCategoryDefs (they start with FT_)
    public static void CompileCategories()
    {
        Log.Warning("Compiling flavor categories");
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


        // look through all ThingDefs in flavorRoot and shift them to new FT_ThingCategoryDefs if they fit better there
        static void ReorderChildren()
        {
            Log.Warning("starting unlisted ThingDefs");
            List<RecipeDef> allRecipes = DefDatabase<RecipeDef>.AllDefs.ToList();

            List<ThingDef> foodThingDefs = ThingCategoryDef.Named("Foods").DescendantThingDefs.ToList();  // all descendant ThingDefs in "Foods"
            foodThingDefs.RemoveDuplicates();

            foreach (ThingDef ingredient in foodThingDefs)
            {
                tag = false;
                /*if (ingredient.defName.ToLower().Contains("jeru")) { tag = true; }*/

                HashSet<string> splitNames = ExtractNames(ingredient);
                ThingCategoryDef newParent = NewParent(splitNames, ingredient, ingredient.thingCategories);
                if (tag) { Log.Warning($"!!! found new parent {newParent.defName}"); }
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
        /*        Log.Warning("------------------------");
                { Log.Warning($"Finding NewParent for {searchedDef.defName}"); }*/
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
                if (tag) { Log.Warning($"Found parent category {parentCategory.defName}"); }
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


