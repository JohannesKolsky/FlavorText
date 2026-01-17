using System.Collections.Generic;
using Verse;

//TODO: LookMode.Reference errror for compFlavor

namespace FlavorText
{
    public class CompFlavorUtility : MapComponent
    {
        private static int iterations;
        internal static int Iterations
        {
            get { return iterations; }
        }

        internal static void Iterate()
        {
            iterations++;
        }

        private static Dictionary<int, CompFlavor> activeProcesses;

        private static List<int> thingIDNumbers;
        private static List<CompFlavor> compFlavors;

        public CompFlavorUtility(Map map) : base(map)
        {
            iterations = 0;
        }

        internal static void Next()
        {
            iterations++;
        }

        public static Dictionary<int, CompFlavor> ActiveProcesses
        {
            get
            {
                activeProcesses ??= [];
                Log.Message($"activeProcesses: {activeProcesses.ToStringSafeEnumerable()}");
                return activeProcesses;
            }
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref iterations, "iterations");
            Scribe_Collections.Look(ref activeProcesses, "activeProcesses", LookMode.Value, LookMode.Reference, ref thingIDNumbers, ref compFlavors);
        }

    }
}
