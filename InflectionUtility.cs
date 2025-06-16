using RimWorld.Planet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using Verse;

namespace FlavorText;

/// <summary>
/// various methods used to calculate stuff for ingredient name variations (e.g. singular vs plural)
/// </summary>

[StaticConstructorOnStartup]
internal static class InflectionUtility
{
    private static bool tag;

    public const int numInflections = 4;  // changing this requires rewriting this class, since currently it's designed for English and 4 grammatical forms

    // predefined inflections from XML for active mods
    internal static Dictionary<ThingDef, List<string>> ThingInflectionsDictionary = DefDatabase<ThingInflectionsData>.AllDefs
        .Where(dict => dict.packageID is null || ModLister.GetActiveModWithIdentifier(dict.packageID) is not null)
        .SelectMany(dict => dict.dictionary)
        .ToDictionary(kvp => DefDatabase<ThingDef>.GetNamed(kvp.Key), kvp => kvp.Value);

    // predefined category-wide inflections from XML for active mods
    internal static Dictionary<FlavorCategoryDef, List<string>> CategoryInflectionsData = DefDatabase<FlavorCategoryInflectionsData>.AllDefs
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
                    //tag = ingredient.defName.ToLower().Contains("vagp");
                    // try and get inflections defined in the XML
                    List<string> inflections = ThingInflectionsDictionary.TryGetValue(ingredient);
                    if (inflections is null)
                    {
                        ThingInflectionsDictionary.Add(ingredient, []);
                        if (tag) Log.Warning($"Could not find {ingredient} in the thingDefs of predefined inflections, checking category overrides...");
                        var thisAndParents = CategoryUtility.ThingCategories[ingredient].First().ThisAndParents;
                        foreach (var cat in thisAndParents)
                        {
                            inflections = CategoryInflectionsData.TryGetValue(cat);
                            if (inflections is not null) break;
                        }
                        if (inflections is null)
                        {
                            if (tag) Log.Warning($"Could not find {ingredient} in the thingDefs of predefined category inflections, will generate inflections instead.");
                            inflections = [];
                        }
                    }

                    // generate inflections
                    inflections = GenerateIngredientInflections(ingredient, inflections);
                    if (inflections.Any(inflect => inflect is null))
                    {
                        string errorString = $"\nplur = {inflections[0]}\ncoll = {inflections[1]}\nsing = {inflections[2]}\nadj = {inflections[3]}";
                        throw new NullReferenceException($"Generated inflections for {ingredient} but some or all of them were null" + errorString);
                    }
                    ThingInflectionsDictionary[ingredient] = inflections;
                    if (tag) Log.Message($"\nplur = {inflections[0]}\ncoll = {inflections[1]}\nsing = {inflections[2]}\nadj = {inflections[3]}");
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
        //tag = ingredient.defName.ToLower().Contains("pickled");

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
                else if (inflections[i] == "^") generatedInflections.Add(generatedInflections[i - 1]);
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
            string labelNoSpacers = Regex.Replace(labelOriginal, "([-])", " "); // remove spacer chars
            string defNameClean = ingredient.defName;  // EX_GruyereCheese
            defNameClean = Regex.Replace(defNameClean, "([_])", " ");  // remove spacer chars // EX GruyereCheese
            defNameClean = Regex.Replace(defNameClean, "(?<=[a-zA-Z])([A-Z][a-z]+)", " $1");  // split up name based on capitalized words  // EX Gruyere Cheese
            defNameClean = Regex.Replace(defNameClean, "(?<=[a-z])([A-Z]+)", " $1");  // split up names based on unbroken all-caps sequences  // E X Gruyere Cheese

            // remove parentheses and their contents
            //TODO: this doesn't work with the delete list for some reason; probably some conflict between how C# and Regex read strings
            string labelNoParentheses = labelNoSpacers;
            temp = Regex.Replace(labelOriginal, @"\(.*\)", "").Trim();
            temp = temp.Replace("  ", " ");
            if (Regex.IsMatch(temp, "[a-zA-Z]")) { labelNoParentheses = temp; }  // accept deletion from label if letters remain  // Gruyère cheese meal

