using System;
using System.Linq;
using System.Text;
using StardewModdingAPI;
using StoryProgression;
using StardewValley;
using StardewValley.Characters;
using StoryProgression.Configs;
using StoryProgression.Calculations;

namespace StoryProgression.Patches
{
    class PatchMaster : DataGetters
    {
        public static IMonitor Monitor;
        public static IModHelper OverallScope;
        public static void Initialize(IMonitor monitor)
        {
            Monitor = monitor;
        }
        public static void InitializeScope(IModHelper overallScope)
        {
            OverallScope = overallScope;
        }


    }

    

}
