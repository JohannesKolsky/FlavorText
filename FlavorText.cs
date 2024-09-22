using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

//--TODO: make flavor entries a class
//--TODO: eggs + eggs makes weird names like omlette w/eggs
//--TODO: move databases to xml
//--TODO: dynamically build flavor database from all foodstuffs
//--TODO: organize flavors tightly: category-->category-->...-->item defName

//TODO: flavor label does not appear while meal is being carried directly from stove to stockpile
//TODO: merging stacks doesn't change the meal name
//TODO: options to prevent merging meals
//TODO: function to remove duplicate ingredients
//TODO: default to vanilla name on null
//TODO: big-ass null check error randomly
//TOTO: flavor descriptions

//fixedIngredientFilter: which items are allowed
//defaultIngredientFilter: default assignment of fixedIngredientFilter
//fixedIngredient: used if fixedIngredientFilter.Count == 1


namespace FlavorText
{
    /*    [StaticConstructorOnStartup]
        public static class HarmonyPatches
        {
            private static readonly Type patchType = typeof(HarmonyPatches);

            static HarmonyPatches()
            {
                var harmony = new Harmony(id: "rimworld.hekmo.flavortext");
                Harmony.DEBUG = true;

                harmony.Patch(AccessTools.Method(typeof(ThingFilter), nameof(ThingFilter.categories)), postfix: new HarmonyMethod(patchType, nameof(ThingFilter.customSummary)));
            }
        }*/

    public class CompFlavor : ThingComp
    {

        public CompProperties_Flavor Props => (CompProperties_Flavor)this.props;  // simplify fetching this Comp's Properties

        public CompIngredients ingredientComp;
        public string flavor;  // final flavor label
        /*        public FoodKind foodKindTags;*/

        private const int MaxNumIngredientsFlavor = CompIngredients.MaxNumIngredients;  // max number of ingredients used to find flavors

        public static readonly List<FlavorDef> flavorDefList = (List<FlavorDef>)DefDatabase<FlavorDef>.AllDefs;  // compile a list of all FlavorDefs


        /*private string GetFlavorOld(List<ThingDef> ingredientList)  // find a flavor label based on the ingredients
        {
            string name =

            List<string> nameList = [];
            string name;

            ingredientList.Capacity = MaxNumIngredientsFlavor;  // cut list down to size
            ingredientList = [.. ingredientList.OrderBy(e => e.defName)];  // sort list for unique key

            for (int i = 0; i < ingredientList.Count; i++)
            {
                try { name = flavorDictionary[ingredientList].flavorLabel; }  // try getting a flavor label
                catch (IndexOutOfRangeException) { name = null; }
                if (name != null)  // if you found a flavor label, add it to the name list and be done
                {
                    name = CleanupFlavorName(ingredientList, name);
                    nameList.Add(name);
                    break;
                }
                else // if you didn't find a flavor label yet, strip off the first ingredient and make a solo name with it
                {
                    Def soloIngredient = ingredientList[i];
                    ingredientList.Remove(soloIngredient);
                    name = GetSoloFlavor(soloIngredient);

                    name = CleanupFlavorName([soloIngredient], name);
                    nameList.Add(name);
                }
            }
            name = JoinFlavorNames(nameList);  // join all found flavor labels together
            return name;
        }*/

        /*private string GetSoloFlavor(Def soloEntry)  // try and get a 1-ingredient flavor
        {
            string soloFlavor = flavorDictionary[[soloEntry]].flavorLabel;
            return soloFlavor;
        }*/

