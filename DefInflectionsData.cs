using System.Collections.Generic;
using Verse;

namespace FlavorText
{
    public class ThingDefInflectionsData : Def
    {
        public string packageID;
        public Dictionary<string, List<string>> dictionary;
    }
    public class FlavorCategoryDefInflectionsData : Def
    {
        public string packageID;
        public Dictionary<string, List<string>> dictionary;
    }
}
