using LudeonTK;
using System.Collections.Generic;
using Verse;

namespace FlavorText;

// adds 3 fields to ThingCategoryDef to store FlavorText information
public class FlavorCategoryModExtension : DefModExtension
{
    public List<string> keywords = []; // keywords to search for when deciding which modded ingredients fit into which ThingCategoryDefs

    public List<ThingDef> flavorChildThingDefs = [];  // this will be copied to childThingDefs, since ThingCategoryDef's constructor clears childThingDefs

    public List<ThingCategoryDef> flavorChildCategories = [];  // this will be copied to childThingCategoryDefs, since ThingCategoryDef's constructor clears childThingCategoryDefs
}
