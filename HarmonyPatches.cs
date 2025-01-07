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
/*        harmony.Patch(AccessTools.Method(typeof(ThingComp), "Initialize", null, null), null, new HarmonyMethod(patchType, "InitializePostFix", null), null, null);*/
        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "PostExposeData", null, null), null, new HarmonyMethod(patchType, "CompIngredientsPostExposeDataPostFix", null), null, null);
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
                yield return product;
            }
            else
            {
                yield return product;
            }
        }
    }

    // after loading CompIngredients from save, broadcast a signal saying it's done loading so CompFlavor can do its thing
    public static void CompIngredientsPostExposeDataPostFix(CompIngredients __instance)
    {
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            __instance.parent.BroadcastCompSignal("IngredientsRegistered");
        }
    }

/*    // after CompIngredients initializes, broadcast a signal saying it's done
    public static void InitializePostFix(CompProperties props, ThingComp __instance)
    {
        Log.Message($"Initialized a ThingComp");
        if (props.compClass == typeof(CompIngredients))
        {
            Log.Error($"Initialized CompIngredients");
            __instance.parent.BroadcastCompSignal("IngredientsRegistered");
        }
    }*/
}
