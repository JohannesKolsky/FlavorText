using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace FlavorText
{
    public class FlavorThingFilter : ThingFilter
    {
        public List<ThingDef> thingDefs;
        public List<string> categories;

        public FlavorThingFilter() { }
    }
}