using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StoryProgression.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace StoryProgression.Calculations
{
    class DataGetters
    {
        public static Vector2 childCribSpot = new Vector2(16f, 4f) * 64f + new Vector2(0f, -24f); // COPIED FROM VANILLA

        public static int getChildStage(Child child)
        {
            if (child.modData.TryGetValue(ConfigsMain.dataChildStage, out string value))
            {
                if (int.TryParse(child.modData[ConfigsMain.dataChildStage], out int savedAge))
                {
                    return savedAge;
                } else
                {
                    return child.Age;
                }
            }
            else
            {
                return child.Age;
            }
        }

        private static string getModderControlValue(Child child, string subtype = "base")
        {
            // parse arguments
            subtype = subtype.ToLower();
            string[] acceptableInputs = new string[5] { "base", "texture", "dialogue", "schedule", "gifts" };
            subtype = !acceptableInputs.Contains(subtype) ? "base" : subtype;

            string modData = child.modData.TryGetValue(getStringNameFromSubtype(subtype), out string value) ?
                                            value : null;

            return modData;
        }

        public static string getModderControl(Child child, string subtype = "base")
        {
            if (isUsingDefault(child, subtype))
            {
                if (getChildStage(child) == 3 && subtype == "schedule" && !ModEntry.defaultToddlersCanSchedule)
                {
                    return (null); // no schedules if they're default and the default toddlers don't have schedules
                } else if (getChildStage(child) == 3 && subtype == "dialogue" && !ModEntry.defaultToddlersCanSpeak)
                {
                    return (null); // no dialogue if they're default and the default toddlers don't have dialogue
                }
                else
                {
                    return ConfigsMain.childToAsset(child, subtype == "texture");
                }
            }
            else
            {
                return getModderControlValue(child, subtype);
            }

            //string modData = getModderControlValue(child, subtype);
            //if (modData == "DEFAULT")
            //{
            //    if (getChildStage(child) == 3 && subtype == "schedule" && !ModEntry.defaultToddlersCanSchedule)
            //    {
            //        return (null); // no schedules if they're default and the default toddlers don't have schedules
            //    }

            //    modData = ConfigsMain.childToAsset(child, ModEntry.GeneralMonitor);
            //}
            //return (modData);
        }

        public static bool isUsingDefault(Child child, string subtype = "base")
        {
            return (getModderControlValue(child, subtype) == ConfigsMain.logicPathDefault);
        }

        public static string getStringNameFromSubtype(string subtype)
        {
            switch (subtype)
            {
                case "texture":
                    return ConfigsMain.dataTexture;
                case "dialogue":
                    return ConfigsMain.dataDialogue;
                case "schedule":
                    return ConfigsMain.dataSchedule;
                case "gifts":
                    return ConfigsMain.dataGiftTastes;
                default:
                    return ConfigsMain.dataModder;
            }
        }

        public static string searchModderLabels(IDictionary<string, string> data, string modControlBase, int childStage)
        {
            return data.TryGetValue(modControlBase, out string value) ? modControlBase : null;
        }

        public static string getModderEntryLabel(IDictionary<string, string> data, Child child, string subtype = "base")
        {
            int childStage = getChildStage(child);
            string modControlLabel = searchModderLabels(data, getModderControl(child, subtype), childStage);

            if (modControlLabel == null) // if can't find an entry with this subtype-specific logic path, try base logic path
            {
                modControlLabel = searchModderLabels(data, getModderControl(child, "base"), childStage);
            }
            if (modControlLabel == null) // if it still is null after that, nuclear option
            {
                modControlLabel = ConfigsMain.logicPathDefault;
            }

            return modControlLabel;

        }


        //*** save list of player children so they can be accessed more easily
        public static void setChildrenList(Farmer player, bool updateModData = false)
        {
            string playerID = player.UniqueMultiplayerID.ToString();
            string savedChildNames = "";

            if (player.getChildrenCount() > 0) // player actually has children
            {
                string[] allNames = Calculations.ChildSorting.getChildrenInOrder(player, resetBirthOrder: updateModData);
                savedChildNames = string.Join(",", allNames);
            }

            Game1.getLocationFromName("Town").modData[ConfigsMain.dataPrefixChildren + playerID] = savedChildNames;
        }

        public static string[] getChildrenList(Farmer player)
        {
            string dataName = ConfigsMain.dataPrefixChildren + player.UniqueMultiplayerID.ToString();

            if (!Game1.getLocationFromName("Town").modData.TryGetValue(dataName, out string value))
            {
                setChildrenList(player);
            }

            return Game1.getLocationFromName("Town").modData[dataName].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static void addChildrenList(Farmer parent, Child child)
        {
            // get and add to data string
            List<string> currentList = getChildrenList(parent).ToList();
            currentList.Add(child.Name);

            // update data
            string dataName = ConfigsMain.dataPrefixChildren + parent.UniqueMultiplayerID.ToString();
            Game1.getLocationFromName("Town").modData[dataName] = string.Join(",", currentList);
        }

        //*** Get this child's "default path" number/ID
        public static int defaultsMax = 3; // how many "logic paths" are available?
        public static int defaultsLastMale = defaultsMax; // initialize at max so that the first path is 1
        public static int defaultsLastFemale = defaultsMax; // initialize at max so that the first path is 1

        public static int getDefaultNumber(Child child, IMonitor monitor)
        {

            int defaultID;
            bool updateSavedID = true; // in most cases, we need to update the saved modData

            if (child.modData.TryGetValue(ConfigsMain.dataDefaultID, out string existDefaultID))
            {
                if (!int.TryParse(existDefaultID, out int parsedDefaultID))
                {
                    monitor.Log("Unable to parse existing default path ID for " + child.Name + ": '" + existDefaultID + "'. Instead, re-setting to 1.", LogLevel.Debug);
                    defaultID = 1;
                }
                else
                {
                    defaultID = parsedDefaultID;
                    updateSavedID = false; // there's nothing wrong with the saved ID
                }
            }
            else // this child has not been assigned a default path: assign one
            {
                if (child.Gender == 0) // is male
                {
                    defaultsLastMale = defaultsLastMale >= defaultsMax ? 1 : defaultsLastMale + 1; // iterate up 1, unless already at max, then circle back to beginning
                    defaultID = defaultsLastMale; // this new value is the reported default ID
                }
                else // is female
                {
                    defaultsLastFemale = defaultsLastFemale >= defaultsMax ? 1 : defaultsLastFemale + 1; // iterate up 1, unless already at max, then circle back to beginning
                    defaultID = defaultsLastFemale; // this new value is the reported default ID
                }
            }

            // update modData
            if (updateSavedID)
            {
                child.modData[ConfigsMain.dataDefaultID] = defaultID.ToString();
            }

            return defaultID;

        }

        //*** Assess whether this child can talk or not
        public static bool getCanTalk(Child child)
        {
            int childStage = getChildStage(child);

            if (childStage < 3) // newborn, baby, or crawler: cannot speak
            {
                return false;
            }
            else if (childStage >= 4) // child and up: can speak
            {
                return true;
            }
            else if (ModEntry.disallowsToddlerSpeech.Contains(getModderControl(child, "texture"))) // they're a toddler, and their texture doesn't have portraits
            {
                return false;
            }
            else if (isUsingDefault(child, "texture") && !ModEntry.defaultToddlersCanSpeak) // they have default texture + specified default texture toddlers can't speak
            {
                return false;
            }
            else // they're a toddler, but their texture technically permits talking; check if they know how to speak
            {
                // LATER: add "learned" toddler speech
                return true;
            }
        }
    }
}
