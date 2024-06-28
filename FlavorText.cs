using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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

public static class CollectionExtension
{
    private static readonly Random rng = new();

    public static T RandomElement<T>(this IList<T> list)
    {
        return list[rng.Next(list.Count)];
    }

    public static T RandomElement<T>(this T[] array)
    {
        return array[rng.Next(array.Length)];
    }
}

namespace FlavorText
{
    public class CompFlavor : ThingComp
    {
        public CompProperties_Flavor Props => (CompProperties_Flavor)this.props;  // simplify fetching this Comp's Properties

        private readonly List<string> meatDatabase = [null, "Meat_Twisted", "Meat_Human", "MeatRaw", "Meat_Megaspider"];  // meat type order for nice grammar
        private readonly List<string> eggDatabase = ["EggChickenUnfertilized", "EggChickenFertilized", "EggCobraFertilized", "EggIguanaFertilized", "EggTortoiseFertilized", "EggCassowaryFertilized", "EggEmuFertilized", "EggOstrichFertilized", "EggTurkeyFertilized", "EggDuckUnfertilized", "EggDuckFertilized", "EggGooseUnfertilized", "EggGooseFertilized"];  // egg order in case it's needed later
        public List<string> ingredientDatabase = [null, "MeatRaw", "RawAgave", "RawBerries", "RawCorn", "RawFungus", "RawPotatoes", "RawRice", "Eggs", "Milk"];  // ingredient order for indexing

        // possible meal names (i.e. flavors)
        // this array removes invalid ingredient combinations in order to shrink the array, so indices must be passed through CompressIndices to target the correct name
        private readonly string[][][] flavorDatabase =
            [[[null, "grilled {M}", "candy", "berries", "corn on the cob", "mushroom soup", "baked potatoes", "mochi", "hard-boiled {E}", "yogurt"], ["{M} and {M} sausage", "teriyaki {M}", "{M} mincemeat", "{M} tamales", "{M} Diane", "{M} cepelinai", "{M} congee", "{M} and {E}", "{M} tikka masala"], [null, "berry jam", "kettle corn", "kombucha", "tapioca", "horchata", "macarons", "gelato"], [null, "cornberry muffins", "mushroom stuffing", "raggmunkar", "eight-treasure rice", "berry curd", "berry yogurt"], [null, "huitlacoche tamales", "succotash", "corn rice", "corn fritters", "elote"], [null, "potato mushroom stew", "mushroom stir fry", "mushroom cups", "cream of mushroom soup"], [null, "potato porridge", "potato pancakes", "mashed potatoes"], [null, "omurice", "rice porridge"], [null, "omlette"], [null]], [["{M} and {M} and {M} sausage", "{M} and {M} summer sausage", "{M} and {M} breakfast sausage", "{M} and {M} corn dogs", "{M} and {M} stuffed mushrooms", "{M} and {M} bangers and mash", "{M} and {M} jambalaya", "{M} and {M} Scotch egg", "{M} and {M} sausage gravy"], [null, "{M} with sweet berry sauce", null, null, null, "wok fried {M}", "sweet {M} frittata", null], [null, null, null, null, "{M} pilaf", "{M} mincemeat pudding", null], [null, "huitlacoche tacos", "{M} chuño", "{M} tacos", "{M} ramen", null], [null, "{M} and mushroom spud pies", null, "{M} mushroom frittata", "{M} stroganoff"], [null, "{M} katsu curry", "{M} papa rellena", "{M} shepherd's pie"], [null, "{M} fried rice", "{M} curry"], [null, "{M} omlette"], [null]], [[null, null, null, null, null, null, null, null], [null, "sweet corn salad", "sweet mushroom salad", "sweet potato salad", "kutya", "berry souffle", "berry gelato"], [null, null, null, "corn sushi", "corn donuts", "cornflakes"], [null, null, "enoki sushi", null, null], [null, "potato sushi", null, "aloo ka halwa"], [null, "tamago sushi", "ferni"], [null, "ice cream"], [null]], [[null, null, null, null, null, null, null], [null, null, null, null, null, null], [null, null, null, null, null], [null, null, null, null], [null, null, "suti polo"], [null, "berry custard"], [null]], [[null, null, null, null, null, null], [null, null, null, "mushroom corn frittata", "huitlacoche quesadillas"], [null, null, "potato corn frittata", "mashed potatoes and corn"], [null, "corn fried rice", "cornbread"], [null, "elote"], [null]], [[null, null, null, null, null], [null, null, "mushroom potato frittata", "mushroom potato pie"], [null, "mushroom fried rice", "risotto"], [null, "mushroom omlette"], [null]], [[null, null, null, null], [null, "potato fried rice", "potato rice porridge"], [null, "croquette"], [null]], [[null, null, null], [null, "rice pudding"], [null]], [[null, null], [null]], [[null]]];


