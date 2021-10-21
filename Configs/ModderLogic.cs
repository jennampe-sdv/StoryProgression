using StardewModdingAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StoryProgression.Configs;

namespace StoryProgression.Configs
{
    public class ModderLogicShell
    {
        public string ModderHandle { get; set; } = "";

        public ModderLogic[] Paths { get; set; }
    }
    public class ModderLogic
    {
        public string PathName { get; set; }
        public bool AllorNone { get; set; } = false;
        public string ValidGenders { get; set; } = "both";
        public string ValidParents { get; set; } = "all";
        public string ValidAges { get; set; } = "child";
        public string ValidBirthOrder { get; set; } = "all";
        public bool ProvidesDisposition { get; set; } = false;
        public bool ProvidesGiftTastes { get; set; } = false;
        public bool ProvidesTextures { get; set; } = false;
        public bool ProvidesDialogue { get; set; } = false;
        public bool ProvidesSchedule { get; set; } = false;
        public bool ProvidesAnimation { get; set; } = false;
        public bool AllowsToddlerSpeech { get; set; } = false;
        public string ParserControl { get; set; } = "ContentPatcher";

    }

}
