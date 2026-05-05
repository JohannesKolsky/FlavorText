using UnityEngine;
using Verse;

//DONE: reload flavor text on settings change  // happens on reload of save
//--TODO: always randomized recipe output?

//TODO: reinitialize Defs when settings change so you don't require a restart (laxRecipeMatching, dynamicMealIncorporation)

namespace FlavorText
{
    public class FlavorTextMod : Mod
    {
        private static FlavorTextSettings flavorTextSettings;
        public FlavorTextMod(ModContentPack content) : base(content)
        {
            flavorTextSettings = GetSettings<FlavorTextSettings>();
        }

        public override string SettingsCategory()
        {
            return "Flavor Text";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            flavorTextSettings.DoWindowContents(inRect);
        }
        public override void WriteSettings()
        {
            base.WriteSettings();
            //CategoryUtility.Reinitialize();
        }
    }
}
