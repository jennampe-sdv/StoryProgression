using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley.Characters;
using StoryProgression.ManageData;

namespace StoryProgression.Configs
{
    class ConfigsMain
    {
        public static string logicPathDefault = "DEFAULT";

        public static string dataPrefix = "jennampe/sp";

        public static string dataModder = dataPrefix + "ModderPrimary"; // used as preference for other categories, and mandatory for NPCDispositions
        public static string dataTexture = dataPrefix + "ModderTexture";
        public static string dataTextureFile = dataPrefix + "TextureFile"; // this is the actual file name (with "xChild" etc.)
        public static string dataDialogue = dataPrefix + "ModderDialogue";
        public static string dataSchedule = dataPrefix + "ModderSchedule";
        public static string dataGiftTastes = dataPrefix + "ModderGifts";

        public static string dataBirthOrder = dataPrefix + "BirthOrder";
        public static string dataChildStage = dataPrefix + "ChildStage";
        public static string dataLivesHome = dataPrefix + "LivesHome";

        public static string dataBirthYear = dataPrefix + "BirthYear";
        public static string dataVanillaAge = dataPrefix + "AgeVanilla";
        public static string dataModdedAge = dataPrefix + "AgeModded";

        public static string dataParent1 = dataPrefix + "Parent1";
        public static string dataParent1G = dataPrefix + "Parent1G";
        public static string dataParent1ID = dataPrefix + "Parent1ID";
        public static string dataParent2 = dataPrefix + "Parent2";
        public static string dataParent2G = dataPrefix + "Parent2G";
        public static string dataParent2ID = dataPrefix + "Parent2ID";
        public static string dataNPCParent = dataPrefix + "NPCParent";

        public static string dataTemperament = dataPrefix + "Temperament";
        public static string dataManners = dataPrefix + "Manners";
        public static string dataAnxiety = dataPrefix + "Anxiety";
        public static string dataOptimism = dataPrefix + "Optimism";

        public static string dataDefaultID = dataPrefix + "DefaultID";

        public static string scheduleWakeUp = dataPrefix + "WakeupTime";
        public static string scheduleGoSleep = dataPrefix + "BedTime";

        public static string prefixTownUsedPaths = dataPrefix + "LogicsUsed";
        public static string dataTownUsedTexture = prefixTownUsedPaths + "Textu";
        public static string dataTownUsedSched = prefixTownUsedPaths + "Sched";
        public static string dataTownUsedDialogue = prefixTownUsedPaths + "Dialo";
        public static string dataTownUsedGenInfo = prefixTownUsedPaths + "Info";
        public static string dataTownUsedGiftTastes = prefixTownUsedPaths + "Gifts";

        public static string furnitureBedOwner = dataPrefix + "BedAssign"; // modData for the *BED* itself, marking child's name
        public static string furnitureBedCheckResult = dataPrefix + "BedCheck"; // prevents ten minute update from constantly checking for a bed
        public static string furnitureNumCribs = dataPrefix + "NumCribs"; // LATER: tracks number of cribs in the house

        public static string dataPrefixChildren = "jennampe/children_"; // this is used to prefix the list of a player's children
        public static string tempSetMarried = dataPrefixChildren + "tempMarried"; // controls whether Child should temporarily return "married" for married logic
        

        public static Dictionary<string, string> defaultDialogueFills = new Dictionary<string, string>
        {
            { "player parent", "XXPLAYERXX" },
            { "npc parent", "XXNPCXX" },
            { "npc parent s/he", "XXNPC-HE-SHEXX" }
        };

        public static List<string> vanillaChars = new List<string>(){
            "Abigail", "Alex", "Caroline", "Clint", "Demetrius",
            "Elliott", "Emily", "Evelyn", "George", "Gus", "Haley",
            "Harvey", "Jas", "Jodi", "Kent", "Leah", "Leo", "Lewis",
            "Linus", "Marnie", "Maru", "Pam", "Penny", "Pierre", "Robin",
            "Sam", "Sandy", "Sebastian", "Shane", "Vincent", "Willy"
            };

