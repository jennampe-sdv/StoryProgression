using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;

namespace StoryProgression.Calculations
{
    class ChildSorting
    {
        public static string[] getChildrenInOrder(Farmer parent, bool resetBirthOrder = false)
        {
            List<Child> allChildren = parent.getChildren();
            if (Context.IsMultiplayer && parent.getSpouse() == null && parent.team.GetSpouse(parent.UniqueMultiplayerID) != null)
            {
                // this is a multiplayer game
                var spouse = Game1.getFarmerMaybeOffline(parent.team.GetSpouse(parent.UniqueMultiplayerID).GetValueOrDefault());

                allChildren.AddRange(spouse.getChildren());
            }

            int[] allChildAges = new int[allChildren.Count];
            string[] allChildNames = new string[allChildren.Count];

            int iter = 0;
            foreach (var child in allChildren)
            {
                allChildAges[iter] = child.daysOld.Value;
                allChildNames[iter] = child.Name;

                iter++;
            }
            Array.Sort(allChildAges, allChildNames);
            Array.Reverse(allChildNames); // put oldest child first

            if (resetBirthOrder)
            {
                // set modData for children
                foreach (var child in allChildren)
                {
                    int birthOrder = Array.IndexOf(allChildNames, child.Name);
                    child.modData[Configs.ConfigsMain.dataBirthOrder] = birthOrder.ToString();
                }
            }

            return allChildNames;
        }
    }
}
