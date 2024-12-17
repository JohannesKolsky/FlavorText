using System.Collections.Generic;
using Verse;
using System.Text.RegularExpressions;
using System.Linq;

// Verse.ThingCategoryNodeDatabase.FinilizeInit() is what adds core stuff to ThingCategoryDef.childCategories

namespace FlavorText;


    [StaticConstructorOnStartup]
    public static class FlavorTextUtilities
    {
        public static List<ThingCategoryDef> flavorThingCategoryDefs = [];  // list of all FlavorText related ThingCategoryDefs

        static FlavorTextUtilities()
        {
            DefDatabase<ThingCategoryDef>.ResolveAllReferences();  // I don't think this does anything atm; see corresponding CompFlavor.GetFlavorText ResolveReferences call
        }

        // get all FlavorText ThingCategoryDefs (they start with FT_)
        public static void CompileFlavorCategories()
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
        public static void GetFlavorCategoryChildren()
        {
            GetUnlistedChildren();

/*            // copy over vanilla child ThingDefs and ThingCategoryDefs from special fields of ThingCategoryDefs to the actual ones used by the game
            static void GetListedChildren()
            {
                Log.Warning("Getting Listed Children");
                // get ThingDefs
                foreach (ThingCategoryDef thingCategoryDef in flavorThingCategoryDefs)
                {
                    List<ThingDef> flavorChildThingDefs = thingCategoryDef.GetModExtension<FlavorCategoryModExtension>().flavorChildThingDefs;
                    foreach (ThingDef thingDef in flavorChildThingDefs)
                    {
                        if (!thingCategoryDef.childThingDefs.Contains(thingDef))
                        {
                            thingCategoryDef.childThingDefs.Add(thingDef);
                        }
                    }
                }

            // get ThingCategories
            foreach (ThingCategoryDef thingCategoryDef in flavorThingCategoryDefs)
            {
                List<ThingCategoryDef> flavorChildCategories = thingCategoryDef.GetModExtension<FlavorCategoryModExtension>().flavorChildCategories;
                foreach (ThingCategoryDef childCategory in flavorChildCategories)
                {
                    if (!thingCategoryDef.ThisAndChildCategoryDefs.Contains(childCategory))
                    {
                        thingCategoryDef.childCategories.Add(childCategory);
                    }
                }
            }
        }*/
            // look through all ThingDefs and determine which ones fit into which FT_ThingCategories
            static void GetUnlistedChildren()
            {
                Log.Warning("starting unlisted ThingDefs");
                foreach (ThingDef ingredient in DefDatabase<ThingDef>.AllDefs)
                {
                    if (!ThingCategoryDef.Named("Foods").ContainedInThisOrDescendant(ingredient)) { continue; }  // if it's not in "Foods" category, ignore it
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
                foreach (ThingCategoryDef category in DefDatabase<ThingCategoryDef>.AllDefs)
                {
                    if (!category.Parents.Contains(ThingCategoryDef.Named("Foods"))) { continue; }  // if it's not in "Foods" category, ignore it
                    if (category == DefDatabase<ThingCategoryDef>.GetNamed("FT_Beet")) { Log.Message("examining category " + category.defName); }
                    HashSet<string> splitNames = [];
                    // try to find word boundaries in the defName and label and split it into those words
                    string defNames = Regex.Replace(category.defName, "([_])|([-])", " ");
                    defNames = Regex.Replace(defNames, "([A-Z][a-z]*)", " $1");
                    defNames = defNames.ToLower();
                    string[] splitDefNames = defNames.Split(' ');
                    foreach (string defName in splitDefNames) { splitNames.Add(defName); }

                    string labels = Regex.Replace(category.label, "([-])", " ");
                    string[] splitLabels = labels.Split(' ');
                    foreach (string label in splitLabels) { splitNames.Add(label); }

                    PlaceIntoCategories(category, splitNames);
                }
            }
        }


        // compare the split names to the keywords from each FT_ThingCategory; the category which has 1+ keyword match one of the names the most times is the best, place it in there
        static void PlaceIntoCategories(ThingDef ingredient, HashSet<string> splitNames)
        {
            ThingCategoryDef bestFlavorCategoryMatch = BestCategory(splitNames);
            bestFlavorCategoryMatch.childThingDefs.Add(ingredient);  // add the ThingDef to the best FT_ThingCategory }
        }

        // overload for ThingCategoryDef
        static void PlaceIntoCategories(ThingCategoryDef category, HashSet<string> splitNames)
        {
            ThingCategoryDef bestFlavorCategoryMatch = BestCategory(splitNames);
            bestFlavorCategoryMatch.childCategories.Add(category);  // add the ThingCategoryDef to the best FT_ThingCategory }
        }


        private static ThingCategoryDef BestCategory(HashSet<string> splitNames)
        {
            ThingCategoryDef bestFlavorCategory = null;
            int keywordScore;
            int categoryScore;
            int bestCategoryScore = 0;
            bool tag = false;
            foreach (string subName in splitNames)
            {
                if (subName.Contains("beet"))  // TODO: FT_Beets seems to go into FT_Foods rather than FT_RootVeg
                {
                    Log.Warning("TAG!");
                    tag = true;
                    foreach (string name in splitNames) { Log.Message(name); }
                    break;
                }
            }
            foreach (ThingCategoryDef flavorCategory in flavorThingCategoryDefs)
            {
                keywordScore = 0;
                categoryScore = 0;
                List<string> keywords = flavorCategory.GetModExtension<FlavorCategoryModExtension>().keywords;

                foreach (string keyword in keywords)
                {
                    if (splitNames.Contains(keyword))  // +3 to score if the keyword matches an element exactly in splitNames (e.g. 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins])
                    {
                        keywordScore += 3;
                    }
                    foreach (string subName in splitNames)  // +2 to score each time the keyword matches a substring of an element in splitNames (e.g. 2x 'pumpkin' in [pumpkin, orange, smoothie, sugar, pumpkins]
                    {
                        if (subName.Contains(keyword))
                        {
                            keywordScore += 2;
                        }
                    }
                    if (keywordScore == 0)  // if the keyword hasn't matched anything yet (effectively, if it contains a space or will never match anything), +6 to score each time the keyword matches a substring of splitNames when they're all combined (e.g. 'sugar pumpkin' in "pumpkin orange smoothie sugar pumpkins")
                    {
                        string temp = string.Join(" ", splitNames);
                        keywordScore += 6 * Regex.Matches(temp, keyword).Count;
                    }
                    categoryScore += keywordScore;
                }
                if (categoryScore > 0) { categoryScore += flavorCategory.treeNode.nestDepth; }  // if there was a match add the category's depth in the category node tree, because deeper is more specific
                                                                                                // if you exceeded the best score so far, save the new score and category
                if (categoryScore > bestCategoryScore && flavorCategory != null)
                {
                    bestCategoryScore = categoryScore;
                    bestFlavorCategory = flavorCategory;
                }

                if (tag && bestFlavorCategory != null) { Log.Message("Best category currently is " + bestFlavorCategory + " with score of " + bestCategoryScore); }
            }
            // if nothing matched, select the Foods category
            if (bestCategoryScore == 0)
            {
                bestFlavorCategory = ThingCategoryDef.Named("FT_Foods");
            }
            return bestFlavorCategory;
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


