using System;

namespace AreArpg.Core
{
    /// <summary>Versioned authoring IDs, never a second quest, inventory or save owner.</summary>
    public static class AuthoredHungerBinding
    {
        public const string Contract = "AshenCrypt.Fadenwerk.Hunger.v1";
        public const string QuestId = "ashen.hunger";
        public const string DialogueId = "ashen.hunger.actions";
        public static string ActionId(HungerAction action)
        {
            switch (action)
            {
                case HungerAction.HearIlyan: return "hear_ilyan";
                case HungerAction.InspectTrail: return "inspect_trail";
                case HungerAction.InspectShelter: return "inspect_shelter";
                case HungerAction.Reveal: return "reveal";
                case HungerAction.Protect: return "protect";
                case HungerAction.Replace: return "replace";
                case HungerAction.InspectBeds: return "inspect_beds";
                default: return null;
            }
        }
        public static SocialResult Execute(ArenaSimulation world, string actionId)
        {
            if (world != null && !string.IsNullOrEmpty(actionId))
                for (int i=0;i<7;i++)
                    if (ActionId((HungerAction)i)==actionId) return world.ActOnHunger((HungerAction)i);
            return new SocialResult { Text="Diese Autorenaktion ist nicht an das Spiel gebunden." };
        }
        public static int TextStage(CharacterProgression p)
        {
            if(p==null || p.EntryAct<2 || p.EntryAct>4 || p.HungerStage>=4) return -1;
            return p.HungerStage==3 && p.SupplyChoice!=HungerChoice.Undecided ? 4 : p.HungerStage;
        }
        public static string Status(CharacterProgression p)
        {
            if(p==null || p.EntryAct<2) return "locked";
            return p.HungerStage>=4 ? "completed" : p.HungerStage==0 ? "available" : "active";
        }
    }

    /// <summary>Read-only Fadenwerk text keys for existing canonical Core outcomes.</summary>
    public static class ChronicleFadenwerkBinding
    {
        public const string Contract = "AshenCrypt.Fadenwerk.LivingChronicle.v1";
        public static readonly string[] Keys =
        {
            "chronicle.camp.theft",
            "chronicle.camp.donation",
            "chronicle.camp.decision.stolen",
            "chronicle.camp.decision.replaced",
            "chronicle.story.hearilyan",
            "chronicle.story.inspecttrail",
            "chronicle.story.inspectshelter",
            "chronicle.story.reveal",
            "chronicle.story.protect",
            "chronicle.story.replace",
            "chronicle.story.inspectbeds"
        };
        public static string DialogueId(string textKey)
        {
            if (textKey == null || System.Array.IndexOf(Keys, textKey) < 0) return null;
            return "ashen." + textKey;
        }
    }
}
