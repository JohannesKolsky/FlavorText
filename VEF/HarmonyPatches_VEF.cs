using FlavorText;
using HarmonyLib;
using PipeSystem;
using System.Collections.Generic;
using System.Linq;
using Verse;
using System.Diagnostics;
using System;
using System.Reflection;
using RimWorld;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?
//DONE: spoiled soup causes error on next insert
//DONE: pawn pathfinding error on destroying ElectricPot while processing b/c ElectricPot is no longer on a map (SYR is fine)


namespace VEF
{
    /// <summary>
    /// patches for processor buildings from VEF
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_VEF
    {
        static HarmonyPatches_VEF()
        {
            var patchType = typeof(HarmonyPatches_VEF);
            Harmony harmony = new("rimworld.hekmo.VEF");
            {
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.Process"), "SpawnOrPushToNet"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_SpawnOrPushToNetPostfix"));
 /*               harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.AdvancedProcessorsManager"), "AddIngredient"), prefix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_AddIngredientPrefix"));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.Process"), "HandleIngredientsAndQuality"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_HandleIngredientsAndQualityPostfix"));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("PipeSystem.Process"), "ResetProcess"), prefix: new HarmonyMethod(patchType, "HarmonyPatch_VEF_ResetProcessPrefix"));*/
            }
        }

        //PipeSystem.Process.ResetProcess

/*        // VEF: cache CompFlavor when meal is added to processor
        public static void HarmonyPatch_VEF_AddIngredientPrefix(ref ThingComp comp, ref Thing thing)
        {
            if (thing == null) throw new NullReferenceException($"item being inserted into processor was null in HarmonyPatch_VEF_AddIngredientPrefix. Please report.");
            if (thing.TryGetComp(out CompFlavor compFlavor))
            {
                compFlavor.TryGetFlavorText();
                if (VerifyCompFlavorIntegrity(compFlavor))
                {
                    Log.Message($"thing was {thing.ToStringSafe()} on map {thing?.Map.ToStringSafe()}");
                    var activeProcesses = thing.MapHeld.GetComponent<CompFlavorUtility>().ActiveProcesses;
                    activeProcesses.Add(comp.parent.ThingID, new CompFlavorData(compFlavor));
                    Log.Warning($"activeProcesses were [{activeProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
                }
                else Log.Error($"CompFlavor for input meal into {comp.parent} had a null field, ignoring it. Output meal CompFlavor will be regenerated. Please report.");
            }

            static bool VerifyCompFlavorIntegrity(CompFlavor compFlavor)
            {
                if (compFlavor?.Iteration == null) return false;
                // no need for cookID since that might be null for spawned meals
                if (compFlavor?.MealTags == null) return false;
                return true;
            }
        }*/

/*        // VEF: retrieve CompFlavor from cache when meal is removed from processor
        public static void HarmonyPatch_VEF_HandleIngredientsAndQualityPostfix(ref Thing outThing, ref PipeSystem.Process __instance)
        {
            if (outThing.TryGetComp(out CompFlavor outCompFlavor))
            {
                string processorID = __instance.advancedProcessor.parent.ThingID;
                var activeProcesses = __instance.advancedProcessor.parent.MapHeld.GetComponent<CompFlavorUtility>().ActiveProcesses;
                activeProcesses.TryGetValue(processorID, out CompFlavorData cachedCompFlavorData);

                Log.Message($"Changing old CompFlavor {outCompFlavor.ToStringSafe()} with iteration {outCompFlavor.Iteration.ToStringSafe()} to cached CompFlavor {cachedCompFlavorData.ToStringSafe()} with processor ID {processorID.ToStringSafe()} and iteration {cachedCompFlavorData.iteration.ToStringSafe()}");
                outCompFlavor.Iteration = cachedCompFlavorData.iteration;
                outCompFlavor.CookID = cachedCompFlavorData.cookID;
                outCompFlavor.MealTags = cachedCompFlavorData.mealTags;
                activeProcesses.Remove(processorID);


            }
        }*/

        //VEF: add CompFlavor data
        public static void HarmonyPatch_VEF_SpawnOrPushToNetPostfix(ref Pawn extractor, ref List<Thing> outThings, ref PipeSystem.Process __instance)
        {
            Log.Message($"outThings was [{outThings.ToStringSafeEnumerable()}]");
            foreach (var outThing in outThings)
            {
                if (outThing.TryGetComp(out CompFlavor outCompFlavor))
                {
                    outCompFlavor.CookingStation = __instance.advancedProcessor.parent.def;
                    outCompFlavor.HourOfDay = GenLocalDate.HourOfDay(__instance.advancedProcessor.parent.Map);
                    outCompFlavor.TickCreated = GenTicks.TicksAbs;
                    outCompFlavor.CookID = extractor.ThingID;
                       if (ModsConfig.BiotechActive && extractor?.genes is not null && extractor.genes.HasActiveGene(DefDatabase<GeneDef>.GetNamed("Furskin"))) // don't ask
                    {

                        Rand.PushState(Find.World.info.Seed + CompFlavorUtility.Iterations);
                        if (Rand.Range(0, 20) == 0)
                        {
                            outCompFlavor.MealTags.Add("hairy");
                        }
                        Rand.PopState();
                    }
                }
            }
        }

/*        // VEF: remove item from CompFlavorUtility if the process is reset for any reason (despawn, spoil)
        public static void HarmonyPatch_VEF_ResetProcessPrefix(ref PipeSystem.Process __instance)
        {
            //TODO: can this be done without reflection? I wrote this b/c at this stage the processor is despawned and thus has no map
            var managerForMap = (AdvancedProcessorsManager)__instance.GetType().GetField("advancedProcessorsManager", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(__instance);
            var activeProcesses = managerForMap.map.GetComponent<CompFlavorUtility>().ActiveProcesses;
            activeProcesses.Remove(__instance?.advancedProcessor?.parent?.ThingID);
            Log.Warning($"after resetting process, activeProcesses were [{activeProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
        }*/
    }
}
