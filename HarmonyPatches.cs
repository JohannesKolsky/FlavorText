using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

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
/*        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "PostExposeData", null, null), null, new HarmonyMethod(patchType, "CompIngredientsPostExposeDataPostFix", null), null, null);*/
/*        harmony.Patch(AccessTools.Method(typeof(DefGenerator), "GenerateImpliedDefs_PreResolve", null, null), null, new HarmonyMethod(patchType, "GenerateImpliedDefs_PreResolvePostFix", null), null, null);*/
    }

    // after making a product with CompIngredients, broadcast a signal saying it's done so CompFlavor can do its thing
    public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result, RecipeDef recipeDef, List<Thing> ingredients, Thing dominantIngredient)
    {
        foreach (Thing product in __result)
        {
            if (product.HasComp<CompFlavor>() && product.HasComp<CompIngredients>())
            {
/*                product.TryGetComp<CompFlavor>().Ingredients = (from Thing thing in ingredients select thing.def).ToList();*/
                product.TryGetComp<CompFlavor>().parent.BroadcastCompSignal("IngredientsRegistered");
            }
            yield return product;
        }
    }

/*    // after loading CompIngredients from save, broadcast a signal saying it's done loading so CompFlavor can do its thing
    public static void CompIngredientsPostExposeDataPostFix(CompIngredients __instance)
    {
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            Log.Error("CompIngredientsPostExposeDataPostFix");
            __instance.parent.BroadcastCompSignal("IngredientsRegistered");
        }
    }*/

/*    public static void GenerateImpliedDefs_PreResolvePostFix()
    {
        foreach (ThingDef item in DefDatabase<ThingDef>.AllDefs)
        {
            if (item.IsWithinCategory(FT_MealsFlavor))
        }
    }*/

}
