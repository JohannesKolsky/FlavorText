using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Verse;

namespace FlavorText
{
    internal static class InflectionUtility
    {

        private static bool tag;

        public const int numInflections = 4;

        internal static Dictionary<ThingDef, List<string>> ThingInflectionsDictionary = DefDatabase<ThingDefInflectionsData>.AllDefs
            .Where(dict => dict.packageID is null || ModLister.GetActiveModWithIdentifier(dict.packageID) is not null)
            .SelectMany(dict => dict.dictionary)
            .ToDictionary(kvp => DefDatabase<ThingDef>.GetNamed(kvp.Key), kvp => kvp.Value);

        internal static Dictionary<FlavorCategoryDef, List<string>> CategoryInflectionsDictionary = DefDatabase<FlavorCategoryDefInflectionsData>.AllDefs
            .Where(dict => ModLister.GetActiveModWithIdentifier(dict.packageID) is not null)
            .SelectMany(dict => dict.dictionary)
            .ToDictionary(kvp => DefDatabase<FlavorCategoryDef>.GetNamed(kvp.Key), kvp => kvp.Value);

        // get various grammatical forms of each ingredient
        internal static void AssignIngredientInflections()
        {
            foreach (ThingDef ingredient in FlavorCategoryDefOf.FT_Foods.DescendantThingDefs.Distinct().ToList())
            {
                try
                {
                    {
                        // try and get inflections defined in the XML
                        List<string> inflections = ThingInflectionsDictionary.TryGetValue(ingredient);
                        if (inflections is null)
                        {
                            ThingInflectionsDictionary.Add(ingredient, []);
                            if (tag) Log.Warning($"Could not find {ingredient} in the thingDefs of predefined inflections, checking category overrides...");
                            inflections = CategoryInflectionsDictionary.TryGetValue(CategoryUtility.ThingCategories[ingredient].First());
                            if (inflections is null)
                            {
                                if (tag) Log.Warning($"Could not find {ingredient} in the thingDefs of predefined category inflections, will generate inflections instead.");
                                inflections = [];
                            }
                        }

                        // generate inflections
                        inflections = GenerateIngredientInflections(ingredient, inflections);
                        if (inflections.Any(inflect => inflect.NullOrEmpty()))
                        {
                            string errorString = $"\nplur = {inflections[0]}\ncoll = {inflections[1]}\nsing = {inflections[2]}\nadj = {inflections[3]}";
                            throw new NullReferenceException($"Generated inflections for {ingredient} but some or all of them were null" + errorString);
                        }
                        ThingInflectionsDictionary[ingredient] = inflections;
                        //Log.Warning($"Found all predefined inflections for {ingredient}");
                        //Log.Message($"\nplur = {inflections[0]}\ncoll = {inflections[1]}\nsing = {inflections[2]}\nadj = {inflections[3]}");
                    }

                }
                catch (Exception ex)
                {
                    Log.Error($"Error when getting inflections for {ingredient}. {ex}");
                }
            }
        }
        // generate various grammatical forms of each ingredient
        internal static List<string> GenerateIngredientInflections(ThingDef ingredient, List<string> inflections)
        {
            /*if (ing.defName.ToLower().Contains("egg")) { tag = true; }*/
            // plural form // a dish made of CABBAGES that are diced and then stewed in a pot
            // collective form, singular/plural ending depending in real-life ing size // stew with CABBAGE  // stew with PEAS
            // singular form // a slice of BRUSSELS SPROUT
            // adjectival form // PEANUT brittle

            // if there were predefined inflections, fill them out as much as possible
            // depending on what's given, you may be able to skip inflection generation
            bool doGeneration = false;
            List<string> generatedInflections = [];

            if (!inflections.Empty())
            {
                if (inflections.Count != numInflections) throw new ArgumentOutOfRangeException($"{ingredient} had wrong number of inflections. Expected {numInflections} inflections, found {inflections.Count} instead");
                if (inflections[0] == "^") throw new NullReferenceException($"Got predefined inflections for {ingredient}, but its first inflection used '^' which is invalid.");
                for (int i = 0; i < inflections.Count; i++)
                {
                    if (inflections[i] == "_") generatedInflections.Add("");
                    else if (inflections[i] == "^") generatedInflections.Add(inflections[i - 1]);
                    else if (inflections[i] == "*" || inflections[i].Contains("{0}")) { generatedInflections.Add(null); doGeneration = true; }
                    else if (inflections[i].NullOrEmpty()) throw new NullReferenceException($"Got predefined inflections for {ingredient}, but one of them was null or an empty string");
                    else generatedInflections.Add(inflections[i]);
                }
            }
            else doGeneration = true;

            if (doGeneration) generatedInflections = AutoGenerateInflections();

            return generatedInflections;

            // determine correct inflections for plural, collective, singular, and adjectival forms of the ing's label
            List<string> AutoGenerateInflections()
            {
                string plur, coll, sing, adj;
                if (!generatedInflections.Empty())
                {
                    plur = generatedInflections[0];
                    coll = generatedInflections[1];
                    sing = generatedInflections[2];
                    adj = generatedInflections[3];
                }
                else plur = coll = sing = adj = null;

                List<(string, string)> singularPairs = [("ies$", "y"), ("sses$", "ss"), ("us$", "us"), ("([aeiouy][cs]h)es$", "$1"), ("([o])es$", "$1"), ("([^s])s$", "$1")];  // English conversions from plural to singular noun endings
                string temp;

                string labelOriginal = ingredient.label;  // (French) Gruyère cheese meal (fresh)
                if (tag) { Log.Warning($">>>starting label is {labelOriginal}"); }
                string defNameCompare = ingredient.defName;  // EX_GruyereCheese
                if (tag) { Log.Warning($">>>starting defName is {defNameCompare}"); }
                defNameCompare = Regex.Replace(defNameCompare, "([_])", " ");  // remove spacer chars // EX GruyereCheese

                // remove parentheses and their contents
                //TODO: this doesn't work with the delete list for some reason; probably some conflict between how C# and Regex read strings
                string labelNoParentheses = labelOriginal;
                temp = Regex.Replace(labelOriginal, @"\(.*\)", "").Trim();
                if (Regex.IsMatch(temp, "[a-zA-Z]")) { labelNoParentheses = temp; }  // accept deletion from label if letters remain  // Gruyère cheese meal

                // unnecessary whole words
                List<string> delete = ["meal", "leaf", "leaves", "stalks*", "seeds*", "cones*", "eggs*", "flour", "meat"];  // bits to delete

                // don't delete certain word combinations that include "meat"
                List<string> exemptCombinations = ["canned meat", "pickled meat", "dried meat", "dehydrated meat", "salted meat", "trimmed meat", "cured meat", "prepared meat", "marinated meat"];
                foreach (string combination in exemptCombinations)
                {
                    if (labelNoParentheses.ToLower() == combination)
                    {
                        delete.Remove("meat");
                        break;
                    }
                }

                // remove bits
                string labelDeleted = labelNoParentheses;
                foreach (string del in delete)
                {
                    temp = Regex.Replace(labelNoParentheses.ToLower(), $@"(?i)\b{del}\b", "").Trim();  // delete complete words from labelCompare that match those in "delete"
                    // accept deletion from labelCompare if letters remain
                    if (Regex.IsMatch(temp, "[a-zA-Z]"))
                    {
                        int head = labelNoParentheses.ToLower().IndexOf(temp, StringComparison.Ordinal);
                        labelDeleted = labelNoParentheses.Substring(head, temp.Length);  // Gruyère cheese
                    }
                    if (tag) { Log.Message($"deleted bit from labelCompare, is now {labelDeleted}"); }

                    temp = Regex.Replace(defNameCompare, $@"(?i)\b{del}\b", "").Trim();  // delete complete words from defNameCompare that match those in "delete"
                    if (Regex.IsMatch(temp, "[a-zA-Z]")) { defNameCompare = temp; }  // accept deletion from defNameCompare if letters remain
                    if (tag) { Log.Message($"deleted bit from defNameCompare, is now {defNameCompare}"); }
                }

                // remove diacritics and capitalization
                string labelCompare = Remove.RemoveDiacritics(labelDeleted);  // Gruyere cheese
                labelCompare = labelCompare.ToLower();  // gruyere cheese
                defNameCompare = Remove.RemoveDiacritics(defNameCompare); // EX GruyereCheese
                defNameCompare = Regex.Replace(defNameCompare, "(?<=[a-zA-Z])([A-Z][a-z]+)", " $1");  // split up name based on capitalized words  // EX Gruyere Cheese
                defNameCompare = Regex.Replace(defNameCompare, "(?<=[a-z])([A-Z]+)", " $1");  // split up names based on unbroken all-caps sequences  // E X Gruyere Cheese
                if (tag) { Log.Message($"split up defName, is now {defNameCompare}"); }
                defNameCompare = defNameCompare.ToLower();  // e x gruyere cheese

                // formulate inflections by comparing label and defName; if you're unable to, use the label
                string root = LongestCommonSubstring(defNameCompare, labelCompare);  // e.g. EX_GruyereCheese + GruyèreCheese => gruyere cheese
                if (tag) { Log.Warning($"Longest common substring was {root}"); }


                // try to get plural form from label, b/c it's usually plural
                // you can't just rely on checking -s endings b/c some names like "meat" will never end in -s
                // you can't rely on label on its own b/c it might have unnecessary words (e.g. "mammoth gold" pumpkins)
                if (plur is null)
                {
                    // if root is some generic term like "meat" or doesn't start at the start of a word, used reduced label instead
                    if (!root.NullOrEmpty() && !delete.Contains(root) && Regex.IsMatch(labelCompare, $"\\b{root}"))
                    {
                        // if plur has a placeholder, replace it with root
                        // otherwise plur is root extended to the end of the label // mammoth gold pumpkins & pumpkin => pumpkins
                        if (!inflections.Empty() && inflections[0].Contains("{0}")) plur = inflections[0].Formatted(root);
                        else
                        {
                            plur = Regex.Match(labelCompare, "(?i)" + root + "[^ ]*").Value;
                            if (tag) { Log.Message($"plural matched = {plur}"); }
                            int head = labelCompare.IndexOf(root, StringComparison.Ordinal);
                            plur = labelOriginal.Substring(head, plur.Length);  // get diacritics and capitalization back
                            if (tag) { Log.Message($"plural final = {plur}"); }

                            // if the 2 forms differ in length by more than 2 letters, discard them and use reduced label
                            if (root.Length == 0 || root.Length < plur.Length - 2)
                            {
                                if (tag) { Log.Message($"root was {root}"); }
                                plur = labelDeleted;
                                if (tag) { Log.Message($"plural fallback = {plur}"); }
                            }
                        }

                    }
                    // otherwise use reduced label
                    else
                    {
                        plur = labelDeleted;
                        if (tag) { Log.Message($"plural fallback2 = {plur}"); }
                    }
                }
                if (tag) { Log.Message($"plural final = {plur}"); }

                // try to get singular form from plural form
                // done this way so that plural form matches singular form if label and defName aren't similar (e.g. VCE_Oranges => mandarins when other mods are installed)
                if (sing is null)
                {
                    if (!inflections.Empty() && inflections[2].Contains("{0}")) sing = inflections[2].Formatted(root);
                    else
                    {
                        sing = plur;
                        foreach (var pair in singularPairs)
                        {
                            if (Regex.IsMatch(sing, pair.Item1))
                            {
                                sing = Regex.Replace(sing, pair.Item1, pair.Item2);
                                break;
                            }
                        }
                    }
                }
                if (tag) { Log.Message($"sing = {sing}"); }


                // try to get collective form (either based on singular or plural depending on FT_Category)
                if (coll is null)
                {
                    if (!inflections.Empty() && inflections[1].Contains("{0}")) coll = inflections[1].Formatted(root);
                    else
                    {
                        FlavorCategoryDef parentCategory = CategoryUtility.ThingCategories[ingredient].First();
                        bool? singularCollective = parentCategory.singularCollective;
                        coll = singularCollective == true ? sing : plur;
                    }
                }
                if (tag) { Log.Message($"coll = {coll}"); }

                // try to get adjectival form (based on singular)
                if (adj is null)
                {
                    if (!inflections.Empty() && inflections[3].Contains("{0}")) adj = inflections[3].Formatted(root);
                    else adj = sing;
                }
                if (tag) { Log.Message($"adj = {adj}"); }

                return [plur, coll, sing, adj];
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
        }}
}