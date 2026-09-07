using System;
using DOL.GS;
using DOL.GS.ServerProperties;

namespace DOL.AI.Brain
{
    public class TurretFNFBrain : TurretBrain
    {
        public override int ThinkInterval => 1000;
        protected override bool CanAddToAggroListFromMultipleLosChecks => true;

        public TurretFNFBrain(GameLiving owner) : base(owner) { }

        public override void Think()
        {
            CheckProximityAggro();

            if (!CheckSpells(eCheckSpellType.Offensive))
                CheckSpells(eCheckSpellType.Defensive);
        }

        public override bool CheckProximityAggro()
        {
            // FnF turrets need to add all players and NPCs to their aggro list to be able to switch target randomly and effectively.
            _playerAggroLosChecksThisTick = 0;
            _npcAggroLosChecksThisTick = 0;
            CheckPlayerAggro();
            CheckNpcAggro();
            return HasAggro;
        }

        protected override void CheckPlayerAggro()
        {
            // Copy paste of 'base.CheckPlayerAggro()' except we add all players in range.

            foreach (var player in BuildPlayerAggroCandidateLoop())
            {
                if (!CanAggroTarget(player))
                    continue;

                if (player.IsStealthed || player.Steed != null)
                    continue;

                if (player.effectListComponent.ContainsEffectForEffectType(eEffect.Shade))
                    continue;

                if (Properties.CHECK_LOS_BEFORE_AGGRO_FNF)
                    SendPlayerAggroLosCheck(player, player);
                else
                    AddToAggroList(player);
            }
        }

        protected override void CheckNpcAggro()
        {
            // Copy paste of 'base.CheckNPCAggro()' except we add all NPCs in range.

            foreach (var npc in BuildNpcAggroCandidateLoop())
            {
                if (!CanAggroTarget(npc))
                    continue;

                if (npc is GameTaxi or GameTrainingDummy)
                    continue;

                if (Properties.CHECK_LOS_BEFORE_AGGRO_FNF)
                {
                    if (npc.Brain is ControlledMobBrain theirControlledNpcBrain && theirControlledNpcBrain.GetPlayerOwner() is GamePlayer theirOwner)
                    {
                        SendNpcAggroLosCheck(theirOwner, npc);
                        continue;
                    }
                    else if (GetPlayerOwner() is GamePlayer ourOwner)
                    {
                        SendNpcAggroLosCheck(ourOwner, npc);
                        continue;
                    }
                }

                AddToAggroList(npc);
            }
        }

        protected override bool TrustCast(Spell spell, eCheckSpellType type, GameLiving target, bool checkLos)
        {
            // Turn towards the target we're attempting to cast on if not already casting.
            if (base.TrustCast(spell, type, target, checkLos))
            {
                if (!Body.IsCasting)
                    Body.TurnTo(target);

                return true;
            }

            return false;
        }

        protected override AggroTable BuildAggroTable()
        {
            return new(new FnfTurretThreatStrategy(this));
        }

        protected override GameLiving CalculateNextAttackTarget()
        {
            GameLiving target = CleanUpAggroListAndGetHighestModifiedThreat();
            Body.attackComponent.AttackState = target != null;
            return target;
        }

        public override void UpdatePetWindow() { }
        public override void OnAttackedByEnemy(AttackData ad) { }

        protected class FnfTurretThreatStrategy : ControlledNpcThreatStrategy
        {
            // For reminder, FnFs acquire a random target in range then perform a LoS check.
            // On Live (August 2026), they do this every second. This is the behavior we're using.
            // In 1.65, they actually attempted to cast, which delayed target acquisition.

            // Furthermore, Live (August 2026) overrides this logic in dungeon and makes them perform the LoS check first,
            // which makes them more reactive and less likely to lock onto an NPC in a different room or floor.
            // Live seems to rely on server side LoS checks for this, preventing the client from being flooded with packets.
            // This wasn't the case in ~1.65 according to a video where FnFs attempted to cast behind walls in Dodens Gruva.

            // But because our dungeons are packed with NPCs, this would make them particularly unreliable.
            // For this reason, we make FnFs prioritize NPCs by placing them in different buckets based on distance.
            // Enemy players are always placed in the first bucket so that they're always evaluated,
            // preventing an exploit where one player could force all turrets to lock onto them, remain behind a wall, and protect other players.

            // Fractions of AggroRange marking bucket boundaries (must be in ascending order).
            private static readonly double[] _distanceBucketThresholds = [0.4];

            private static int DistanceBucketCount => _distanceBucketThresholds.Length + 1;

            public FnfTurretThreatStrategy(StandardMobBrain owner) : base(owner) { }

            public override long CalculateEffectiveAggro(long baseAggro, GameLiving target, out double distance)
            {
                // Fnf turrets don't care about effective aggro, target selection is random.
                // We're repurposing it to bucket entities by distance.
                return GetDistanceBucket(target, out distance);
            }

            private int GetDistanceBucket(GameLiving target, out double distance)
            {
                distance = _owner.Body.GetDistanceTo(target);

                // Force players into the closest bucket.
                if (target is GamePlayer)
                    return 0;

                for (int i = 0; i < _distanceBucketThresholds.Length; i++)
                {
                    if (distance < _owner.AggroRange * _distanceBucketThresholds[i])
                        return i;
                }

                return _distanceBucketThresholds.Length; // Farthest bucket.
            }

            public override GameLiving SelectTarget(ReadOnlySpan<AggroTable.TargetCandidate> candidates)
            {
                if (candidates.Length == 0 ||
                    _owner.Body is not TurretPet turretPet ||
                    turretPet.Brain is not StandardMobBrain brain)
                {
                    return null;
                }

                Spell turretSpell = turretPet.TurretSpell;

                if (turretSpell == null)
                    return null;

                // Find the closest non-empty distance bucket.
                // Randomizing which candidate within a bucket gets picked
                // by starting the scan at a random offset.
                int randomIndex = Util.Random(candidates.Length - 1);
                Span<int> bestIndexByBucket = stackalloc int[DistanceBucketCount];
                bestIndexByBucket.Fill(-1);

                for (int i = 0; i < candidates.Length; i++)
                {
                    int index = (randomIndex + i) % candidates.Length;
                    int bucket = (int) candidates[index].EffectiveAggro;
                    ref int slot = ref bestIndexByBucket[bucket];

                    if (slot == -1)
                        slot = index;

                    if (bucket == 0)
                        break; // Closest possible bucket, no need to look further.
                }

                int selectedIndex = -1;

                foreach (int index in bestIndexByBucket)
                {
                    if (index != -1)
                    {
                        selectedIndex = index;
                        break;
                    }
                }

                return selectedIndex == -1 ? null : candidates[selectedIndex].Living;
            }

            public override bool ShouldBeRemoved(GameLiving target)
            {
                return base.ShouldBeRemoved(target) || !_owner.Body.IsWithinRadius(target, _owner.AggroRange);
            }
        }
    }
}
