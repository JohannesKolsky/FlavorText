using JetBrains.Annotations;
using RimWorld;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Policy;
using System.Text;
using Verse;

//--TODO: make flavor entries a class
//--TODO: eggs + eggs makes weird names like omlette w/eggs


//TODO: streamline and simplify
//TODO: move databases to xml
//TODO: dynamically build flavor database from all foodstuffs
//TODO: flavor name does not appear while meal is being carried directly from stove to stockpile
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
        public string flavor;
        public string searchedIngredientName;
        public string displayedIngredient;
/*        public FoodKind foodKindTags;*/

        private const int MaxNumIngredientsFlavor = CompIngredients.MaxNumIngredients;  // max number of ingredients used to find flavors

        private static readonly IOrderedEnumerable<FlavorDef> flavorDefList = DefDatabase<FlavorDef>.AllDefs.OrderByDescending((FlavorDef f) => f.defName);

        private readonly Hashtable flavorHashTable = [];

        CompFlavor()
        {
            foreach (FlavorDef flav in flavorDefList)  // build a hash table containing all flavor names
            {
                List<string> ingredientKey = flav.foodStuffs;
                ingredientKey.Sort();  // sort ingredient list to get unique key
                flavorHashTable.Add(ingredientKey, flav);
            }
        }

        private string GetFlavor(List<ThingDef> ingredientList)  // find a flavor name based on the ingredients
        {
            List<string> nameList = [];
            string name;

            ingredientList.Capacity = MaxNumIngredientsFlavor;  // cut list down to size
            ingredientList.Sort();  // sort list for unique key

            for (int i = 0; i <  ingredientList.Count; i++)
            {
                try { name = ((FlavorDef)flavorHashTable[ingredientList]).label; }  // try getting a flavor name
                catch (IndexOutOfRangeException) { name = null; }
                if (name != null)  // if you found a flavor name, add it to the name list and be done
                {
                    name = CleanupFlavorName(ingredientList, name);
                    nameList.Add(name);
                    break;
                }
                else // if you didn't find a flavor name yet, strip off the first ingredient and make a solo name with it
                {
                    ThingDef soloIngredient = ingredientList[i];
                    ingredientList.Remove(soloIngredient);
                    name = GetSoloFlavor(soloIngredient);

                    name = CleanupFlavorName([soloIngredient], name);
                    nameList.Add(name);
                }
            }
            name = JoinFlavorNames(nameList);  // join all found flavor names together
            return name;
        }

        private string GetSoloFlavor(ThingDef soloEntry)  // try and get a 1-ingredient flavor
        {
            string soloFlavor = ((FlavorDef)flavorHashTable[soloEntry]).label;
            return soloFlavor;
        }

        private string CleanupFlavorName(List<ThingDef> ingredients, string name)  // make the flavor name look nicer and replace placeholder text
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

        private string JoinFlavorNames(List<string> nameList)  // combine the found flavor names into a single flavor name
        {
            // build the flavor name, compositing smaller flavor names if needed
            if (nameList.Count == 1) { return nameList[0]; }
            else if (nameList.Count == 2) { return nameList[1] + " with " + nameList[0]; }
            else if (nameList.Count == 3) { return nameList[2] + " with " + nameList[1] + " and " + nameList[0]; }
            else { return null; }
        }
      
        public override string TransformLabel(string label)  // transform the original label into the flavor name: make a list of ingredients, look them up in the flavor table, and assign a flavor name
        {
            if (flavor == null)
            {
                ingredientComp = parent.GetComp<CompIngredients>();  // get the ingredients comp of the parent meal
                List<ThingDef> ingredientList = ingredientComp.ingredients;  // list of ingredients
                string flavor = GetFlavor(ingredientList);
                flavor = GenText.CapitalizeAsTitle(flavor);
                if (flavor != null) { return flavor; }
                else
                {
                    throw new NullReferenceException("Failed to find flavor name for ingredient combination.");
                }
            }
            else { return label; }
        }


        public override string CompInspectStringExtra()  // if you've successfully created a new flavor label, replace the original name with the flavor label, and move the original name down
        {
            if (flavor != null)
            {
                StringBuilder stringBuilder = new();
                string typeLabel = GenText.CapitalizeAsTitle(parent.def.label);
                stringBuilder.AppendLine(typeLabel);
                return stringBuilder.ToString().TrimEndNewlines();
            }
            else { return null; }
        }


        public override void PostExposeData()  // include the flavor name in game save files
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