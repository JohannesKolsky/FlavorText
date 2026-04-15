using System.Collections.Generic;
using Verse;

//TODO: LookMode.Reference errror for compFlavor

namespace FlavorText
{
	public class CompFlavorUtility : MapComponent
    {
        internal static int Iterations { get => iterations; private set => iterations = value; }

        internal static void Iterate()
        {
            Iterations++;
        }

        private static Dictionary<int, CompFlavor> activeProcesses;

        private static List<int> thingIDNumbers;
        private static List<CompFlavor> compFlavors;
        private static int iterations;

        public CompFlavorUtility(Map map) : base(map)
        {
            Iterations = 0;
        }

        internal static void Next()
        {
            Iterations++;
        }

        public static Dictionary<int, CompFlavor> ActiveProcesses
        {
            get
            {
                activeProcesses ??= [];
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
