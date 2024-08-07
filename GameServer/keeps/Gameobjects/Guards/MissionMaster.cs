/*
 * DAWN OF LIGHT - The first free open source DAoC server emulator
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU General Public License
 * as published by the Free Software Foundation; either version 2
 * of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.
 *
 */

using DOL.AI.Brain;
using DOL.GS.PlayerClass;
using DOL.GS.Quests;
using DOL.GS.ServerProperties;
using DOL.Language;
using System;

namespace DOL.GS.Keeps
{
	/// <summary>
	/// Represents a mission master
	/// </summary>
	public class MissionMaster : GameKeepGuard
	{
		public override bool Interact(GamePlayer player)
		{
			if (!base.Interact(player))
				return false;

			if (Component == null)
				SayTo(player, "Greetings, " + player.Name + ". We have put out the call far and wide for heroes such as yourself to aid us in our ongoing struggle. It warms my heart good to to see a great " + player.CharacterClass.Name + " such as yourself willing to lay their life on the line in defence of the [realm].");
			else SayTo(player, "Hail and well met, " + player.Name + "! As the leader of our forces, I am calling upon our finest warriors to aid in the vanquishing of our enemies. Do you wish to do your duty in defence of our [realm]?");
			return true;
		}

		public override bool WhisperReceive(GameLiving source, string str)
		{
			if (!base.WhisperReceive(source, str))
				return false;

			GamePlayer player = source as GamePlayer;
			if (player == null)
				return false;

			if (!GameServer.ServerRules.IsSameRealm(this, player, true))
			{
				return false;
			}

			if (str.ToLower().StartsWith("tower capture"))
			{
				if (player.Group == null)
				{
					SayTo(player, "You are not in a group!");
				}
				else if (player.Group.Leader != player)
				{
					SayTo(player, "You are not the leader of your group!");
				}
				else
				{
					if (player.Group.Mission != null)
						player.Group.Mission.ExpireMission();

					player.Group.Mission = new CaptureMission(CaptureMission.eCaptureType.Tower, player.Group, str.ToLower().Replace("tower capture", "").Trim());
				}
			}
			else if (str.ToLower().StartsWith("keep capture"))
			{
				if (player.Group == null)
				{
					SayTo(player, "You are not in a group!");
				}
				else if (player.Group.Leader != player)
				{
					SayTo(player, "You are not the leader of your group!");
				}
				else
				{
					if (player.Group.Mission != null)
						player.Group.Mission.ExpireMission();

					player.Group.Mission = new CaptureMission(CaptureMission.eCaptureType.Keep, player.Group, str.ToLower().Replace("keep capture", "").Trim());
				}
			}
			else
			{
				switch (str.ToLower())
				{
					case "realm":
						{
							if (Component == null)
								SayTo(player, "We all must do our part. How would you like to assist the cause? I have [personal missions], [group missions], and [guild missions] available.");
							else SayTo(player, "Excellent! We all must do our part. How would you like to assist the cause? I have [personal missions] and [group missions] available.");
							break;
						}
					case "personal missions":
						{
							SayTo(player, "We have several personal missions from which to choose. Would you like to claim the bounty on some [realm guards], or claim the bounties on some [enemies of the realm]? Perhaps a frontal assault isn't your style? If so, we also have missions that require you to [reconnoiter] an enemy realm, or elimate the thread of an impending [assassination]?");
							break;
						}
					case "realm guards":
						{
							if (player.Mission != null)
								player.Mission.ExpireMission();
							player.Mission = new KillMission(typeof(GameKeepGuard), 15, "enemy realm guards", player);
							break;
						}
					case "enemies of the realm":
						{
							if (player.Mission != null)
								player.Mission.ExpireMission();
							player.Mission = new KillMission(typeof(GamePlayer), 5, "enemy players", player);
							break;
						}
					case "reconnoiter":
						{
							if (player.Mission != null)
								player.Mission.ExpireMission();
							player.Mission = new ScoutMission(player);
							break;
						}
					case "assassination":
						{
							SayTo(player, "This type of mission is not yet implemented");
							break;
						}
					case "group missions":
						{
							if (player.Group == null)
							{
								SayTo(player, "You are not in a group!");
								break;
							}

							if (player.Group.Leader != player)
							{
								SayTo(player, "You are not the leader of your group!");
								break;
							}

							SayTo(player, "Would your group like to help with a [tower capture], a [keep capture], or a [caravan] raid? Should those choices fail to appeal to you, I also have bounty missions on [enemy guards] and [realm enemies] if that is your preference.");
							break;
						}
					case "tower raize":
						{
							if (player.Group == null)
							{
								SayTo(player, "You are not in a group!");
								break;
							}

							if (player.Group.Leader != player)
							{
								SayTo(player, "You are not the leader of your group!");
								break;
							}
							player.Group.Mission = new RaizeMission(player.Group);
							break;
						}
					case "tower capture":
						{
							break;
						}
					case "keep capture":
						{
							break;
						}
					case "caravan":
						{
							if (player.Group == null)
							{
								SayTo(player, "You are not in a group!");
								break;
							}

							if (player.Group.Leader != player)
							{
								SayTo(player, "You are not the leader of your group!");
								break;
							}
							SayTo(player, "This type of mission is not yet implemented");
							break;
						}
					case "enemy guards":
						{
							if (player.Group == null)
							{
								SayTo(player, "You are not in a group!");
								break;
							}

							if (player.Group.Leader != player)
							{
								SayTo(player, "You are not the leader of your group!");
								break;
							}
							if (player.Group.Mission != null)
								player.Group.Mission.ExpireMission();
							player.Group.Mission = new KillMission(typeof(GameKeepGuard), 25, "enemy realm guards", player.Group);
							break;
						}
					case "realm enemies":
						{
							if (player.Group == null)
							{
								SayTo(player, "You are not in a group!");
								break;
							}

							if (player.Group.Leader != player)
							{
								SayTo(player, "You are not the leader of your group!");
								break;
							}
							if (player.Group.Mission != null)
								player.Group.Mission.ExpireMission();
							player.Group.Mission = new KillMission(typeof(GamePlayer), 15, "enemy players", player.Group);
							break;
						}
					case "guild missions":
						{
							if (Component != null)
								break;
							if (player.Guild == null)
							{
								SayTo(player, "You have no guild!");
								return false;
							}

							if (!player.Guild.HasRank(player, Guild.eRank.OcSpeak))
							{
								SayTo(player, "You are not high enough rank in your guild!");
								return false;
							}
							//TODO: implement guild missions
							SayTo(player, "This type of mission is not yet implemented");
							SayTo(player, "Outstanding, we can always use help from organized guilds. Would you like to press the attack on the realm of [Albion] or the realm of [Hibernia].");
							break;
						}
				}
			}

			if (player.Mission != null)
				SayTo(player, player.Mission.Description);

			if (player.Group != null && player.Group.Mission != null)
				SayTo(player, player.Group.Mission.Description);

			return true;
		}

