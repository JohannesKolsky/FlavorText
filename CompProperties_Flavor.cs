using Verse;

namespace FlavorText;

// static data used by CompFlavor
public class CompProperties_Flavor : CompProperties
{

    public const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite and is not recommended (too many combinations)

    public CompProperties_Flavor()
    {
        compClass = typeof(CompFlavor);
    }

    internal static int WorldSeed => GenText.StableStringHash(Find.World.info.seedString);
}


