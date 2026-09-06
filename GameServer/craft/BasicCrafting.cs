using DOL.Language;

namespace DOL.GS
{
    public class BasicCrafting : AbstractProfession
    {
        public BasicCrafting()
        {
            Icon = 0x0F;
            Name = LanguageMgr.GetTranslation(ServerProperties.Properties.SERV_LANGUAGE, "Crafting.Name.BasicCrafting");
            eSkill = eCraftingSkill.BasicCrafting;
        }

        protected override string Profession => "CraftersProfession.BasicCrafter";

        public override void GainCraftingSkillPoints(GamePlayer player, Recipe recipe)
        {
            if (Util.Chance(CalculateChanceToGainPoint(player, recipe.Level)))
            {
                player.GainCraftingSkill(eCraftingSkill.BasicCrafting, 1);
                player.Out.SendUpdateCraftingSkills();
            }
        }

        public override string GetTitle(GamePlayer player, int skillLevel)
        {
            return string.Empty;
        }
    }
}
