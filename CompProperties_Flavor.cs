using Verse;
using System.Collections.Generic;

namespace FlavorText;

// static data used by CompFlavor
public class CompProperties_Flavor : CompProperties
{

    public const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite and is not recommended (too many combinations)

    internal readonly List<FlavorCategoryDef> defaultGhostExcludedCategories = [FlavorCategoryDefOf.FT_Meat_Twisted, FlavorCategoryDefOf.FT_Meat_Human, FlavorCategoryDefOf.FT_Meat_Insect, FlavorCategoryDefOf.FT_Fungus, FlavorCategoryDefOf.FT_AnimalFoods];

    public CompProperties_Flavor()
    {
        compClass = typeof(CompFlavor);
    }
}


