using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FlavorText;

// various static data and static functions used in FlavorText
public class CompPropertiesFlavor : CompProperties
{

    public const int MaxNumIngredientsFlavor = 3;  // max number of ingredients used to find flavors, default 3; changing this requires a rewrite

    public CompPropertiesFlavor()
    {
        compClass = typeof(CompFlavor);
    }

    // all FinalFlavorDefs that fit the given meal type
    public static IEnumerable<FlavorDef> ValidFlavorDefsForMealType(ThingDef mealThingDef)
    {
        foreach (var flavorDef in DefDatabase<FlavorDef>.AllDefs)
        {
            if (flavorDef != null)
            {
                foreach (var mealCategory in flavorDef.MealCategories)
                {
                    if (mealCategory.DescendantThingDefs.Contains(mealThingDef))
                    {
/*                        Log.Message($"Found valid FlavorDef {flavorDef} in category {mealCategory}");*/
                        yield return flavorDef;
                        break;
                    }
                }
            }
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