		protected override ICharacterClass GetClass()
		{
			if (ModelRealm == eRealm.Albion) return new ClassArmsman();
			else if (ModelRealm == eRealm.Midgard) return new ClassWarrior();
			else if (ModelRealm == eRealm.Hibernia) return new ClassHero();
			return new DefaultCharacterClass();
		}

		protected override void SetBlockEvadeParryChance()
		{
			base.SetBlockEvadeParryChance();

			BlockChance = 15;
			ParryChance = 15;

			if (ModelRealm != eRealm.Albion)
			{
				EvadeChance = 10;
				ParryChance = 5;
			}
		}

		protected override void SetRespawnTime()
		{
			if (Realm == eRealm.None && (GameServer.Instance.Configuration.ServerType == EGameServerType.GST_PvE ||
			GameServer.Instance.Configuration.ServerType == EGameServerType.GST_PvP))
			{
				// In PvE & PvP servers, lords are really just mobs farmed for seals.
				int iVariance = 1000 * Math.Abs(ServerProperties.Properties.GUARD_RESPAWN_VARIANCE);
				int iRespawn = 60 * ((Math.Abs(ServerProperties.Properties.GUARD_RESPAWN) * 1000) +
					(Util.Random(-iVariance, iVariance)));

				RespawnInterval = (iRespawn > 1000) ? iRespawn : 1000; // Make sure we don't end up with an impossibly low respawn interval.
			}
			else
				RespawnInterval = 10000; // 10 seconds
		}

		protected override void SetAggression()
		{
			SetAggression(99, 400);
		}

		protected override void SetName()
		{
			switch (ModelRealm)
			{
				case eRealm.None:
				case eRealm.Albion:
					Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.CaptainCommander");
					break;
				case eRealm.Midgard:
					Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.HersirCommander");
					break;
				case eRealm.Hibernia:
					Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.ChampionCommander");
					break;
			}

			if (Realm == eRealm.None)
			{
				Name = LanguageMgr.GetTranslation(Properties.SERV_LANGUAGE, "SetGuardName.Renegade", Name);
			}
		}
	}

	/*
	 * Champion Commander
	 * Captain Commander
	 * Hersir Commander
	 * 
	 * Hail and well met, PLAYERNAME! As the leader of our forces, I am calling upon our finest warriors to aid in the vanquishing of our enemies. Do you wish to do your duty in defence of our [realm]?
	 * Excellent! We all must do our part. How would you like to assist the cause? I have [personal missions] and [group missions] available.
	 * 
	 * General
	 * 
	 * Greetings, PLAYERNAME. We have put out the call far and wide for heroes such as yourself to aid us in our ongoing struggle. It warms my heart good to to see a great CLASSNAME such as yourself willing to lay their life on the line in defence of the [realm].
	 * We all must do our part. How would you like to assist the cause? I have [personal missions], [group missions], and [guild missions] available.
	 * We have several personal missions from which to choose. Would you like to claim the bounty on some [realm guards], or claim the bounties on some [enemies of the realm]? Perhaps a frontal assault isn't your style? If so, we also have missions that require you to [reconnoiter] an enemy realm, or elimate the thread of an impending [assassination]?
	 * Would your group like to help with a [tower capture], a [keep capture], or a [caravan] raid? Should those choices fail to appeal to you, I also have bounty missions on [enemy guards] and [realm enemies] if that is your preference.
	 * Outstanding, we can always use help from organized guilds. Would you like to press the attack on the realm of [Albion] or the realm of [Hibernia].
	 */
}
