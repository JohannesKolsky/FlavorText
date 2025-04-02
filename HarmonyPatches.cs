using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;
using RimWorld;

namespace FlavorText;

/// <summary>
/// why is FlavorText triggered via this specific harmony patching? here's why:
/// CompIngredients is empty when initialized and all the way through CompIngredients.PostPostMake
/// CompIngredients gains its ingredients when CompIngredients.RegisterIngredients is called by GenRecipe.MakeRecipeProducts
/// if the bill is set to carry to stockpile, the meal is put directly into the pawn's hands, which is distinct from being spawned
/// so logical place is a MakeRecipeProducts PostFix
/// 
/// if using random ingredients added in by the CommonSense mod, that mod adds them in after MakeThing if there aren't any ingredients
/// so for this and other situations, MakeThing PostFix is appropriate
/// </summary>

[StaticConstructorOnStartup]
public static class HarmonyPatches
{
    private static readonly Type patchType;

    static HarmonyPatches()
    {
        patchType = typeof(HarmonyPatches);
        Harmony harmony = new("rimworld.hekmo.FlavorText");
        harmony.Patch(AccessTools.Method(typeof(ThingMaker), "MakeThing", null, null), null, new HarmonyMethod(patchType, "MakeThingPostFix", null), null, null);
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts", null, null), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix", null), null, null);
/*        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "PostExposeData", null, null), null, new HarmonyMethod(patchType, "CompIngredientsPostExposeDataPostFix", null), null, null);*/
/*        harmony.Patch(AccessTools.Method(typeof(DefGenerator), "GenerateImpliedDefs_PreResolve", null, null), null, new HarmonyMethod(patchType, "GenerateImpliedDefs_PreResolvePostFix", null), null, null);*/
    }

    // after making a Thing, try and get flavor text if it should have flavor text
    public static void MakeThingPostFix(Thing __result)
    {
        if (__result.HasComp<CompFlavor>() && __result.HasComp<CompIngredients>())
        {
            __result.TryGetComp<CompFlavor>().GetFlavorText(CompProperties_Flavor.AllFlavorDefsList);
        }
    }

    // after making a product with CompIngredients, try and get flavor text if it should have flavor text
    public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result)
    {
        foreach (Thing product in __result)
        {
            if (product.HasComp<CompFlavor>() && product.HasComp<CompIngredients>())
            {
                CompFlavor compFlavor = product.TryGetComp<CompFlavor>();
                if (compFlavor != null)
                {
                    compFlavor.fail = false;
                }
                compFlavor.GetFlavorText(CompProperties_Flavor.AllFlavorDefsList);
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
