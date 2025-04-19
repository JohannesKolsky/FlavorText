using HarmonyLib;
using System;
using System.Collections.Generic;
using Verse;
using RimWorld;
using System.Linq;

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
/// 
/// when getting nutrient paste, ingredients are added after MakeThing runs
/// so Pawn_CarryTracker TryStartCarry covers that and probably other modded food dispensers
/// 
/// additionally CompFlavor runs PostSpawnSetup for when the item is spawned on a map
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
        harmony.Patch(AccessTools.Method(typeof(Pawn_CarryTracker), "TryStartCarry", [typeof(Thing)], null), null, new HarmonyMethod(patchType, "TryStartCarryPostFix", null), null, null);
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts", null, null), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix", null), null, null);
    }

    // after making a Thing, try and get flavor text if it should have flavor text
    public static void MakeThingPostFix(ref Thing __result)
    {
        if (__result.HasComp<CompFlavor>() && __result.HasComp<CompIngredients>())
        {
            __result.TryGetComp<CompFlavor>().GetFlavorText(CompProperties_Flavor.AllFlavorDefsList(__result.def).ToList());
        }
    }

    // when carrying a thing, try and get Flavor Text
    public static void TryStartCarryPostFix(ref Thing item)
    {
        if (item.HasComp<CompFlavor>() && item.HasComp<CompIngredients>())
        {
            CompFlavor compFlavor = item.TryGetComp<CompFlavor>();
            if (compFlavor != null)
            {
                if (compFlavor.finalFlavorLabel == null)
                {
                    compFlavor.GetFlavorText(CompProperties_Flavor.AllFlavorDefsList(item.def).ToList());
                }
            }
        }
    }

    // after making a product with CompIngredients, try and get flavor text if it should have flavor text
    public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result, IBillGiver billGiver, Pawn worker)
    {
        foreach (Thing product in __result)
        {
            if (product.HasComp<CompFlavor>() && product.HasComp<CompIngredients>())
            {
                CompFlavor compFlavor = product.TryGetComp<CompFlavor>();
                if (compFlavor != null)
                {
                    if (compFlavor.finalFlavorLabel == null)
                    {
                        compFlavor.cookingStation = ((Thing)billGiver).def;
                        compFlavor.hourOfDay = GenLocalDate.HourOfDay(billGiver.Map);
                        if (worker.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin")))
                        {
                            Rand.PushState(product.thingIDNumber);
                            if (Rand.Range(0, 10) == 0)
                            {
                                compFlavor.tags.Add("hairy");
                            }
                            Rand.PopState();
                        } 
                    }
                }
                compFlavor.GetFlavorText(CompProperties_Flavor.AllFlavorDefsList(product.def).ToList());
            }
            yield return product;
        }
    }

}
