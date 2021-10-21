using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Locations;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StoryProgression.Configs;
using StoryProgression.Calculations;
using StardewValley.Objects;

namespace StoryProgression.Patches
{
    class OtherMethods
    {
        // // FARMHOUSE
        [HarmonyPatch(typeof(FarmHouse))]
        [HarmonyPatch("getChildren")]
        class newGetChildren : PatchMaster
        {
            public static void Postfix(ref List<Child> __result, StardewValley.Locations.FarmHouse __instance)
            {
                try
                {
                    Farmer owner = __instance.owner;

                    List<Child> allChildren = __result;
                    foreach (NPC examine in Utility.getAllCharacters())
                    {
                        if (!(examine is Child)) { continue;  }

                        Child examineChild = examine as Child;
                        int childStage = DataGetters.getChildStage(examineChild);
                        

                        if (examineChild.modData.TryGetValue(ConfigsMain.dataParent1ID, out string parent1ID) &&
                            parent1ID == owner.UniqueMultiplayerID.ToString())
                        {
                            allChildren.Add(examineChild);
                        }
                        if (examineChild.modData.TryGetValue(ConfigsMain.dataParent2ID, out string parent2ID) && // check if they are the co-parent of this multiplayer child
                            parent2ID == owner.UniqueMultiplayerID.ToString())
                        {
                            allChildren.Add(examineChild);
                        }
                    }

                    List<string> childNames = new List<string> { };
                    List<int> removals = new List<int> { };

                    for (int i = 0; i < allChildren.Count; i++)
                    {
                        Child child = allChildren[i];

                        if (i == 0 || !childNames.Contains(child.Name))
                        {
                            childNames.Add(child.Name);
                        }
                        else
                        {
                            allChildren.Remove(child);
                        }
                    }

                    __result = allChildren;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in getChildren alternative:\n{ex}", LogLevel.Error);
                    // run original logic
                }
            }
        }

        [HarmonyPatch(typeof(FarmHouse))]
        [HarmonyPatch("getChildrenCount")]
        class newGetChildrenCount : PatchMaster
        {
            public static void Postfix(ref int __result, FarmHouse __instance)
            {
                try
                {
                    __result = __instance.getChildren().Count;
                }
                catch (Exception ex)
                {
                    Monitor.Log($"Failed in getChildrenCount alternative:\n{ex}", LogLevel.Error);
                }
            }
        }

        [HarmonyPatch(typeof(Utility))]
        [HarmonyPatch("playersCanGetPregnantHere")]
        class newCanGetPregnant : PatchMaster
        {
            public static void Postfix(FarmHouse farmHouse, ref bool __result)
            {
                if (!__result) // don't interfere with anything that has made this result true as of yet
                {
                    List<Child> kids = farmHouse.getChildren();

                    int numBabies = 0;
                    int numChildren = 0;
                    foreach (Child kid in kids)
                    {
                        if (DataGetters.getChildStage(kid) < 3)
                        {
                            numBabies++;
                        }
                        else
                        {
                            numChildren++;
                        }
                    }

                    string numCribsStr = farmHouse.modData.TryGetValue(ConfigsMain.furnitureNumCribs, out string cribs) ? cribs : farmHouse.cribStyle.Value.ToString();
                    int numCribs = int.TryParse(numCribsStr, out int cribs2) ? cribs2 : 0;

                    int numBeds = 0;
                    // COPIED (inspired by) FROM VANILLA FarmHouse.GetBed
                    foreach (Furniture f in farmHouse.furniture)
                    {
                        if (!(f is BedFurniture))
                        {
                            continue;
                        }
                        BedFurniture bed = f as BedFurniture;
                        if (bed.bedType == BedFurniture.BedType.Child)
                        {
                            numBeds++;
                        }
                    }

                    __result = (numCribs > numBabies) && (numBeds >= numChildren) && (numBabies + numChildren < ModEntry.maxChildrenAllowed);
                }
            }
        }

    }
}
