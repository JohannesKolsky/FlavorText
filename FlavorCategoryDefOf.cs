using RimWorld;
using Verse;

namespace FlavorText;

[DefOf]
public static class FlavorCategoryDefOf
{
    public static FlavorCategoryDef FT_Root;  // topmost category for FlavorText
    public static FlavorCategoryDef FT_Foods; // topmost category used for meal ingredients; contains everything in vanilla Foods ThingCategoryDef
    public static FlavorCategoryDef FT_CookingStations;  // all buildings marked as a meal source
    public static FlavorCategoryDef FT_MealsWithCompFlavor;  // all items that should get their label changed via Flavor Text
    public static FlavorCategoryDef FT_MealsKinds;  // all meals of all kinds, ignoring quality
    public static FlavorCategoryDef FT_MealsQualities; // all qualities of meals
    public static FlavorCategoryDef FT_MealsNonSpecial; // non-specialty meals; this is the default if meal kind is unknown
    public static FlavorCategoryDef FT_MealsCooked; // all meals that are "cooked"; for vanilla this is all except paste and baby food
}


