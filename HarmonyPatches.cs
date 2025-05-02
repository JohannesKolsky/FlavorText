using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using Verse;

//TODO: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?
//TODO: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)

namespace FlavorText;

/// <summary>
/// why is FlavorText triggered via this specific harmony patching? here's why:
/// CompIngredients is empty when initialized all the way through its creation process
/// CompIngredients only gains its ingredients when CompIngredients.RegisterIngredients is called by GenRecipe.MakeRecipeProducts
/// if the bill is set to carry to stockpile, the meal is put directly into the pawn's hands, which is distinct from being spawned
/// so logical place is a MakeRecipeProducts PostFix
/// 
/// if using random ingredients added in by the CommonSense mod, that mod adds them in after MakeThing if there aren't any ingredients
/// so for this and other situations, MakeThing PostFix is appropriate
/// 
/// TryDispenseFood covers nutrient paste
/// 
/// MakeThingPostFix covers meals in trade caravans and friendly settlements, and all other instances
/// 
/// additionally CompFlavor runs PostPostGeneratedForTrader for random trader, visitor, and settlement inventories
/// additionaly CompFlavor runs PostSpawnSetup for when item is spawned on the map
/// </summary>

[StaticConstructorOnStartup]
public static class HarmonyPatches
{
    static HarmonyPatches()
    {
        var patchType = typeof(HarmonyPatches);
        Harmony harmony = new("rimworld.hekmo.FlavorText");
        harmony.Patch(AccessTools.Method(typeof(CompIngredients), "RegisterIngredient"), null, new HarmonyMethod(patchType, "RegisterIngredientPostFix"));
        /*harmony.Patch(AccessTools.Method(typeof(ThingMaker), "MakeThing"), null, new HarmonyMethod(patchType, "MakeThingPostFix"));
        harmony.Patch(AccessTools.Method(typeof(Building_NutrientPasteDispenser), "TryDispenseFood"), null, new HarmonyMethod(patchType, "TryDispenseFoodPostFix"));*/
        /*        harmony.Patch(AccessTools.Method(typeof(Pawn_CarryTracker), "TryStartCarry", [typeof(Thing)], null), null, new HarmonyMethod(patchType, "TryStartCarryPostFix", null), null, null);*/
        /*harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts"), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix"));*/
    }

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    public static void RegisterIngredientPostFix(ref CompIngredients __instance)
    {
        if (!__instance.parent.HasComp<CompFlavor>()) return;
        //Log.Warning("RegisterIngredientPostFix");
        CompFlavor compFlavor = __instance.parent.TryGetComp<CompFlavor>();
        if (compFlavor != null) compFlavor.TriedFlavorText = false;
    }

    // possibly unnecessary, and also conflicts with CommonSense random ingredients which for nutrient paste are briefly added before being replaced by the actual ingredients
    // after making a Thing, try and get flavor text if it should have flavor text
    // things like VCE canned meat aren't included, because they do not track which meat is in them once put into a meal (e.g. canned human meat in a meal isn't abhorrent)
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once InconsistentNaming
    /*public static void MakeThingPostFix(ref Thing __result)
    {
        if (!__result.HasComp<CompFlavor>()) return;
        Log.Warning("MakeThing");
        CompFlavor compFlavor = __result.TryGetComp<CompFlavor>();
        compFlavor?.TryGetFlavorText();
    }*/


    // when dispensing nutrient paste, try and get flavor text
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once InconsistentNaming
    /*public static void TryDispenseFoodPostFix(ref Thing __result)
    {
        if (!__result.HasComp<CompFlavor>()) return;
        Log.Warning("TryDispenseFood");
        CompFlavor compFlavor = __result.TryGetComp<CompFlavor>();
        compFlavor?.TryGetFlavorText();
    }*/

    // after making a product with CompIngredients, try and get flavor text if it should have flavor text
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once InconsistentNaming
    /*public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result, IBillGiver billGiver, Pawn worker)
    {
        foreach (Thing product in __result)
        {
            if (product.HasComp<CompFlavor>())
            {
                Log.Warning("MakeRecipeProducts");
                CompFlavor compFlavor = product.TryGetComp<CompFlavor>();
                if (compFlavor != null)
                {
                    compFlavor.CookingStation = ((Thing)billGiver).def;
                    compFlavor.HourOfDay = GenLocalDate.HourOfDay(billGiver.Map);
                    if (worker.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin")))
                    {
                        Rand.PushState(product.thingIDNumber);
                        if (Rand.Range(0, 20) == 0)
                        {
                            compFlavor.Tags.Add("hairy");
                        }
                        Rand.PopState();
                    }
                    compFlavor.TryGetFlavorText();
                }
            }
            yield return product;
        }
    }*/

}
