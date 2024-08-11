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

//TODO: organize flavors tightly: category-->category-->...-->item defName
//TODO: flavor label does not appear while meal is being carried directly from stove to stockpile
//TODO: merging stacks doesn't change the meal name
//TODO: options to prevent merging meals
//TODO: function to remove duplicate ingredients
//TODO: big-ass null check error randomly

namespace FlavorText
{
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

        public FlavorDef GetFlavor(List<ThingDef> ingredientsToSearchFor, List<FlavorDef> flavorDefsToSearch)  // see which FlavorDefs match with the ingredients you have, and choose the most specific FlavorDef you find
        {
            if (ingredientsToSearchFor == null)
            {
                Log.Error("Ingredients to search for are null");
                return null;
            }
            else if (flavorDefsToSearch.Count == 0)
            {
                Log.Error("List of Flavor Defs is empty or null");
                return null;
            }

            //see how many FlavorDefs match with the ingredients in the meal
            List<FlavorDef> matchingFlavorDefs = [];
            foreach (FlavorDef flavorDef in flavorDefsToSearch)
            {
                if (IsMatchingFlavorDef(ingredientsToSearchFor, flavorDef))
                {
                    matchingFlavorDefs.Add(flavorDef);
                }
            }

            /*            FlavorDef finalFlavorDef = ChooseBestFlavorDef(matchingFlavorDefs);
                        return finalFlavorDef;*/
            if (matchingFlavorDefs.Count > 0) { return matchingFlavorDefs[0]; } else { Log.Error("No matching FlavorDefs found."); return null; }
        }

        private bool IsMatchingFlavorDef(List<ThingDef> ingredientsToSearchFor, FlavorDef flavorDef)
        {
            if (ingredientsToSearchFor.Count == flavorDef.ingredients.Count)
            {
                foreach (IngredientCount ingredientCount in flavorDef.ingredients)
                {
                    foreach (ThingDef thing in ingredientsToSearchFor)
                    {
                        if (!ingredientCount.filter.Allows(thing))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }
            else { return false; }
        }

        private FlavorDef ChooseBestFlavorDef(List<FlavorDef> validFlavorDefs)
        {
            FlavorDef bestFlavorDef = validFlavorDefs[0];
            if (bestFlavorDef != null)
            {
                for (int i = 1; i < validFlavorDefs.Count; i++)  // choose the most specific FlavorDef you found; note this for loop starts at 1 b/c bestFlavorDef is already the 0th
                {
                    if (validFlavorDefs[i].totalDepth < bestFlavorDef.totalDepth)
                    {
                        bestFlavorDef = validFlavorDefs[i];
                    }
                }
                return bestFlavorDef;
            }
        else { Log.Error("Failed to find any valid FlavorDefs for ingredient combination"); return null; }
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
            if (flavor == null)
            {
                ingredientComp = parent.GetComp<CompIngredients>();  // get the ingredients comp of the parent meal
                List<ThingDef> ingredientList = ingredientComp.ingredients;  // list of ingredients
                FlavorDef bestFlavorDef = GetFlavor(ingredientList, flavorDefList);
                string flavor = bestFlavorDef.label;
                flavor = GenText.CapitalizeAsTitle(flavor);
                if (flavor != null) { return flavor; }
                else
                {
                    throw new NullReferenceException("Failed to find flavor label for ingredient combination.");
                }
            }
            else { return baseLabel; }
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