        public CompIngredients ingredientComp;
        public string flavor;
        public string searchedIngredientName;
        public string displayedIngredient;
        public FoodKind foodKindTags;


        internal record struct FlavorEntry(int FlavorIndex, string FlavorDefName, string FlavorLabel)
        {
            public static implicit operator (int, string, string)(FlavorEntry value)
            {
                return (value.FlavorIndex, value.FlavorDefName, value.FlavorLabel);
            }

            public static implicit operator FlavorEntry((int, string, string) value)
            {
                return new FlavorEntry(value.Item1, value.Item2, value.Item3);
            }
        }

        private const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors; changing this requires reworking the entire mod's code
        /*        string originalLabelCapNoCount;*/

        private List<FlavorEntry> AddFlavorEntry(ThingDef ing, List<FlavorEntry> flavorEntries)  // add a new ingredient entry to the list of ingredients important for flavor
        {
            int ingIndex = 0;
            string labelName = null;
            FlavorEntry flavorEntry;

            foreach (ThingCategoryDef cat in ing.thingCategories)  // if it's meat or eggs, use the category name
            {
                if (cat.defName == "MeatRaw") // if it's meat
                {
                    ingIndex = 1;
                }
                else if (cat.defName == "EggsUnfertilized" || cat.defName == "EggsFertilized")  // if it's eggs
                {
                    foreach (FlavorEntry entry in flavorEntries)  // check if eggs are already on the list of flavor entries
                    {
                        if (entry.FlavorLabel == "eggs") { return flavorEntries; }
                    }
                    ingIndex = 8;
                    labelName = "eggs";
                }
            }

            if (ingIndex == 0 && (ing.defName == "RawToxipotato" || ing.defName == "RawPotatoes"))  // convert toxipotatoes to potatoes
            { 
                foreach (FlavorEntry entry in flavorEntries)  // check if either kind of potatoes are already on the list of flavor entries
                {
                    if (entry.FlavorLabel == "potatoes") { return flavorEntries; }
                }
                ingIndex = 6;
                labelName = "potatoes";
            }

            else if (ingIndex == 0 && ing.defName != null)  // if no searchName yet, use the defName
            {
                ingIndex = ingredientDatabase.IndexOf(ing.defName);
            }

            if (ingIndex != 0)
            {
                labelName ??= ing.label;
                flavorEntry = (ingIndex, ing.defName, labelName);  // index, defName, and label of each ingredient; unsupported ingredients have index -1
/*                if (flavorEntry.FlavorDefName == "Meat_Twisted") // add twisted meat to the front (for grammar)
                {
                    flavorEntries = flavorEntries.Prepend(flavorEntry).ToList();
                }*//*
                else { flavorEntries.Add(flavorEntry); }  // otherwise add ingredient to the back*/
                flavorEntries.Add(flavorEntry);
                return flavorEntries;
            }
            else { Log.Message("ingredient does not exist"); return flavorEntries; }
        }

