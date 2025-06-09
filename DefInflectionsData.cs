using System.Collections.Generic;
using Verse;

/// <summary>
/// predefined inflections for ingredients and ingredient categories, stored in ThingInflections.xml
/// </summary>
namespace FlavorText
{
    public class ThingInflectionsData : Def
    {
        public string packageID;
        public Dictionary<string, List<string>> dictionary;
    }
    public class FlavorCategoryInflectionsData : Def
    {
        public string packageID;
        public Dictionary<string, List<string>> dictionary;
    }
}
