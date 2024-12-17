using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;

namespace FlavorText;

[StaticConstructorOnStartup]
public static class HarmonyPatches
{
    private static readonly Type patchType;

    static HarmonyPatches()
    {
        patchType = typeof(HarmonyPatches);
        Harmony harmony = new("rimworld.hekmo.FlavorText");
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts", null, null), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix", null),null, null);
    }

    // retrieve list of ingredients from meal as it's being made
    public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result, RecipeDef recipeDef, List<Thing> ingredients, Thing dominantIngredient)
    {
        if (!recipeDef.ProducedThingDef.IsWithinCategory(DefDatabase<ThingCategoryDef>.GetNamed("FoodMeals")))  // if the recipe makes a meal (first thing made)
        {
            yield break;
        }
        Log.Message("Adding ingredients from MakeRecipeProducts to CompFlavor");
        foreach (Thing product in __result)
        {
            product.TryGetComp<CompFlavor>().Ingredients = ingredients;
            Log.Message("product found");
            Log.Message("product ID is " + product.ThingID);
            product.TryGetComp<CompFlavor>().parent.BroadcastCompSignal("IngredientsRegistered");
            yield return product;
        }
    }
}