        private List<int> ConvertIndices(List<int> indices)  // compress the indices to search the flavor database
        {
            List<int> indicesCompressed = [0, 0, 0];
            for (int i = MaxNumIngredientsFlavor - 1; i >= 0; i--)  // copy indices and compress
            {
                indicesCompressed[i] = indices[i];
                if (i > 0 && indices[i] != -1 && indices[i-1] != -1)  // if neither the index and the one preceding it are -1, and you aren't at the first element, subtract the predecessor from the index
                {
                    indicesCompressed[i] -= indices[i - 1];
                }
            }
            return indicesCompressed;
        }
        private string GetFlavor(List<FlavorEntry> Entries)  // find the matching flavor name based on the ingredients
        {
            List<int> indicesLong = [];
            List<string> nameList = [];
            string name;

            if (flavor == null)
            {
                for (int e = 0; e < MaxNumIngredientsFlavor; e++)
                {
                    indicesLong.Add(Entries[e].FlavorIndex); // pull the ingredient indices
                }
                List<int> indicesShort = ConvertIndices(indicesLong);  // convert indices to compressed format and replace unsupported ingredients with 0 index

                for (int i = 0; i < MaxNumIngredientsFlavor; i++)
                {
                    try { name = flavorDatabase[indicesShort[0]][indicesShort[1]][indicesShort[2]]; }  // try getting a flavor name
                    catch (IndexOutOfRangeException) { name = null; }
                    if (name != null)
                    {
                        nameList.Add(name);
                        break;
                    }
                    else // if invalid name, strip off the first ingredient and see if you can make a solo name with it
                    {
                        indicesLong[i] = 0;
                        indicesShort = ConvertIndices(indicesLong);
                        nameList = CheckGetSoloFlavor(Entries[i], nameList);
                    }
                }
                name = BuildFlavorName(nameList);  // find the flavor name
                for (int i = 0; i < MaxNumIngredientsFlavor; i++)  // clean up the flavor name and replace placeholders for meat/eggs
                {
                    if (Entries[i].FlavorIndex > 0) { name = CleanupFlavorName(Entries[i], name); }
                }

                flavor = name;
            }
            return flavor;
        }

        private List<string> CheckGetSoloFlavor(FlavorEntry entry, List<string> nameList)  // try and get a 1-ingredient flavor
        {
            string solo_flavor;

            switch (entry.FlavorIndex)
            {
                case > 0:
                    solo_flavor = flavorDatabase[0][0][entry.FlavorIndex];   // if supported ingredient find a 1-ingredient name
                    break;
                case -1:
                    solo_flavor = entry.FlavorLabel; // if unsupported ingredient use the label
                    break;
                default:  // if it's blank return the nameList unchanged
                    return nameList;
            }

            nameList.Add(solo_flavor);
            return nameList;
        }

        private string BuildFlavorName(List<string> nameList)  // combine the names into a final flavor name
        {
            // build the flavor name, compositing smaller flavor names if needed
            if (nameList.Count == 1) { return nameList[0]; }
            else if (nameList.Count == 2) { return nameList[1] + " with " + nameList[0]; }
            else if (nameList.Count == 3) { return nameList[2] + " with " + nameList[1] + " and " + nameList[0]; }
            else { return null; }
        }

