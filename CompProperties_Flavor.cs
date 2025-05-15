using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FlavorText;

// various static data and static functions used in FlavorText
public class CompProperties_Flavor : CompProperties
{

    public const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite

    public CompProperties_Flavor()
    {
        compClass = typeof(CompFlavor);
    }

    internal static int WorldSeed => GenText.StableStringHash(Find.World.info.seedString);
}


