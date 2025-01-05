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
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts", null, null), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix", null), null, null);
    }

    // retrieve list of ingredients from meal as it's being made
    public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result, RecipeDef recipeDef, List<Thing> ingredients, Thing dominantIngredient)
    {
        // add the used ingredients to EACH product made by the recipe (will usually be 1 product)
        foreach (Thing product in __result)
        {
            if (product.HasComp<CompFlavor>())
            {
                product.TryGetComp<CompFlavor>().Ingredients = ingredients;
                product.TryGetComp<CompFlavor>().parent.BroadcastCompSignal("IngredientsRegistered");
                yield return product;
            }
            else
            {
                yield return product;
            }
        }
    }
}
