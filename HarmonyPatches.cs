using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Verse;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?

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
[SuppressMessage("ReSharper", "InconsistentNaming")]
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
        harmony.Patch(AccessTools.Method(typeof(GenRecipe), "MakeRecipeProducts"), null, new HarmonyMethod(patchType, "MakeRecipeProductsPostFix"));

        /*harmony.Patch(AccessTools.Method(typeof(Toils_Ingest), "TakeMealFromDispenser"), null, new HarmonyMethod(patchType, "TakeMealFromDispenserPostFix"));
        harmony.Patch(AccessTools.Method(typeof(JobDriver_Ingest), "GetReport"), null, new HarmonyMethod(patchType, "GetReportPostFix"));*/
    }

    // dirty ingredient cache when a new ingredient is added, forcing a recheck once TryGetFlavorText is next called
    // ReSharper disable once UnusedMember.Global
    public static void RegisterIngredientPostFix(ref CompIngredients __instance)
    {
        if (!__instance.parent.HasComp<CompFlavor>()) return;
        //Log.Warning("RegisterIngredientPostFix");
        CompFlavor compFlavor = __instance.parent.TryGetComp<CompFlavor>();
        if (compFlavor != null) compFlavor.TriedFlavorText = false;
    }


    // after making a product with CompIngredients, add information about how it was cooked
    // ReSharper disable once UnusedMember.Global
    // ReSharper disable once InconsistentNaming
    public static IEnumerable<Thing> MakeRecipeProductsPostFix(IEnumerable<Thing> __result, IBillGiver billGiver, Pawn worker, List<Thing> ingredients)
    {
        foreach (Thing product in __result)
        {
            if (product.HasComp<CompFlavor>())
            {
                //Log.Warning("MakeRecipeProducts");
                CompFlavor compFlavor = product.TryGetComp<CompFlavor>();
                if (compFlavor != null)
                {
                    compFlavor.CookingStation = ((Thing)billGiver).def;
                    compFlavor.HourOfDay = GenLocalDate.HourOfDay(billGiver.Map);
                    // average percentage of hit points of each ingredient group (ignoring quantity in group)
                    compFlavor.IngredientsHitPointPercentage = ingredients
                        .FindAll(i => i?.def != null && ThingCategoryDefUtility.FlavorRoot.ContainedInThisOrDescendant(i.def))
                        .Where(i => i.def.useHitPoints)
                        .Sum(j => (float)j.HitPoints / j.MaxHitPoints) / ingredients.Count;
                    if (ModsConfig.BiotechActive && worker.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin")))
                    {
                        Rand.PushState(product.thingIDNumber);
                        if (Rand.Range(0, 20) == 0)
                        {
                            compFlavor.Tags.Add("hairy");
                        }
                        Rand.PopState();
                    }
                }
            }
            yield return product;
        }
    }

    // possibly unnecessary, and also conflicts with CommonSense random ingredients which for nutrient paste are briefly added before being replaced by the actual ingredients
    // after making a Thing, try and get flavor text if it should have flavor text
    // things like VCE canned meat aren't included, because they do not track which meat is in them once put into a meal (e.g. canned human meat in a meal isn't abhorrent)
    /*public static void MakeThingPostFix(ref Thing __result)
    {
        if (!__result.HasComp<CompFlavor>()) return;
        Log.Warning("MakeThing");
        CompFlavor compFlavor = __result.TryGetComp<CompFlavor>();
        compFlavor?.TryGetFlavorText();
    }*/


    // when dispensing nutrient paste, try and get flavor text
    /*public static void TryDispenseFoodPostFix(ref Thing __result)
    {
        if (!__result.HasComp<CompFlavor>()) return;
        Log.Warning("TryDispenseFood");
        CompFlavor compFlavor = __result.TryGetComp<CompFlavor>();
        compFlavor?.TryGetFlavorText();
    }*/


    /*// edits job override string after getting nutrient paste meal from dispenser
    public static void TakeMealFromDispenserPostFix(ref Toil __result)
    {
        /*Toil toil = __result;
        Job curJob = toil.actor.jobs.curJob;
        Log.Warning($"curJob is {curJob.def.defName}");
        Log.Warning($"curToil is {toil.debugName}");
        toil.AddFinishAction(delegate
        {
            Thing thing = toil.actor.carryTracker.CarriedThing;
            if (thing?.def.ingestible != null)
            {
                if (!thing.def.ingestible.ingestReportStringEat.NullOrEmpty() && (thing.def.ingestible.ingestReportString.NullOrEmpty() || (int)toil.actor.RaceProps.intelligence < 1))
                {
                    curJob.reportStringOverride = thing.def.ingestible.ingestReportStringEat.Formatted(curJob.targetA.Thing.LabelShort, curJob.targetA.Thing);
                }
                if (!thing.def.ingestible.ingestReportString.NullOrEmpty())
                {
                    curJob.reportStringOverride = thing.def.ingestible.ingestReportString.Formatted(curJob.targetA.Thing.LabelShort, curJob.targetA.Thing);
                }
                Log.Warning($"TakeMealFromDispenser, report string override was {curJob.reportStringOverride}");
            }
        });
        __result = toil;#1#
        Log.Warning($"added finish action");
    }

    // changes job display string after getting nutrient paste meal from dispenser
    public static void GetReportPostFix(ref string __result, ref JobDriver_Ingest __instance)
    {
        Log.Warning($"GetReport, report string override was {__instance.job.reportStringOverride}");
        /*if (!__instance.job.reportStringOverride.NullOrEmpty())
        {
            return __instance.job.reportStringOverride;
        }#1#
        Thing thing = __instance.job.targetA.Thing;
        if (thing?.def.ingestible != null)
        {
            Log.Warning($"ingestible");
            if (!thing.def.ingestible.ingestReportStringEat.NullOrEmpty() && (thing.def.ingestible.ingestReportString.NullOrEmpty() || (int)__instance.pawn.RaceProps.intelligence < 1))
            {
                Log.Warning($"unintelligent");
                __result = thing.def.ingestible.ingestReportStringEat.Formatted(__instance.job.targetA.Thing.LabelShort, __instance.job.targetA.Thing);
            }
            if (!thing.def.ingestible.ingestReportString.NullOrEmpty())
            {
                Log.Warning($"ingestible nonEmpty");
                __result = thing.def.ingestible.ingestReportString.Formatted(__instance.job.targetA.Thing.LabelShort, __instance.job.targetA.Thing);
            }
        }

        try
        {
            __result = __instance.GetReport();
        }
        catch (Exception ex)
        {
            Log.Error(ex.Message);
            __result =
        }
    }*/

}
