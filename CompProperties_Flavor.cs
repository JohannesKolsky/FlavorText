using Verse;
using System.Collections.Generic;

namespace FlavorText;

// static data used by CompFlavor
internal class CompProperties_Flavor : CompProperties
{

    internal const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite and is not recommended (too many combinations)

    internal readonly List<FlavorCategoryDef> defaultGhostExcludedCategories = [FlavorCategoryDefOf.FT_Meat_Twisted, FlavorCategoryDefOf.FT_Meat_Human, FlavorCategoryDefOf.FT_Meat_Insect, FlavorCategoryDefOf.FT_Fungus, FlavorCategoryDefOf.FT_FoodMeals, FlavorCategoryDefOf.FT_AnimalFoods];  // by excluding Meals you avoid common meat sources // condiments and drinks could contain meat and dairy, but it's not as big a deal and is worth it for fun combinations

    internal CompProperties_Flavor()
    {
        compClass = typeof(CompFlavor);
    }
}


