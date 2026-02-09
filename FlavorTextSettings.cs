using UnityEngine;
using Verse;

//DONE: reload flavor text on settings change  // happens on reload of save

namespace FlavorText
{
    public class FlavorTextSettings : ModSettings
    {

        public static int numAllowedMissingIngredients = 0; // how many ingredients is it ok to be missing to match a flavor def?

        public static bool randomizedRecipeOuput = true; // true: randomizes the recipe when multiple recipes match // false: always chooses the most specific recipe'

        public static bool laxRecipeMatching = true;  // true: if modded soups are present, soup-type labels will still appear for normal meals

        public static bool dynamicMealIncorporation = true; // true: add flavor text to meals outside of the explicitly defined ones; requires restart

        public static bool flavorTextForStacks = true; // true: add flavor text to stacks of meals

        public static void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new();
            listing_Standard.Begin(inRect);
            listing_Standard.Gap();
            numAllowedMissingIngredients = (int)listing_Standard.SliderLabeled("numAllowedMissingIngredients".Translate(numAllowedMissingIngredients), numAllowedMissingIngredients, 0, CompProperties_Flavor.MaxNumIngredientsFlavor, labelPct: 0.7f, tooltip: "numAllowedMissingIngredientsTooltip".Translate());
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("randomizedRecipeOutput".Translate(), ref randomizedRecipeOuput, "randomizedRecipeOutputTooltip".Translate());
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("laxRecipeMatching".Translate(), ref laxRecipeMatching, "laxRecipeMatchingTooltip".Translate());
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("dynamicMealIncorporation".Translate(), ref dynamicMealIncorporation, "dynamicMealIncorporationTooltip".Translate());
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("flavorTextForStacks".Translate(), ref flavorTextForStacks, "flavorTextForStacksTooltip".Translate());
            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref numAllowedMissingIngredients, "numAllowedMissingIngredients", defaultValue: 0, forceSave: true);
            Scribe_Values.Look(ref randomizedRecipeOuput, "randomizedRecipeOutput", defaultValue: true, forceSave: true);
            Scribe_Values.Look(ref laxRecipeMatching, "laxRecipeMatching", defaultValue: true, forceSave: true);
            Scribe_Values.Look(ref dynamicMealIncorporation, "dynamicMealIncorporation", defaultValue: true, forceSave: true);
            Scribe_Values.Look(ref flavorTextForStacks, "flavorTextForStacks", defaultValue: true, forceSave: true);
        }
    }

    internal class FlavorTextMod : Mod
    {
        public FlavorTextMod(ModContentPack content) : base(content)
        {
            GetSettings<FlavorTextSettings>();
        }

        public override string SettingsCategory()
        {
            return "Flavor Text";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            FlavorTextSettings.DoWindowContents(inRect);
        }
        /*        public override void WriteSettings()
                {
                    base.WriteSettings();
                    if (FlavorTextSettings.) Scribe_Values.Read
                }*/
    }
}
