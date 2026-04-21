using FlavorText;
using HarmonyLib;
using ProcessorFramework;
using System;
using System.Linq;
using Verse;

//DONE: cover meals in inventories of spawned non-trader pawns (PawnInventoryGenerator)
//DONE: you want to find something for a ThingWithComps or ThingComp that runs once; maybe something graphics-related?


namespace SYR
{
    /// <summary>
    /// patches for processor buildings from SYR Processor Framework
    /// </summary>
    [StaticConstructorOnStartup]
    public static class HarmonyPatches_SYR
    {
        static HarmonyPatches_SYR()
        {
            var patchType = typeof(HarmonyPatches_SYR);
            Harmony harmony = new("rimworld.hekmo.SYR");
            {
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("ProcessorFramework.CompProcessor"), "TakeOutProduct"), prefix: new HarmonyMethod(patchType, "HarmonyPatch_SYR_TakeOutProductPrefix"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_SYR_TakeOutProductPostfix"));
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("ProcessorFramework.MapComponent_Processors"), "Deregister"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_SYR_DeregisterPostfix"));
            }
        }

        // cache CompFlavor before old meal is destroyed
        public static void HarmonyPatch_SYR_TakeOutProductPrefix(ref ActiveProcess activeProcess, ref ThingComp __instance)
        {
            var activeProcesses = __instance.parent.MapHeld.GetComponent<CompFlavorUtility>().ActiveProcesses;
            activeProcesses.TryGetValue(__instance.parent.ThingID, out CompFlavorData cachedCompFlavorData);
            foreach (var ingredientThing in activeProcess.ingredientThings)
            {
                if (ingredientThing.TryGetComp(out CompFlavor compFlavor))
                {
                    compFlavor.TryGetFlavorText();
                    activeProcesses.Add(__instance.parent.ThingID, new CompFlavorData(compFlavor));
                    Log.Warning($"activeProcesses were [{activeProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
                    break;
                }
            }
        }

        // retrieve cached CompFlavor and apply it to new meal
        public static void HarmonyPatch_SYR_TakeOutProductPostfix(ref ThingComp __instance, ref Thing __result)
        {

            if (__result.TryGetComp(out CompFlavor outCompFlavor))
            {
                var activeProcesses = __instance.parent.MapHeld.GetComponent<CompFlavorUtility>().ActiveProcesses;
                activeProcesses.TryGetValue(__instance.parent.ThingID, out CompFlavorData cachedCompFlavorData);

                string processorID = __instance.parent.ThingID;
                Log.Message($"Changing old CompFlavor {outCompFlavor.ToStringSafe()} with iteration {outCompFlavor.Iteration.ToStringSafe()} to cached CompFlavor {cachedCompFlavorData.ToStringSafe()} with processor ID {processorID.ToStringSafe()} and iteration {cachedCompFlavorData.iteration.ToStringSafe()}");
                outCompFlavor.Iteration = cachedCompFlavorData.iteration;
                outCompFlavor.CookID = cachedCompFlavorData.cookID;
                outCompFlavor.MealTags = cachedCompFlavorData.mealTags;
                activeProcesses.Remove(processorID);
                
            }
        }
        
        //remove item from CompFlavorUtility if the process is reset for any reason (despawn)
        public static void HarmonyPatch_SYR_DeregisterPostfix(ref ThingWithComps thing, ref MapComponent_Processors __instance)
        {
            var activeProcesses = __instance.map.GetComponent<CompFlavorUtility>().ActiveProcesses;
            activeProcesses.Remove(thing.ThingID);
            Log.Warning($"after deregistering, activeProcesses were [{activeProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
        }
    }
}
