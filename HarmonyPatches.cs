using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using Verse;

namespace FlavorText;

[StaticConstructorOnStartup]
public static class HarmonyPatches
{
    private static readonly Type patchType;

    static HarmonyPatches()
    {
        patchType = typeof(HarmonyPatches);
        Harmony harmony = new Harmony("rimworld.hekmo.FlavorText");
        harmony.Patch((MethodBase)AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null);
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
            product.TryGetComp<CompFlavor>().ingredients = ingredients;
            Log.Message("product found");
            Log.Message("product ID is " + product.ThingID);
            yield return product;
        }
    }
}
