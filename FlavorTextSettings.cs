using UnityEngine;
using Verse;

namespace FlavorText
{
    public class FlavorTextSettings : ModSettings
    {
        public static bool strictRecipeMatching = false;  // if modded soups are present, soup-type labels won't appear for normal meals

        public static void DoWindowContents(Rect inRect)
        {
            Listing_Standard listing_Standard = new();
            listing_Standard.Begin(inRect);
            listing_Standard.Gap();
            listing_Standard.CheckboxLabeled("strictRecipeMatching".Translate(), ref strictRecipeMatching, "strictRecipeMatchingTooltip".Translate());
            listing_Standard.End();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref strictRecipeMatching, "strictRecipeMatching", defaultValue: false, forceSave: true);
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
    }
}