        public static List<string> vanillaCharsLower = vanillaChars.Select(x => x.ToLower()).ToList();

        public static string childToAsset(Child child, bool useSkin = false)
        {
            // asset should be specified as: SPDEFAULT_[light/dark]_[boy/girl][1-3]_[btc]
            // example: SPDEFAULT_light_boy1_b
            //string startPhrase = "SPDEFAULT" + (useSkin ? "_" + (child.darkSkinned.Value ? "dark" : "light") : "");
            return (String.Join("_", new string[]
            {
                "SPDEFAULT",
                child.darkSkinned.Value ? "dark" : "light",
                //startPhrase,
                (child.Gender == 0 ? "boy" : "girl") + Calculations.DataGetters.getDefaultNumber(child,  ModEntry.GeneralMonitor).ToString(),
                new Dictionary<String, String>()
                {
                    {"0", "b" }, // newborn
                    {"1", "b" }, // baby
                    {"2", "b" }, // crawler
                    {"3", "t" }, // toddler
                    {"4", "c" }, // child
                    {"5", "e" }, // teen
                    {"6", "a" } // adult
                }[child.modData[ConfigsMain.dataChildStage]]

                // child age: 0 = newborn; 1 = baby; 2 = crawler; 3 = toddler
                // with extensions: 4 = child; 5 = teen; 6 = adult
            }));

        }

        public static string assetToFilePath(string assetName)
        {
            // asset should be specified as: SPDEFAULT_[light/dark]_[boy/girl][1-3]_[btc]
            // example: SPDEFAULT_light_boy1_b

            // parse different elements of the asset name
            int spdefaultPosition = assetName.IndexOf("SPDEFAULT");
            if (spdefaultPosition < 0) // SPDEFAULT not found
            {
                return null; // return null, so calling function can throw an error
            }
            string[] assetParameters = assetName.Substring(spdefaultPosition).ToLower().Split('_');
            if (assetParameters.Length != 4)
            {
                return null; // return null, so calling function can throw an error
            }

            // what type of asset is this?
            string assetType = null;
            string filePathExt = null;
            if (assetName.Contains("Portraits"))
            {
                assetType = Path.Combine("textures", assetParameters[1], "portraits");
                filePathExt = ".png";
            } else if (assetName.Contains("Dialogue") && assetName.Contains("Marriage"))
            {
                // marriage dialogue not supported for default children
            } else if (assetName.Contains("Dialogue") && assetName.Contains("rainy"))
            {
                assetType = Path.Combine("text", "rainy_dialogue");
                filePathExt = ".json";
            } else if (assetName.Contains("Dialogue"))
            {
                assetType = Path.Combine("text", "dialogue");
                filePathExt = ".json";
            } else if (assetName.Contains("schedules"))
            {
                assetType = Path.Combine("text", "schedules");
                filePathExt = ".json";
            } else if (assetName.Contains("Dispositions"))
            {
                // TO DO
            }
            else if (assetName.Contains("GiftTastes"))
            {
                // TO DO
            }
            else if (assetName.Contains("Characters"))
            {
                assetType = Path.Combine("textures", assetParameters[1], "sprites");
                filePathExt = ".png";
            }
            if (assetType == null)
            {
                return null; // return null, so calling function can throw an error
            }

            // what age is this child?
            string filePathAge = "child"; // as default
            switch (assetParameters[3])
            {
                case "b":
                    filePathAge = "baby"; break;
                case "t":
                    filePathAge = "toddler"; break;
                case "c":
                    filePathAge = "child"; break;
            }

            // put together the whole file path
            string[] filePathComps = new string[] {
                    "assets", "defaults",
                    assetType,
                    assetParameters[2].Contains("boy") ? "boy" : "girl", // boy or girl (subfolder)
                    String.Join("_", new string[] {
                            assetParameters[2], // boy or girl + entity number
                            filePathAge + filePathExt // age as calculated above + file extension
                        })
                    };

            return (Path.Combine(filePathComps));
        }

    }
}
