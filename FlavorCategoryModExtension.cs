using LudeonTK;
using System.Collections.Generic;
using Verse;

namespace FlavorText;

// adds 4 fields to ThingCategoryDef to store FlavorText information
public class FlavorCategoryModExtension : DefModExtension
{
    public int specificity;  // how many non-duplicate descendant ThingDefs are in this ThingCategoryDef

    public List<string> keywords = []; // keywords to search for when deciding which modded ingredients fit into which ThingCategoryDefs

    public List<string> blacklist = []; // keywords NOT to match; e.g. pig != guinea pig

    public List<ThingDef> flavorChildThingDefs = [];  // this will be copied to childThingDefs, since ThingCategoryDef's constructor clears childThingDefs

    public List<ThingCategoryDef> flavorSisterCategories = []; // corresponding vanilla ThingCategory; FT_ThingCategories are their own separate tree under Root
}
