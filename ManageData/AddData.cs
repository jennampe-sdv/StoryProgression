using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StoryProgression.Calculations;
using StoryProgression.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoryProgression.ManageData
{
    class AddData
    {
        //*** Add overall mod data to Childs
        public static bool addChildModInfo(Child child, int birthOrder, IMonitor monitor)
        {
            // identify the parent of record
            StardewValley.Farmer parent1 = null;
            if (Context.IsMultiplayer)
            {
                parent1 = Game1.getFarmerMaybeOffline(child.idOfParent.Value);
            }
            else
            {
                parent1 = Game1.player;
            }

            if (parent1 == null)
            {
                return false; // error: skip all this
            }

            // child parents: set to player + current spouse
            child.modData[ConfigsMain.dataParent1] = parent1.displayName;
            child.modData[ConfigsMain.dataParent1ID] = parent1.UniqueMultiplayerID.ToString();
            child.modData[ConfigsMain.dataParent1G] = parent1.IsMale ? "m" : "f";

            // // info on spouse
            bool marryNPC = !(Context.IsMultiplayer && parent1.getSpouse() == null && parent1.team.GetSpouse(parent1.UniqueMultiplayerID) != null);
            string spouseName;
            string spouseID;
            string spouseGender;

            if (marryNPC)
            {
                spouseName = parent1.getSpouse().displayName;
                spouseID = parent1.getSpouse().Name;
                spouseGender = parent1.getSpouse().Gender == 0 ? "m" : "f";
            }
            else
            {
                Farmer parent2 = Game1.getFarmerMaybeOffline(parent1.team.GetSpouse(parent1.UniqueMultiplayerID).GetValueOrDefault());
                if (parent2 == null)
                {
                    // something went wrong, error out
                    return false;
                }

                spouseName = parent2.displayName;
                spouseID = parent2.UniqueMultiplayerID.ToString();
                spouseGender = parent2.IsMale ? "m" : "f";
            }

            child.modData[ConfigsMain.dataParent2] = spouseName;
            child.modData[ConfigsMain.dataParent2ID] = spouseID;
            child.modData[ConfigsMain.dataParent2G] = spouseGender;
            child.modData[ConfigsMain.dataNPCParent] = marryNPC ? "true" : "false";

            // child age: 0 = newborn; 1 = baby; 2 = crawler; 3 = toddler
            // // with extensions: 4 = child; 5 = teen; 6 = adult
            child.modData[ConfigsMain.dataChildStage] = child.Age.ToString();

            // birth order
            child.modData[ConfigsMain.dataBirthOrder] = birthOrder.ToString();

            // birthday 
            List<string> allSeasons = new List<string> { "spring", "summer", "fall", "winter" };

            int calcDOBDay = Game1.dayOfMonth - child.daysOld.Value;

            int calcDOBSeason = allSeasons.IndexOf(Game1.currentSeason);
            int calcDOBYear = Game1.year;

            while (calcDOBDay <= 0)
            {
                calcDOBDay += 28; // add another month on
                calcDOBSeason--;

                if (calcDOBSeason < 0) // need to shift to previous year
                {
                    calcDOBSeason = 3; // change to winter
                    calcDOBYear--;
                }
            }

            child.modData[ConfigsMain.dataBirthYear] = calcDOBYear.ToString();
            child.Birthday_Day = calcDOBDay;
            child.Birthday_Season = allSeasons[calcDOBSeason];

            // lives at home
            // possible values: "farm" [lives with player]; "divorce" [lives with divorced spouse]; "other" [lives off farm as adult]
            child.modData[ConfigsMain.dataLivesHome] = "farm";

            // logic paths
            assignLogicPaths(child, monitor);
            
            return true;
        }

        //*** Assign logic paths

        public static string selectLogicPath(Child child, string subtype = "base", string prefer = null, bool allowComplete = false)
        {
            // get child demographics
            string stage = null;
            switch (DataGetters.getChildStage(child))
            {
                case 0:
                case 1:
                case 2:
                    stage = "baby";
                    break;
                case 3:
                    stage = "toddler";
                    break;
                case 4:
                    stage = "child";
                    break;
                case 5:
                    stage = "teen";
                    break;
                default:
                    stage = "adult";
                    break;
            }
            string gender = child.Gender == 0 ? "boy" : "girl";
            string parent = child.modData[ConfigsMain.dataNPCParent] == "true" ? child.modData[ConfigsMain.dataParent2ID].ToLower() : "multiplayer";
            string birthOrder = ("birth" + child.GetChildIndex().ToString());

            // validate subtype value
            List<string> validSubtypes = new List<string>() { "base", "texture", "dialogue", "schedule", "gifts" };
            if (!validSubtypes.Contains(subtype)) { subtype = "base";  } // reset invalid entries to "base"

            // find used + available values
            Dictionary<string, List<string>> allPaths = ModEntry.allLogicPaths[subtype];
            List<string> usedPaths = ModEntry.usedLogicPaths[subtype];

            // identify which paths for this child are applicable
            // // by parent
            List<string> extantPaths = allPaths.TryGetValue(parent, out List<string> value) ? value : new List<string>();
            if (allPaths.TryGetValue("all_parent", out List<string> allParents))
            {
                extantPaths.AddRange(allPaths["all_parent"]);
            }

            // // by gender
            if (allPaths.TryGetValue(gender, out List<string> allGender))
            {
                extantPaths = extantPaths.Intersect(allGender).Distinct().ToList();
            }
            // // by birth order
            if (allPaths.TryGetValue(birthOrder, out List<string> allBirths))
            {
                extantPaths = extantPaths.Intersect(allBirths).Distinct().ToList();
            }
            // // by age bracket
            if (allPaths.TryGetValue(stage, out List<string> allStages))
            {
                extantPaths = extantPaths.Intersect(allStages).Distinct().ToList();
            }

            // can we attempt all or none entries?
            if (allowComplete)
            {
                List<string> allOrNonePaths = ModEntry.allOrNone.TryGetValue(parent, out List<string> value2) ? value2 : new List<string>();
                if (ModEntry.allOrNone.TryGetValue("all_parent", out List<string> allParents2))
                {
                    allOrNonePaths.AddRange(allParents2);
                }
                // // by gender
                if (ModEntry.allOrNone.TryGetValue(gender, out List<string> allGender2))
                {
                    allOrNonePaths = allOrNonePaths.Intersect(allGender2).Distinct().ToList();
                }
                // // by birth order
                if (ModEntry.allOrNone.TryGetValue(birthOrder, out List<string> allBirths2))
                {
                    allOrNonePaths = allOrNonePaths.Intersect(allBirths2).Distinct().ToList();
                }
                // // by age bracket
                if (ModEntry.allOrNone.TryGetValue(stage, out List<string> allStages2))
                {
                    allOrNonePaths = allOrNonePaths.Intersect(allStages2).Distinct().ToList();
                }
                // combine with other paths
                extantPaths.AddRange(allOrNonePaths);
                extantPaths = extantPaths.Distinct().ToList();
            }


            // identify which paths are still up for grabs
            List<string> usablePaths = extantPaths.Except(usedPaths).ToList();
            usablePaths = usablePaths.Except(ModEntry.blacklistLogicPaths[subtype]).ToList(); // do not consider paths known to have issues this game run

            // pick a path
            if (usablePaths.Count == 0)
            {
                // no more paths remain to be assigned
                return ConfigsMain.logicPathDefault;
            }

            // try to honor preference, if possible
            string finalSelection = null;
            bool foundIt = false;
            if (prefer != null && usablePaths.Contains(prefer))
            {
                finalSelection = prefer;
                foundIt = true;
            }
            else if (ModEntry.userModderPrefs.Count > 0 && !foundIt)
            {
                foreach (string pref in ModEntry.userModderPrefs)
                {
                    foreach (string entry in usablePaths)
                    {
                        if (entry.Contains(pref)) { finalSelection = entry; foundIt = true; }
                        break;
                    }

                    if (foundIt) { break; }
                }
            }
            if (!foundIt)
            {
                int index = new Random().Next(usablePaths.Count);
                finalSelection = usablePaths[index];
            }

            // add the used value to the listings of used data
            ModEntry.usedLogicPaths[subtype].Add(finalSelection);
            ModEntry.pathToChildDict[finalSelection][subtype] = child.Name;
            // // additions to child object itself made in assignLogicPaths()

            return finalSelection;
        }

        public static void assignLogicPaths(Child child, IMonitor monitor, bool badPaths = false) // "badPaths" determines whether old paths are removed from allLogicPaths
        {
            // save current logic paths (if any) for easier access
            string genInfo = DataGetters.getModderControl(child, "base");
            string gifts = DataGetters.getModderControl(child, "gifts");
            string dialogue = DataGetters.getModderControl(child, "dialogue");
            string texture = DataGetters.getModderControl(child, "texture");
            string schedule = DataGetters.getModderControl(child, "schedule");

            // find out which paths need to be changed
            bool blankGenInfo = genInfo == null;
            bool defaultGenInfo = DataGetters.isUsingDefault(child, "base");
            bool blankGifts = gifts == null;
            bool defaultGifts = DataGetters.isUsingDefault(child, "gifts");
            bool blankDialogue = dialogue == null;
            bool defaultDialogue = DataGetters.isUsingDefault(child, "dialogue");
            bool blankTexture = texture == null;
            bool defaultTexture = DataGetters.isUsingDefault(child, "texture");
            bool blankSchedule = schedule == null;
            bool defaultSchedule = DataGetters.isUsingDefault(child, "schedule");


            // does this child need EVERY path? (ie eligible for all or none)
            bool canUseAllOrNone = (blankGenInfo || defaultGenInfo) && (blankGifts || defaultGifts) &&
                                        (blankTexture || defaultTexture) && (blankDialogue || defaultDialogue) && (blankSchedule || defaultSchedule) &&
                                        (!ModEntry.dynamPersonality && !ModEntry.dynamGifts && !ModEntry.dynamSched);

            // fill in logic paths
            if ((blankGenInfo || defaultGenInfo) && !ModEntry.dynamPersonality) { genInfo = selectLogicPath(child, "base", allowComplete: canUseAllOrNone); }
            string preference = genInfo != null && genInfo != ConfigsMain.logicPathDefault ? genInfo : null;

            if ((blankGifts || defaultGifts) && !ModEntry.dynamGifts) { gifts = selectLogicPath(child, "gifts", preference, allowComplete: canUseAllOrNone); }
            preference = gifts != preference && gifts != null && gifts != ConfigsMain.logicPathDefault ? gifts : preference;
            
            if (blankTexture || defaultTexture) { texture = selectLogicPath(child, "texture", preference, allowComplete: canUseAllOrNone); }
            preference = texture != preference && texture != null && texture != ConfigsMain.logicPathDefault ? texture : preference;
            
            if (blankDialogue || defaultDialogue) { dialogue = selectLogicPath(child, "dialogue", preference, allowComplete: canUseAllOrNone); }
            preference = dialogue != preference && dialogue != null && dialogue != ConfigsMain.logicPathDefault ? dialogue : preference;
            
            if (blankSchedule && !ModEntry.dynamSched) { schedule = selectLogicPath(child, "schedule", preference, allowComplete: canUseAllOrNone); }

            // set logic paths
            if (!ModEntry.dynamPersonality) { updateLogicPaths(child, "base", genInfo, badPaths); }

            if (texture != DataGetters.getModderControl(child, "texture")) // texture changed
            {
                updateLogicPaths(child, "texture", texture, badPaths);
                child.reloadSprite();
            }
            if (dialogue != DataGetters.getModderControl(child, "dialogue")) // dialogue changed
            {
                updateLogicPaths(child, "dialogue", dialogue, badPaths);
            }

            if (!ModEntry.dynamSched) { updateLogicPaths(child, "schedule", schedule, badPaths); }
            if (!ModEntry.dynamGifts) { updateLogicPaths(child, "gifts", gifts, badPaths); }
        }
    
        public static void updateLogicPaths(Child child, string subtype, string newVal, bool badPaths)
        {
            string[] validSubtypes = new string[5] { "base", "gifts", "dialogue", "texture", "schedule" };
            subtype = validSubtypes.Contains(subtype) ? subtype : "base"; // error prevention

            // get old/current value
            string oldVal = child.modData.TryGetValue(DataGetters.getStringNameFromSubtype(subtype), out string result) ? result : null;

            if ( oldVal != null && oldVal.Equals(newVal)) { return; } // no change

            // remove child/path from registries associated with old value
            if (oldVal != null && !DataGetters.isUsingDefault(child, subtype))
            {
                ModEntry.usedLogicPaths[subtype].Remove(oldVal); // this logic path is no longer in use for this subtype
                ModEntry.pairedLogicPaths[subtype][oldVal] = null; // this child is no longer associated with this logic path
                if (badPaths) { ModEntry.blacklistLogicPaths[subtype].Add(oldVal); } // add this path to list of paths for this subtype with current issues
            }

            // add child/path to registries associated with new value
            if (newVal != ConfigsMain.logicPathDefault)
            {
                ModEntry.usedLogicPaths[subtype].Add(newVal);
                ModEntry.pairedLogicPaths[subtype][newVal] = child.Name;
            }

            // set the new value
            child.modData[DataGetters.getStringNameFromSubtype(subtype)] = newVal;

            if (subtype == "schedule")
            {
                child.Schedule = child.getSchedule(Game1.dayOfMonth); // reset schedule
            }
            if (subtype == "dialgoue")
            {
                Dictionary<string, string> testDialogue = child.Dialogue; // the act of getting the Dialgogue will reset it
            }

        }

        //*** Verify/check on logic paths
        public static void updateAssets(Child child, IMonitor monitor, int iter = 1)
        {
            
            if (iter != 1)
            {
                monitor.Log($"Attempt {iter} to update assets for child {child.Name}.", LogLevel.Trace);
            }

            // set all to correct (no update needed) at start
            bool needsTexture = false;
            bool needsSchedule = false;
            bool needsDialogue = false;
            bool needsGifts = false;

            // needed parameters for convenience
            int childStage = int.Parse(child.modData[ConfigsMain.dataChildStage]);
            bool shouldHaveSchedule = childStage >= 4 ||
                (childStage == 3 && ModEntry.toddlerSchedules && (!DataGetters.isUsingDefault(child, "texture") | !ModEntry.defaultToddlersCanSchedule));

            // CHECK TEXTURES
            // /// overworld sprite
            if (child.Sprite == null)
            {
                monitor.Log($"Blank sprite for child {child.Name}. Attempting replacement.", LogLevel.Debug);
                needsTexture = true;
            }
            // // portrait
            if (!needsTexture && DataGetters.getCanTalk(child)) // no point checking portrait if: we already know they need a texture; or, they can't talk
            {
                if (child.Portrait == null)
                {
                    monitor.Log($"Blank portrait for child {child.Name}. Attempting replacement.", LogLevel.Debug);
                    needsTexture = true;
                }
            }    

            // CHECK SCHEDULE
            if (child.Schedule == null && !ModEntry.dynamSched && shouldHaveSchedule)
            {        
                monitor.Log($"Blank schedule for child {child.Name}. Attempting replacement.", LogLevel.Debug);
                needsSchedule = true;
            }

            // CHECK DIALOGUE
            if (child.Dialogue == null && childStage >= 4)
            {
                monitor.Log($"Blank dialogue for child {child.Name}. Attempting replacement.", LogLevel.Debug);
                needsDialogue = true;
            }

            // CHECK GIFT TASTES
            if (!Game1.NPCGiftTastes.TryGetValue(child.Name, out var value) && !ModEntry.dynamGifts && childStage >= 4)
            {
                monitor.Log($"Blank gift tastes for child {child.Name}. Attempting replacement.", LogLevel.Debug);
                needsGifts = true; // TO DO: turn this back on
            }

            // IS ALL WELL?
            if (!needsTexture && !needsSchedule && !needsDialogue && !needsGifts)
            {
                ModEntry.childrenToRemove.Add(child); // queue this child for removal from watchlist
                return;
            }

            // IF NOT, APPLY UPDATES
            if (needsTexture) { child.modData[DataGetters.getStringNameFromSubtype("texture")] = null;  }
            if (needsSchedule) { child.modData[DataGetters.getStringNameFromSubtype("schedule")] = null; }
            if (needsDialogue) { child.modData[DataGetters.getStringNameFromSubtype("dialogue")] = null; }
            if (needsGifts) { child.modData[DataGetters.getStringNameFromSubtype("gifts")] = null; }

            assignLogicPaths(child, monitor, badPaths: true);

            // if this Child is not on the watch list, add it
            ModEntry.addChildToWatchlist(child);

            //updateAssets(child, monitor, iter: iter+1);

        }
    }
}
