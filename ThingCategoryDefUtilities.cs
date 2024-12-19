using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

// Verse.ThingCategoryNodeDatabase.FinilizeInit() is what adds core stuff to ThingCategoryDef.childCategories

//--TODO: poultry eggs show up in FT_Poultry


//TODO: FT_Poultry and FT_Sheep aren't getting their relevant thingdefs


namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for FlavorText-related ThingCategoryDefs
/// </summary>

[StaticConstructorOnStartup]
public static class ThingCategoryDefUtilities
{
    public static List<ThingCategoryDef> flavorThingCategoryDefs = [];  // list of all FlavorText related ThingCategoryDefs


    static ThingCategoryDefUtilities()
    {
        Log.Warning("FlavorTextUtilities static constructor");

        CompileCategories();  // get FlavorText-related ThingCategoryDefs
        GetFlavorCategoryChildren();  // assign all relevant ThingsDefs and ThingCategoryDefs to a FlavorText ThingCategoryDef
        FlavorDef.SetSpecificities();  // get specificity for each FlavorDef; can't do this until now, needs previous 2 methods and a built DefDatabase
        foreach (ThingCategoryDef category in DefDatabase<ThingCategoryDef>.AllDefs.ToList())
        {
            if (category.defName.StartsWith("FT_Meat") && !category.defName.StartsWith("FT_Root"))
            {
                Log.Warning($"utilities found {category.defName} with {category.DescendantThingDefs.Count()} descendant ThingDefs");
                foreach (ThingCategoryDef childCategory in category.childCategories)
                {
                    Log.Message(childCategory.defName);
                }
                Log.Message("------------");
                foreach (ThingDef childThingDef in category.childThingDefs)
                {
                    Log.Message(childThingDef.defName);
                }
                /*Log.Message("<><><><><><><><><><><><>");
                foreach (ThingDef descendant in category.DescendantThingDefs) { Log.Message(descendant.defName); }*/
            }
        }
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
                flavorThingCategoryDefs.Add(allThingCategoryDefs[i]);
            }
        }
    }

    // assign all ThingDefs and ThingCategoryDefs in "Foods" to the best FT_ThingCategory
    public static void GetFlavorCategoryChildren()  // TODO: duplicate entries showing up in MeatRaw; caused by overlapping child categories
    {
        CopyFromSisterCategories();
        ReorderChildren();

        // copy over vanilla child ThingDefs and ThingCategoryDefs from listed equivalent vanilla ThingCategoryDefs to corresponding fields of the FT_ThingCategoryDef
        static void CopyFromSisterCategories()
        {
            foreach (ThingCategoryDef thingCategoryDef in flavorThingCategoryDefs)
            {
                List<ThingCategoryDef> flavorSisterCategories = thingCategoryDef.GetModExtension<FlavorCategoryModExtension>().flavorSisterCategories;
                foreach (ThingCategoryDef flavorSisterCategory in flavorSisterCategories)
                {
                    // copy child categories
                    foreach (ThingCategoryDef childCategory in flavorSisterCategory.childCategories)
                    {
                        if (!thingCategoryDef.childCategories.Contains(childCategory))
                        {
                            thingCategoryDef.childCategories.Add(childCategory);
                        }
                    }

                    // copy child thingdefs
                    foreach (ThingDef childThingDef in flavorSisterCategory.childThingDefs)
                    {
                        if (!thingCategoryDef.childThingDefs.Contains(childThingDef))
                        {
                            thingCategoryDef.childThingDefs.Add(childThingDef);
                        }
                    }
                }
            }
        }


        // look through all ThingDefs and all FT_ThingCategoryDefs and shift them to new FT_ThingCategoryDefs if they fit better there
        static void ReorderChildren()
        {
            Log.Warning("starting unlisted ThingDefs");
            List<ThingDef> thingDefsFoods = ThingCategoryDef.Named("Foods").DescendantThingDefs.ToList();  // all ThingDefs in "Foods"
            thingDefsFoods.RemoveDuplicates();
            foreach (ThingDef ingredient in thingDefsFoods)
            {
                HashSet<string> splitNames = [];
                // try to find word boundaries in the defName and label and split it into those words
                string defNames = Regex.Replace(ingredient.defName, "([_])|([-])", " ");
                defNames = Regex.Replace(defNames, "([A-Z][a-z]*)", " $1");
                defNames = defNames.ToLower();
                string[] splitDefNames = defNames.Split(' ');
                foreach (string defName in splitDefNames) { splitNames.Add(defName); }

                string labels = Regex.Replace(ingredient.label, "([-])", " ");
                string[] splitLabels = labels.Split(' ');
                foreach (string label in splitLabels) { splitNames.Add(label); }

                PlaceIntoCategories(ingredient, splitNames);

            }

            Log.Warning("starting unlisted ThingCategoryDefs");
            List<ThingCategoryDef> categoriesFoods = ThingCategoryDef.Named("Foods").ThisAndChildCategoryDefs.ToList();  // all ThingCategoryDefs in "Foods"
            categoriesFoods.RemoveDuplicates();

            foreach (ThingCategoryDef flavorCategory in categoriesFoods)
            {
                HashSet<string> splitNames = [];
                // try to find word boundaries in the defName and label and split it into those words
                string defNames = Regex.Replace(flavorCategory.defName, "([_])|([-])", " ");
                defNames = Regex.Replace(defNames, "([A-Z][a-z]*)", " $1");
                defNames = defNames.ToLower();
                string[] splitDefNames = defNames.Split(' ');
                foreach (string defName in splitDefNames) { splitNames.Add(defName); }

                string labels = Regex.Replace(flavorCategory.label, "([-])", " ");
                string[] splitLabels = labels.Split(' ');
                foreach (string label in splitLabels) { splitNames.Add(label); }

                PlaceIntoCategories(flavorCategory, splitNames);

            }
        }
    }


    // place the Def into the best-fitting FT_ThingCategoryDef
    static void PlaceIntoCategories(ThingDef ingredient, HashSet<string> splitNames)
    {
        /*Log.Warning("BestCategory is examining ingredient " + ingredient.defName);*/
        List<ThingCategoryDef> oldFlavorParentCategories = [];
        foreach (ThingCategoryDef parentCategory in ingredient.thingCategories)
        {
            if (parentCategory.defName.StartsWith("FT_"))
            {
                oldFlavorParentCategories.Add(parentCategory);
            }
        }

        List<ThingCategoryDef> newFlavorParentCategories = NewParents(splitNames, ingredient, oldFlavorParentCategories);  // new parent flavor categories

        // move ingredients from old to new FlavorText parent categories, if the category changed
        ingredient.thingCategories = ingredient.thingCategories.Except(oldFlavorParentCategories).ToList();
        ingredient.thingCategories.AddRange(newFlavorParentCategories);
        foreach (ThingCategoryDef newFlavorCategory in newFlavorParentCategories)
        {
            if (!newFlavorCategory.childThingDefs.Contains(ingredient))
            {
                newFlavorCategory.childThingDefs.Add(ingredient);
            }
        }
    }

    // overload for ThingCategoryDef
    static void PlaceIntoCategories(ThingCategoryDef category, HashSet<string> splitNames)
    {
        List<ThingCategoryDef> oldFlavorParentCategories = category.parent != null ? [category.parent] : [];
        ThingCategoryDef newFlavorParentCategory = NewParents(splitNames, category, oldFlavorParentCategories)[0];  // change flavor parent categories; can only ever return 1 parent so this is easier than with the ThingDefs

        category.parent = newFlavorParentCategory;

        category.parent.childCategories.Remove(category);
        if (!newFlavorParentCategory.childCategories.Contains(category))
        {
            newFlavorParentCategory.childCategories.Add(category);
        }
    }

    // figure out what the new parents should be; may be the same as the old parents
    private static List<ThingCategoryDef> NewParents(HashSet<string> splitNames, Def searchedDef, List<ThingCategoryDef> parentFlavorCategories)
    {
        bool tag = false;
        if (searchedDef.defName.Contains("Meal")) { tag = true; }
        if (tag) { Log.Warning($"Finding NewParents using {searchedDef.defName} with {parentFlavorCategories.Count} parent flavor categories"); }
        ThingCategoryDef bestFlavorCategory = null;
        int categoryScore;
        int bestCategoryScore = 0;

        for (int i = 0; i == 0 || i < parentFlavorCategories.Count; i++)  // do at least once, up to # parent categories
        {
            if (!parentFlavorCategories.NullOrEmpty()) { bestFlavorCategory = parentFlavorCategories[i]; }
            foreach (ThingCategoryDef flavorCategory in flavorThingCategoryDefs)
            {
                if (parentFlavorCategories.NullOrEmpty() || parentFlavorCategories[i].ThisAndChildCategoryDefs.Contains(flavorCategory))  // only look at FT_ThingCategoryDefs that are children of the current parent; if no parent go ahead anyways
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
                            /*                            if (tag) { Log.Message($"Best new category is {bestFlavorCategory.defName} with score of {bestCategoryScore} with nest depth of {bestFlavorCategory.treeNode.nestDepth}"); }*/
                        }
                    }
                }

            }
            // if nothing matched and the Def didn't have a parent category, select the Foods category
            if (bestCategoryScore == 0)
            {
                if (parentFlavorCategories.NullOrEmpty())
                {
                    Log.Warning($"No suitable FT_ThingCategoryDef found for {searchedDef.defName} and it has no existing FT_ThingCategoryDef: placing it in FT_Foods.");
                    parentFlavorCategories.Add(ThingCategoryDef.Named("FT_Foods"));
                }
                else { Log.Warning($"No better FT_ThingCategoryDef found for {searchedDef.defName}: keeping its original FT_ThingCategoryDef of {parentFlavorCategories[i]}"); }
            }
        }
        return parentFlavorCategories;

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
                    foreach (IngredientCount ing in flavorDef.ingredients)
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


