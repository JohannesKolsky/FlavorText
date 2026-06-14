using UnityEngine;
using RimWorld;
using Verse;

//DONE: reload flavor text on settings change  // happens on reload of save
//DONE: always randomized recipe output?

//TODO: reinitialize Defs when settings change so you don't require a restart (laxRecipeMatching, dynamicMealIncorporation)

namespace FlavorText
{
    public class FlavorTextSettings : ModSettings
    {

        public static bool fillUpBlankMeals = false; // should blank meals get ingredients to fill in their recipes?

        public static int ghostIngredientCap = 0; // how many extra ingredients can be added?

        public static bool quickSearch = false; // true: randomizes the recipe when multiple recipes match // false: always chooses the most specific recipe'

        public static bool laxRecipeMatching = true;  // true: if modded soups are present, soup-type labels will still appear for normal meals; requires restart

        public static bool dynamicMealIncorporation = true; // true: add flavor text to meals outside of the explicitly defined ones; requires restart

        public static bool flavorTextForStacks = true; // true: add flavor text to stacks of meals

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new();
            listing_Standard.Begin(inRect);
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("fillUpBlankMeals".Translate(), ref fillUpBlankMeals, "fillUpBlankMealsTooltip".Translate());
            listing_Standard.Gap();
            ghostIngredientCap = (int)listing_Standard.SliderLabeled("numAllowedMissingIngredients".Translate(ghostIngredientCap), ghostIngredientCap, 0, 6, labelPct: 0.7f, tooltip: "numAllowedMissingIngredientsTooltip".Translate(ghostIngredientCap));
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("quickSearch".Translate(), ref quickSearch, "quickSearchTooltip".Translate());
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("flavorTextForStacks".Translate(), ref flavorTextForStacks, "flavorTextForStacksTooltip".Translate());
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("laxRecipeMatching".Translate(), ref laxRecipeMatching, "laxRecipeMatchingTooltip".Translate());
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("dynamicMealIncorporation".Translate(), ref dynamicMealIncorporation, "dynamicMealIncorporationTooltip".Translate());
            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fillUpBlankMeals, "fillUpBlankMeals", defaultValue: false, forceSave: true);
            Scribe_Values.Look(ref ghostIngredientCap, "numAllowedMissingIngredients", defaultValue: 0, forceSave: true);
            Scribe_Values.Look(ref quickSearch, "quickSearch", defaultValue: false, forceSave: true);
            Scribe_Values.Look(ref laxRecipeMatching, "laxRecipeMatching", defaultValue: true, forceSave: true);
            Scribe_Values.Look(ref dynamicMealIncorporation, "dynamicMealIncorporation", defaultValue: true, forceSave: true);
            Scribe_Values.Look(ref flavorTextForStacks, "flavorTextForStacks", defaultValue: true, forceSave: true);
        }
    }
}
