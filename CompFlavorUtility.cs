using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RimWorld;
using Verse;

namespace FlavorText
{
    public class CompFlavorUtility: MapComponent
    {
        private static Dictionary<int, CompFlavor> activeProcesses;

        private static List<int> thingIDNumbers;
        private static List<CompFlavor> compFlavors;

        public static Dictionary<int, CompFlavor> ActiveProcesses
        {
            get
            {
                activeProcesses ??= [];
                Log.Message($"activeProcesses: {activeProcesses.ToStringSafeEnumerable()}");
                return activeProcesses;
            }
        }

        public CompFlavorUtility(Map map): base(map) { }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref activeProcesses, "activeProcesses", LookMode.Value, LookMode.Reference, ref thingIDNumbers, ref compFlavors);
        }
        
    }
}
