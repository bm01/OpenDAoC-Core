using System;
using DOL.Language;

namespace DOL.GS
{
    public abstract class AbstractProfession : AbstractCraftingSkill
    {
        protected abstract string Profession { get; }

        public static string GetTitleFormat(int skillLevel)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(skillLevel);

            return (skillLevel / 100) switch
            {
                0 => "CraftersTitle.Helper",
                1 => "CraftersTitle.JuniorApprentice",
                2 => "CraftersTitle.Apprentice",
                3 => "CraftersTitle.Neophyte",
                4 => "CraftersTitle.Assistant",
                5 => "CraftersTitle.Junior",
                6 => "CraftersTitle.Journeyman",
                7 => "CraftersTitle.Senior",
                8 => "CraftersTitle.Master",
                9 => "CraftersTitle.Grandmaster",
                _ => "CraftersTitle.Legendary"
            };
        }

        public string GetTitle(GamePlayer player, int skillLevel)
        {
            string profession = LanguageMgr.TryTranslateOrDefault(player, "!Profession!", Profession);
            return LanguageMgr.TryTranslateOrDefault(player, "!None {0}!", GetTitleFormat(skillLevel), profession);
        }
    }
}
