using System.Collections.Generic;
using System.Linq;
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

    // all FlavorDefs of a given meal category (basic, paste, baby)
    public static IEnumerable<FlavorDef> AllFlavorDefsList(ThingDef mealThingDef)
    {
        foreach (var flavorDef in DefDatabase<FlavorDef>.AllDefs)
        {
            foreach (var mealCategory in flavorDef.mealCategories)
            {
                if (mealCategory.DescendantThingDefs.Contains(mealThingDef))
                {
                    yield return flavorDef;
                    break;
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


