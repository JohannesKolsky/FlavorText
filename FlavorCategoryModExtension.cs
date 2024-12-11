using System.Collections.Generic;
using Verse;

namespace FlavorText;

public class FlavorCategoryModExtension : DefModExtension
{
    public List<string> keywords;

    public List<ThingDef> flavorChildThingDefs = new List<ThingDef>();

    public List<ThingCategoryDef> flavorChildCategories = new List<ThingCategoryDef>();
}
