using System.Collections.Generic;
using Verse;

namespace FlavorText;

// various static data and static functions used in FlavorText
public partial class CompProperties_Flavor : CompProperties
{
    public CompProperties_Flavor()
    {
        compClass = typeof(CompFlavor);
    }

    public RulePackDef sideDishClauses;

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


