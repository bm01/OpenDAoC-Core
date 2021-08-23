﻿using DOL.AI;
using DOL.AI.Brain;
using DOL.GS;
using DOL.GS.Movement;
using FiniteStateMachine;
using log4net;
using System;
using System.Collections.Generic;
using System.Reflection;
using static DOL.AI.Brain.StandardMobBrain;

public class StandardMobFSMState : State
{
    public StandardMobStateType ID { get { return _id; } }
    protected static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

    protected StandardMobBrain _brain = null;
    protected StandardMobStateType _id;

    public StandardMobFSMState(FSM fsm, StandardMobBrain brain) : base(fsm)
    {
        _brain = brain;
    }

    public override void Enter()
    {
        base.Enter();
    }

    public override void Exit()
    {
        base.Exit();
    }

    public override void Think()
    {
        base.Think();
    }
}

public class StandardMobFSMState_IDLE : StandardMobFSMState
{
    public StandardMobFSMState_IDLE(FSM fsm, StandardMobBrain brain) : base(fsm, brain)
    {
        _id = StandardMobStateType.IDLE;
    }

    public override void Enter()
    {
        Console.WriteLine($"{_brain.Body} is entering IDLE");
        base.Enter();
    }

    public override void Think()
    {

        //if DEAD, bail out of calc
        //if HP < 0, set state to DEAD

        //check if has patrol
        //Mob will now always walk on their path
        if (_brain.HasPatrolPath())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.PATROLLING);
        }

        if (_brain.CanRandomWalk)
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.ROAMING);
        }

        // check for returning to home if to far away
        if (_brain.IsBeyondTetherRange())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.RETURN_TO_SPAWN);
        }

        //if aggroList > 0,
        //setStatus = aggro
        if (_brain.HasAggressionTable())
        {
            _brain.Body.FireAmbientSentence(GameNPC.eAmbientTrigger.fighting, _brain.Body.TargetObject as GameLiving);
            _brain.FSM.SetCurrentState(StandardMobStateType.AGGRO);
            return;
        }

        //cast self buffs if applicable
        _brain.CheckSpells(eCheckSpellType.Defensive);

        base.Think();
    }
}

public class StandardMobFSMState_WAKING_UP : StandardMobFSMState
{
    public StandardMobFSMState_WAKING_UP(FSM fsm, StandardMobBrain brain) : base(fsm, brain)
    {
        _id = StandardMobStateType.WAKING_UP;
    }

    public override void Enter()
    {
        //Console.WriteLine($"Entering WAKEUP for {_brain}");
        base.Enter();
    }

    public override void Think()
    {
        //Console.WriteLine($"{_brain.Body} is WAKING_UP");
        //if allowed to roam,
        //set state == ROAMING
        if (!_brain.Body.AttackState && _brain.CanRandomWalk && !_brain.Body.IsRoaming)
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.ROAMING);
        }

        //if patrol path,
        //set state == PATROLLING
        if (_brain.HasPatrolPath())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.PATROLLING);
        }

        //if aggroList > 0,
        //setStatus = aggro
        if (_brain.HasAggressionTable())
        {
            _brain.Body.FireAmbientSentence(GameNPC.eAmbientTrigger.fighting, _brain.Body.TargetObject as GameLiving);
            //_brain.AttackMostWanted();
            _brain.FSM.SetCurrentState(StandardMobStateType.AGGRO);
            return;
        }

        //else,
        //set state = IDLE
        _brain.m_fsm.SetCurrentState(StandardMobStateType.IDLE);

        base.Think();
    }
}

public class StandardMobFSMState_AGGRO : StandardMobFSMState
{
    public StandardMobFSMState_AGGRO(FSM fsm, StandardMobBrain brain) : base(fsm, brain)
    {
        _id = StandardMobStateType.AGGRO;
    }

    public override void Enter()
    {
        //enable attack component
        //enable spell component
        if (_brain.Body.attackComponent == null) { _brain.Body.attackComponent = new DOL.GS.AttackComponent(_brain.Body); }
        if (_brain.Body.castingComponent == null) { _brain.Body.castingComponent = new DOL.GS.CastingComponent(_brain.Body); }
        Console.WriteLine($"{_brain.Body} is entering AGGRO");
        _brain.AttackMostWanted();
        base.Enter();
    }

    public override void Exit()
    {
        if (_brain.Body.AttackState)
            _brain.Body.StopAttack();

        _brain.Body.TargetObject = null;

        base.Exit();
    }

