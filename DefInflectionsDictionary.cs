using System.Collections.Generic;
using Verse;

namespace FlavorText
{
    public class ThingDefInflectionsDictionary : Def
    {
        public string packageID;
        public Dictionary<string, List<string>> dictionary;
    }
    public class FlavorCategoryDefInflectionsDictionary : Def
    {
        public string packageID;
        public Dictionary<string, List<string>> dictionary;
    }
}