        private string CleanupFlavorName(FlavorEntry entry, string name)  // make the flavor name look nicer and replace placeholder text
        {
            if (name != null)
            {
                if (entry.FlavorIndex == 1)  // replace the meat placeholder with the rest of the label, but removing the word "meat"
                {
                    string meatType = entry.FlavorLabel;  //label name
                    if (entry.FlavorDefName == "Meat_Twisted")
                    {
                        name = ReplacePlaceholder(name, "{M} and", "{M}"); // remove 1 instance of "and"
                        if (name.IndexOf("{M}") == name.Length - "{M}".Length) { name = ReplacePlaceholder(name, "{M}", "twisted meat"); }  // if it's at the end, use "twisted meat"
                        else { name = ReplacePlaceholder(name, "{M}", "twisted"); }  // otherwise use "twisted"
                    }
                    else if (entry.FlavorDefName == "Meat_Human")
                    {
                        name = ReplacePlaceholder(name, "{M} and", "{M}");
                        if (name.IndexOf("{M}") == name.Length - "{M}".Length) { name = ReplacePlaceholder(name, "{M}", "long pork"); }
                        else { name = ReplacePlaceholder(name, "{M}", "cannibal"); }
                    }
                    else if (entry.FlavorDefName == "Meat_Megaspider") { name = ReplacePlaceholder(name, "{M} and", "{M}"); name = ReplacePlaceholder(name, "{M}", "bug"); }
                    else  // otherwise generic meat name minus the meat part
                    {
                        meatType = meatType.Replace(" meat", ""); // remove "meat" from the name
                        name = ReplacePlaceholder(name, "{M}", meatType);
                    }

                }
                else if (entry.FlavorIndex == 8)  // replace egg placeholder with egg label (currently just "Egg")
                {
                    name = ReplacePlaceholder(name, "{E}", "Eggs");
                }
                return name;
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

        private List<ThingDef> MakeRandomIngredients()  // make random combinations of eggs and milk for meals that have no ingredients
        {

            List<string> vegetarianIngredientList = ["Eggs", "Milk"];
            List<string> eggList = new(eggDatabase);
            List<ThingDef> randomIngredients = [];

            for (int k = 0; k < MaxNumIngredientsFlavor; k++)
            {
                if (k > 0) { vegetarianIngredientList.Add(null); }  // first iteration no null option, then +1 null option in each subsequent iteration
                string vegIng = vegetarianIngredientList.RandomElement<string>();
                if (vegIng == "Milk") { vegetarianIngredientList.Remove(vegIng); } // remove milk if it was chosen
                else if (vegIng == "Eggs")  // if it was eggs, pick a random egg type and remove it from the list of egg types
                {
                    vegIng = eggList.RandomElement<string>();
                    eggList.Remove(vegIng);
                }

                if (vegIng != null)
                {
                    ThingDef vegIngDef = DefDatabase<ThingDef>.GetNamed(vegIng);
                    randomIngredients.Add(vegIngDef);
                }
            }
            return randomIngredients;
        }

        private List<FlavorEntry> MoveToFront(List<FlavorEntry> Entries, FlavorEntry element)
        {
            Entries.Remove(element);
            Entries.Insert(0, element);
            return Entries;
        }

         
        public override string TransformLabel(string label)  // transform the original label into the flavor name: make list of ingredients, look them up on flavor table, and assign a flavor label
        {
            if (flavor == null)
            {
                ingredientComp = parent.GetComp<CompIngredients>();  // get the ingredients comp of the parent meal
                List<ThingDef> ingredientList = ingredientComp.ingredients;  // list of ingredients
                FlavorEntry flavorEntry;
                List<FlavorEntry> flavorEntries = [];  // start a new list to copy the relevant ingredient info to

/*                if (ingredientList.Count == 0) // if no ingredients, make up random vegetarian ones  // disabled for now due to mod conflicts (VCE canned meat, GAB pickled meat are meals)
                {
                    ingredientList = MakeRandomIngredients();
                    foreach (ThingDef ing in ingredientList)  // add the random ingredients to the meal's ingredient list
                    {
                        ingredientComp.RegisterIngredient(ing);
                    }
                }*/

                for (int t = 0; t < ingredientList.Count && flavorEntries.Count < MaxNumIngredientsFlavor; t++) // fetch the data on each ingredient
                {
                    ThingDef ingredient = ingredientList[t];
                    flavorEntries = AddFlavorEntry(ingredient, flavorEntries);
                }

                while (flavorEntries.Count < MaxNumIngredientsFlavor)   // fill up any remaining space with a "blank" ingredient with index 0 and put them in the front
                {
                    flavorEntry = (0, null, null);
                    flavorEntries.Add(flavorEntry);
                }

                   List<FlavorEntry> flavorEntriesSorted = [.. flavorEntries.OrderBy(a => a.FlavorIndex)];

                flavor = GetFlavor(flavorEntriesSorted);  // triangulate the flavor name based on the ingredients
                flavor = GenText.CapitalizeAsTitle(flavor);
            }
            if (flavor != null) { return flavor; }
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


        public override void PostExposeData()  // allow the flavor name to be included in a game save file
        {
            base.PostExposeData();
            Scribe_Values.Look(ref flavor, "flavor", null);
        }
    }

    public class CompProperties_Flavor : CompProperties
    {

        public CompProperties_Flavor() // do when new Comp instance is created
        {
            this.compClass = typeof(CompFlavor);
        }

        public CompProperties_Flavor(Type compClass) : base(compClass)
        {
            this.compClass = compClass;
        }
    }
}