    public override void Think()
    {
        // check for returning to home if to far away
        if (_brain.IsBeyondTetherRange())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.RETURN_TO_SPAWN);
        }

        //if no aggro targets, set State = RETURN_TO_SPAWN
        if (!_brain.HasAggressionTable())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.RETURN_TO_SPAWN);
        } 

        if(_brain.Body.TargetObject == null)
        {
            _brain.AttackMostWanted();
        }
                
        base.Think();
    }
}

public class StandardMobFSMState_ROAMING : StandardMobFSMState
{
    public StandardMobFSMState_ROAMING(FSM fsm, StandardMobBrain brain) : base(fsm, brain)
    {
        _id = StandardMobStateType.ROAMING;
    }

    public override void Enter()
    {
        Console.WriteLine($"{_brain.Body} is entering ROAM");
        base.Enter();
    }

    public override void Think()
    {
        // check for returning to home if to far away
        if (_brain.IsBeyondTetherRange())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.RETURN_TO_SPAWN);
        }

        //if randomWalkChance,
        //find new point
        //walk to point
        if (Util.Chance(DOL.GS.ServerProperties.Properties.GAMENPC_RANDOMWALK_CHANCE))
        {
            IPoint3D target = _brain.CalcRandomWalkTarget();
            if (target != null)
            {
                if (Util.IsNearDistance(target.X, target.Y, target.Z, _brain.Body.X, _brain.Body.Y, _brain.Body.Z, GameNPC.CONST_WALKTOTOLERANCE))
                {
                    _brain.Body.TurnTo(_brain.Body.GetHeading(target));
                }
                else
                {
                    _brain.Body.WalkTo(target, 50);
                }

                _brain.Body.FireAmbientSentence(GameNPC.eAmbientTrigger.roaming);
            }
        }

        //if aggroList > 0,
        //setStatus = aggro
        if (_brain.HasAggressionTable())
        {
            _brain.Body.FireAmbientSentence(GameNPC.eAmbientTrigger.fighting, _brain.Body.TargetObject as GameLiving);
            //_brain.AttackMostWanted();
            _brain.FSM.SetCurrentState(StandardMobStateType.AGGRO);
            return;
        }

        //cast self buffs if applicable
        _brain.CheckSpells(eCheckSpellType.Defensive);

        base.Think();
    }
}

public class StandardMobFSMState_RETURN_TO_SPAWN : StandardMobFSMState
{
    public StandardMobFSMState_RETURN_TO_SPAWN(FSM fsm, StandardMobBrain brain) : base(fsm, brain)
    {
        _id = StandardMobStateType.RETURN_TO_SPAWN;
    }

    public override void Enter()
    {
        Console.WriteLine($"{_brain.Body} is entering RETURN_TO_SPAWN");
        _brain.ClearAggroList();
        base.Enter();
    }

    public override void Think()
    {
        _brain.Body.WalkToSpawn();

        if (_brain.Body.IsNearSpawn())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.WAKING_UP);
        }


        base.Think();
    }
}

public class StandardMobFSMState_PATROLLING : StandardMobFSMState
{
    public StandardMobFSMState_PATROLLING(FSM fsm, StandardMobBrain brain) : base(fsm, brain)
    {
        _id = StandardMobStateType.PATROLLING;
    }

    public override void Enter()
    {
        Console.WriteLine($"{_brain.Body} is PATROLLING");
        _brain.ClearAggroList();
        base.Enter();
    }

    public override void Think()
    {
        // check for returning to home if to far away
        if (_brain.IsBeyondTetherRange())
        {
            _brain.FSM.SetCurrentState(StandardMobStateType.RETURN_TO_SPAWN);
        }

        //if aggroList > 0,
        //setStatus = aggro
        if (_brain.HasAggressionTable())
        {
            _brain.Body.FireAmbientSentence(GameNPC.eAmbientTrigger.fighting, _brain.Body.TargetObject as GameLiving);
            _brain.FSM.SetCurrentState(StandardMobStateType.AGGRO);
            return;
        }

        //handle patrol logic
        PathPoint path = MovementMgr.LoadPath(_brain.Body.PathID);
        if (path != null)
        {
            _brain.Body.CurrentWayPoint = path;
            _brain.Body.MoveOnPath((short)path.MaxSpeed);
        }
        else
        {
            log.ErrorFormat("Path {0} not found for mob {1}.", _brain.Body.PathID, _brain.Body.Name);
            _brain.FSM.SetCurrentState(StandardMobStateType.WAKING_UP);
        }

        base.Think();
    }
}

public enum StandardMobStateType
{
    IDLE = 0,
    WAKING_UP,
    AGGRO,
    ROAMING,
    RETURN_TO_SPAWN,
    PATROLLING,
    DEAD
}