        public string GetFlavor(List<ThingDef> ingredientsToSearchFor, List<FlavorDef> flavorDefList)  // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
        {
            if (ingredientsToSearchFor == null)
            {
                Log.Error("Ingredients to search for are null");
                return null;
            }
            else if (flavorDefList == null)
            {
                Log.Error("List of Flavor Defs is null");
                return null;
            }
            else if (flavorDefList.Count == 0)
            {
                Log.Error("List of Flavor Defs is empty");
                return null;
            }

            //see which FlavorDefs match with the ingredients in the meal, and make a list of them all
            List<FlavorDef> matchingFlavorDefs = [];
             foreach (FlavorDef flavorDef in flavorDefList)
            {
                if (IsMatchingFlavorDef(ingredientsToSearchFor, flavorDef))
                {
                    matchingFlavorDefs.Add(flavorDef);
                }
            }

            FlavorDef bestFlavorDef = null;

            // pick the most specific matching FlavorDef
            if (matchingFlavorDefs.Count > 0)
            {
                 bestFlavorDef = ChooseBestFlavorDef(matchingFlavorDefs);
            }

            // fill in placeholder names in the label
            if (bestFlavorDef != null)
            {
                string bestFlavor = FillInMeatNames(bestFlavorDef.label, ingredientsToSearchFor);
                return bestFlavor;
            }
            else { Log.Error("No matching FlavorDefs found."); return null; }
        }

        private bool IsMatchingFlavorDef(List<ThingDef> ingredientsToSearchFor, FlavorDef flavorDef)  // check if the ingredients match the given FlavorDef
        {
            if (ingredientsToSearchFor.Count >= flavorDef.ingredients.Count)  // FlavorDef can't have more ingredients than the meal has
            {
                List<bool> matches = (from c in Enumerable.Range(0, flavorDef.ingredients.Count) select false).ToList();  // keeps track of how much of the FlavorDef you've matched so far
                for (int i = 0; i < ingredientsToSearchFor.Count; i++) // check each ingredient you're searching for with the FlavorDef
                {
                    matches = BestIngredientMatch(ingredientsToSearchFor[i], flavorDef, matches);  // see if the ingredient fits and if it does, find the most specific match
                }
                if (matches.Contains(false) || matches.Count == 0)
                {
                    return false; // the FlavorDef doesn't match
                }
                return true;  // the FlavorDef matches completely
            }
            return false;  // FlavorDef had too many ingredients
        }

        private List<bool> BestIngredientMatch(ThingDef ingredient, FlavorDef flavorDef, List<bool> matches)  // find the best match for the current single ingredient in the current FlavorDef
        {
            int bestIndex = -1;

            for (int index = 0; index < flavorDef.ingredients.Count && matches[index] == false; index++)  // compare the given ingredient to the FlavorDef's ingredients to see which it matches best with
            {
                if (flavorDef.ingredients[index].filter.Allows(ingredient))
                {
                    // if you matched with a fixed ingredient, that's the best
                    if (flavorDef.ingredients[index].IsFixedIngredient)
                    {
                        matches[index] = true;
                        return matches;
                    }
                    else if (bestIndex != -1)
                    {  
                        // if the current FlavorDef ingredient is the most specific so far, mark its index
                        if (flavorDef.ingredients[index].filter.AllowedDefCount < flavorDef.ingredients[bestIndex].filter.AllowedDefCount)
                        {
                            bestIndex = index;
                        }
                    }
                    else { bestIndex = index; }  // if this is the first match, mark the index
                }
            }
            if (bestIndex != -1) { matches[bestIndex] = true;}  // you found a match
            return matches;
        }
        
        private FlavorDef ChooseBestFlavorDef(List<FlavorDef> validFlavorDefs)  // rank valid flavor defs to choose the best
        {
            if (validFlavorDefs != null && validFlavorDefs.Count != 0)
            {
                validFlavorDefs.SortBy(v => v.specificity);
                return validFlavorDefs.FirstOrDefault();
            }
            Log.Error("no valid flavorDefs to choose from");
            return null;
        }

