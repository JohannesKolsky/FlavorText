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
        if (recipeDef.ProducedThingDef.IsWithinCategory(DefDatabase<ThingCategoryDef>.GetNamed("FoodMeals")))  // if the product is a meal, store its ingredients in its CompFlavor
        {
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
        else  // if the product is not a meal, return it unchanged
        {
            foreach (Thing product in __result) { Log.Message("returning " + product.def.defName); yield return product; }
        }
    }
}
