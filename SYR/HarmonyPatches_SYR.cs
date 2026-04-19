using FlavorText;
using HarmonyLib;
using ProcessorFramework;
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
                harmony.Patch(AccessTools.Method(AccessTools.TypeByName("ProcessorFramework.CompProcessor"), "PostDeSpawn"), postfix: new HarmonyMethod(patchType, "HarmonyPatch_SYR_PostDeSpawnPostfix"));
            }
        }

        // cache CompFlavor before old meal is destroyed
        public static void HarmonyPatch_SYR_TakeOutProductPrefix(ref ActiveProcess activeProcess, ref ThingComp __instance)
        {
            foreach (var ingredientThing in activeProcess.ingredientThings)
            {
                if (ingredientThing.TryGetComp(out CompFlavor compFlavor))
                {
                    CompFlavorUtility.ActiveProcesses.Add(__instance.parent.ThingID, new CompFlavorUtility.CompFlavorData(compFlavor));
                    Log.Warning($"activeProcesses were [{CompFlavorUtility.ActiveProcesses.Select(kvp => kvp.Key.ToStringSafe() + " : " + kvp.Value?.iteration.ToStringSafe()).ToStringSafeEnumerable()}]");
                    break;
                }
            }
        }

        // retrieve cached CompFlavor and apply it to new meal
        public static void HarmonyPatch_SYR_TakeOutProductPostfix(ref ThingComp __instance, ref Thing __result)
        {
            if (__result.TryGetComp(out CompFlavor outCompFlavor))
            {
                string processorID = __instance.parent.ThingID;
                if (CompFlavorUtility.ActiveProcesses.TryGetValue(processorID, out CompFlavorUtility.CompFlavorData cachedCompFlavorData))
                {
                    Log.Message($"Changing old CompFlavor {outCompFlavor.ToStringSafe()} with iteration {outCompFlavor.Iteration.ToStringSafe()} to cached CompFlavor {cachedCompFlavorData.ToStringSafe()} with processor ID {processorID.ToStringSafe()} and iteration {cachedCompFlavorData.iteration.ToStringSafe()}");
                    outCompFlavor.Iteration = cachedCompFlavorData.iteration;
                    outCompFlavor.CookID = cachedCompFlavorData.cookID;
                    outCompFlavor.MealTags = cachedCompFlavorData.mealTags;
                    CompFlavorUtility.ActiveProcesses.Remove(processorID);
                }
            }
        }
        
        //remove item from CompFlavorUtility if the processor despawns
        public static void HarmonyPatch_SYR_PostDeSpawnPostfix(ref CompProcessor __instance)
        {
            CompFlavorUtility.ActiveProcesses.Remove(__instance.parent.ThingID);
        }
    }
}