        private string CleanupFlavorName(List<ThingDef> ingredients, string name)  // make the flavor label look nicer and replace placeholder text
        {
            foreach (ThingDef entry in ingredients)
            {
                if (name != null)
                {
                    if (entry.thingCategories.Any(cat => cat.defName == "MeatRaw"))  // if the ingredient is meat, replace placeholders and do some grammar stuff
                    {
                        string meatType = entry.label;  //label name
                        if (entry.defName == "Meat_Twisted")
                        {
                            name = ReplacePlaceholder(name, "{M} and", "{M}"); // remove 1 instance of "and"
                            if (name.IndexOf("{M}") == name.Length - "{M}".Length) { name = ReplacePlaceholder(name, "{M}", "twisted meat"); }  // if it's at the end, use "twisted meat"
                            else { name = ReplacePlaceholder(name, "{M}", "twisted"); }  // otherwise use "twisted"
                        }
                        else if (entry.defName == "Meat_Human")
                        {
                            name = ReplacePlaceholder(name, "{M} and", "{M}");
                            if (name.IndexOf("{M}") == name.Length - "{M}".Length) { name = ReplacePlaceholder(name, "{M}", "long pork"); }
                            else { name = ReplacePlaceholder(name, "{M}", "cannibal"); }
                        }
                        else if (entry.defName == "Meat_Megaspider") { name = ReplacePlaceholder(name, "{M} and", "{M}"); name = ReplacePlaceholder(name, "{M}", "bug"); }
                        else  // otherwise generic meat name minus the meat part
                        {
                            meatType = meatType.Replace(" meat", ""); // remove "meat" from the name
                            name = ReplacePlaceholder(name, "{M}", meatType);
                        }

                    }
                    else if (entry.thingCategories.Any(cat => cat.defName == "EggsFertilized" || cat.defName == "EggsUnfertilized"))  // replace egg placeholder with egg label (currently just "Egg")
                    {
                        name = ReplacePlaceholder(name, "{E}", "Eggs");
                    }
                    return name;
                }
            }
            return "flavor is null in CleanupFlavorName";
        }

        private string ReplacePlaceholder(string input, string placeholder, string replacement)  // replace the first given placeholder with the given name
        {
            int index = input.IndexOf(placeholder);
            if (index != -1)
            {
                input = input.Remove(index, placeholder.Length);
                input = input.Insert(index, replacement);
            }
            return input;
        }

        private string FillInMeatNames(string flavor, List<ThingDef> ingredients)
        {
            foreach (ThingDef ingredient in ingredients)
            {
                flavor = flavor.Formatted(ingredient.label);
            }
            return flavor;
        }

        private string JoinFlavorNames(List<string> nameList)  // combine the found flavor labels into a single flavor label
        {
            // build the flavor label, compositing smaller flavor labels if needed
            ingredientComp = parent.GetComp<CompIngredients>();  // get the ingredients comp of the parent meal
            if (nameList.Count == 1) { return nameList[0]; }
            else if (nameList.Count == 2) { return nameList[1] + " with " + nameList[0]; }
            else if (nameList.Count == 3) { return nameList[2] + " with " + nameList[1] + " and " + nameList[0]; }
            else { return null; }
        }

        public override string TransformLabel(string baseLabel)  // transform the original label into the flavor label: make a list of ingredients, look them up in the flavor dictionary, and assign a flavor label
        {
            if (flavor == null)  // if no flavor name, find one
            {
                ingredientComp = parent.GetComp<CompIngredients>();  // get the ingredients comp of the parent meal
                List<ThingDef> ingredientList = ingredientComp.ingredients;  // list of ingredients
                string bestFlavor = GetFlavor(ingredientList, flavorDefList);  // single best Flavor Def from all Flavor Defs available
                if (bestFlavor != null)
                {
                    flavor = GenText.CapitalizeAsTitle(bestFlavor);
                    return flavor;
                }
            }
            if (flavor != null) { return flavor; }  // assuming you found a flavor name, transform the meal's original label to that
            return baseLabel;  // if you didn't find a flavor name, keep the meal's original label
        }


        public override string CompInspectStringExtra()  // if you've successfully created a new flavor label, replace the original name with the flavor label, and move the original name down
        {
            if (flavor != null)
            {
                StringBuilder stringBuilder = new();
                string typeLabel = GenText.CapitalizeAsTitle(parent.def.label);  // what kind of meal it is
                stringBuilder.AppendLine(typeLabel);
                return stringBuilder.ToString().TrimEndNewlines();
            }
            else { return null; }
        }


        public override void PostExposeData()  // include the flavor label in game save files
        {
            base.PostExposeData();
            Scribe_Values.Look(ref flavor, "flavor", null);
        }
    }

    public class CompProperties_Flavor : CompProperties
    {

        public CompProperties_Flavor()
        {
            this.compClass = typeof(CompFlavor);
        }

        public CompProperties_Flavor(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}