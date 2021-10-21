using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StoryProgression.Configs
{
    class ModConfig
    {
        public bool DynamicPersonalities { get; set; } = true;
        public bool DynamicGiftTastes { get; set; } = false; // LATER: set back to true;
        public bool DynamicSchedules { get; set; } = false;
        public string ModderPreferences { get; set; } = "";
        public bool ToddlerSchedules { get; set; } = true;
        public int MaxChildren { get; set; } = 20;
        public string PlayerParentNicknameMale { get; set; } = "Daddy";
        public string PlayerParentNicknameFemale { get; set; } = "Mommy";
        public string SpouseParentNicknameMale { get; set; } = "Dad";
        public string SpouseParentNicknameFemale { get; set; } = "Mom";
        public int defaultTimeWakeup { get; set; } = 630;
        public int defaultTimeBedtime { get; set; } = 1900;
        public int stageLengthNewborn { get; set; } = 14;
        public int stageLengthBaby { get; set; } = 14;
        public int stageLengthCrawler { get; set; } = 28;
        public int stageLengthToddler { get; set; } = 56;
        public int minimumDaysWithMod { get; set; } = 4;

    }
}