            // unnecessary whole words
            List<string> delete = ["meal", "leaf", "leaves", "stalks*", "cones*", "grains*", "flour", "eggs*", "meat"];  // bits to delete

            // don't delete certain word combinations that include "meat"
            List<string> exemptCombinations = ["canned meat", "pickled meat", "dried meat", "dehydrated meat", "salted meat", "trimmed meat", "cured meat", "prepared meat", "marinated meat"];
            if (exemptCombinations.Any(combo => labelNoParentheses.ToLower() == combo)) delete.Remove("meat");

            // remove unnecessary whole words
            string labelBitsDeleted = labelNoParentheses;
            foreach (string del in delete)
            {
                temp = Regex.Replace(labelBitsDeleted.ToLower(), $@"(?i)\b{del}\b", "").Trim();  // delete complete words from labelCompare that match those in "delete"
                temp = temp.Replace("  ", " ");
                // accept deletion if letters remain
                if (Regex.IsMatch(temp, "[a-zA-Z]"))
                {
                    int head = labelNoParentheses.ToLower().IndexOf(temp, StringComparison.Ordinal);
                    if (head != -1) labelBitsDeleted = labelNoParentheses.Substring(head, temp.Length); // Gruyère cheese
                }

                temp = Regex.Replace(defNameClean, $@"(?i)\b{del}\b", "").Trim();  // delete complete words from defNameCompare that match those in "delete"
                temp = temp.Replace("  ", " ");
                if (Regex.IsMatch(temp, "[a-zA-Z]")) defNameClean = temp; // accept deletion from defNameCompare if letters remain
            }

            // remove diacritics and capitalization
            string labelClean = Remove.RemoveDiacritics(labelBitsDeleted);  // Gruyere cheese
            labelClean = labelClean.ToLower();  // gruyere cheese
            defNameClean = Remove.RemoveDiacritics(defNameClean); // EX GruyereCheese
            defNameClean = defNameClean.ToLower();  // e x gruyere cheese

            // formulate inflections by comparing label and defName; if you're unable to, use the label
            string root = LongestCommonSubstring(defNameClean, labelClean);  // e.g. EX_GruyereCheese + GruyèreCheese => gruyere cheese


            // try to get plural form from label, b/c it's usually plural
            // you can't just rely on checking -s endings b/c some names like "meat" will never end in -s
            // you can't rely on label on its own b/c it might have unnecessary words (e.g. "mammoth gold" pumpkins)
            if (plur is null)
            {
                // if root is some generic term like "meat" or doesn't start at the start of a word, used reduced label instead
                if (!root.NullOrEmpty() && !delete.Contains(root) && Regex.IsMatch(labelClean, $"\\b{root}"))
                {
                    // if plur has a placeholder, replace it with root
                    // otherwise plur is root extended to the end of the label // mammoth gold pumpkins & pumpkin => pumpkins
                    if (!inflections.Empty() && inflections[0].Contains("{0}")) plur = inflections[0].Formatted(root);
                    else
                    {
                        plur = Regex.Match(labelClean, "(?i)" + root + "[^ ]*").Value;
                        int head = labelClean.IndexOf(root, StringComparison.Ordinal);
                        if (head != -1) plur = labelOriginal.Substring(head, plur.Length);  // get diacritics and capitalization back

                        // if the 2 forms differ in length by more than 2 letters, discard them and use reduced label
                        if (head == -1 || root.Length == 0 || root.Length < plur.Length - 2)
                        {
                            plur = labelBitsDeleted;
                        }
                    }

                }
                // otherwise use reduced label
                else
                {
                    plur = labelBitsDeleted;
                }
            }

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

            // try to get adjectival form (based on singular)
            if (adj is null)
            {
                if (!inflections.Empty() && inflections[3].Contains("{0}")) adj = inflections[3].Formatted(root);
                else adj = sing;
            }

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