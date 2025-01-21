using System.Collections.Generic;
using Verse;

namespace FlavorText;

// various static data and static functions used in FlavorText
public class CompProperties_Flavor : CompProperties
{

    public const int maxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite

    public CompProperties_Flavor()
    {
        compClass = typeof(CompFlavor);
    }

    // all FlavorDefs
    public static List<FlavorDef> FlavorDefs
    {
        get
        {
            return (List<FlavorDef>)DefDatabase<FlavorDef>.AllDefs;
        }
    }

    public static int WorldSeed
    {
        get
        {
            return GenText.StableStringHash(Find.World.info.seedString);
        }
    }
}


