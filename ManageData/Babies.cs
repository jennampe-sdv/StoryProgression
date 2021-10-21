using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;
using StardewValley;
using StardewValley.Characters;
using StardewModdingAPI;
using Netcode;
using StoryProgression.ManageData;
using System.IO;
using StoryProgression.Calculations;

namespace StoryProgression.ManageData
{
    //*** Track newly created babies so mod info can be added to them
    [HarmonyPatch(typeof(Child), MethodType.Constructor)]
    [HarmonyPatch(new Type[] { typeof(string), typeof(bool), typeof(bool), typeof(Farmer) })]
    class ChildPatching
    {
        private static IMonitor Monitor;
        private static IModHelper OverallScope;
        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }
        public static void InitializeScope(IModHelper overallScope)
        {
            OverallScope = overallScope;
        }
        public static void Postfix(NetLong ___idOfParent, NetString ___name)
        {
            OverallScope.Data.WriteJsonFile("data/baby.json", new babyInfo(___name.Value.ToString(), ___idOfParent));

            Monitor.Log("JSON noting birth of child was created.", LogLevel.Debug);
        }
    }
    public class babyInfo
    {
        public string newBabyName { get; set; }
        public string newParentID { get; set; }

        public babyInfo(string name, StardewValley.Farmer parent)
        {
            this.newBabyName = name;
            this.newParentID = parent.UniqueMultiplayerID.ToString();
        }

        public babyInfo(string name, NetLong parentID)
        {
            this.newBabyName = name;
            var intermedParentID = (long)parentID.Value;
            this.newParentID = intermedParentID.ToString();
        }
    }
    public class babyInfoJson
    {
        public string newBabyName { get; set; }
        public string newParentID { get; set; }
    }
    public class babyInfoCalcs
    {
        public static bool updateNewBaby(IModHelper helper)
        {
            // read file
            //var babyData = this.Helper.Data.ReadJsonFile<babyInfo>($"data/{Constants.SaveFolderName}_baby.json");
            var babyData = helper.Data.ReadJsonFile<babyInfoJson>("data/baby.json");

            if (babyData == null)
            {
                return false; // errored out
            }

            var newBabyName = babyData.newBabyName;
            NetLong newParentID = new NetLong(System.Int64.Parse(babyData.newParentID));

            // find the baby
            Child baby = null;
            DisposableList<NPC> all_characters = Utility.getAllCharacters();

            foreach (NPC item in all_characters) // this foreach overall structure borrowed nearly exactly from base game code
            {
                if (item.Name.Equals(newBabyName))
                {
                    var testBaby = item as Child;
                    if (testBaby != null)
                    {
                        if (testBaby.idOfParent == newParentID |
                            (Context.IsMultiplayer &&
                            testBaby.idOfParent.Value == Game1.player.team.GetSpouse(Game1.player.UniqueMultiplayerID).Value))
                        {
                            baby = testBaby;
                            break;
                            // second half of "if" borrowed nearly exactly from base game code
                        }
                    }
                }
            }

            if (baby == null)
            {
                return false; // skip all this
            }

            // info for birth order
            StardewValley.Farmer parent1 = Game1.getFarmerMaybeOffline(newParentID);
            int totalChildren = 0;
            if (!(Context.IsMultiplayer && parent1.getSpouse() == null && parent1.team.GetSpouse(parent1.UniqueMultiplayerID) != null))
            {
                // married an NPC
                totalChildren = parent1.getNumberOfChildren() + (int)parent1.stats.getStat("childrenTurnedToDoves");
            }
            else
            {
                StardewValley.Farmer parent2 = Game1.getFarmerMaybeOffline(parent1.team.GetSpouse(parent1.UniqueMultiplayerID).GetValueOrDefault());
                // married another player
                totalChildren = parent1.getChildrenCount() + (int)parent1.stats.getStat("childrenTurnedToDoves") + parent2.getChildrenCount() + (int)parent2.stats.getStat("childrenTurnedToDoves");
            }

            // update the baby's modData
            if (AddData.addChildModInfo(baby, totalChildren, null)) // if this is successful
            {
                // delete file
                FileInfo file = new FileInfo(Path.Combine(helper.DirectoryPath, "data", "baby.json"));
                if (file.Exists)
                    file.Delete();

                // add child to parent's town list
                DataGetters.addChildrenList(parent1, baby);

                // success
                return true;
            }

            // otherwise, failure
            return false;
        }
    }

    

}
