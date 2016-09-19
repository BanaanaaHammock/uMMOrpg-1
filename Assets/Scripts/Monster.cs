// The Monster class has a few different features that all aim to make behave as
// realistically as possible.
//
// - **States:** first of all, the monster has several different states like
// IDLE, ATTACKING, MOVING and DEATH. The monster will randomly move around in
// a certain movement radius and try to attack any players in its aggro range.
// _Note: monsters use NavMeshAgents to move on the NavMesh._
//
// - **Aggro:** To save computations, we let Unity take care of finding players
// in the aggro range by simply adding a AggroArea _(see AggroArea.cs)_ sphere
// to the monster's children in the Hierarchy. We then use the OnTrigger
// functions to find players that are in the aggro area. The monster will always
// move to the nearest aggro player and then attack it as long as the player is
// in the follow radius. If the player happens to walk out of the follow
// radius then the monster will walk back to the start position quickly.
//
// - **Respawning:** The monsters have a _respawn_ property that can be set to
// true in order to make the monster respawn after it died. We developed the
// respawn system with simplicity in mind, there are no extra spawner objects
// needed. As soon as a monster dies, it will make itself invisible for a while
// and then go back to the starting position to respawn. This feature allows the
// developer to quickly drag monster Prefabs into the scene and place them
// anywhere, without worrying about spawners and spawn areas.
//
// - **Loot:** Dead monsters can also generate loot, based on the _lootItems_
// list. Each monster has a list of items with their dropchance, so that loot
// will always be generated randomly. Monsters can also randomly generate loot
// gold between a minimum and a maximum amount.
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;

public class Monster : Entity {
    [Header("Health")]
    [SerializeField] int _hpMax = 1;
    public override int hpMax { get { return _hpMax; } }

    [Header("Mana")]
    [SerializeField] int _mpMax = 1;
    public override int mpMax { get { return _mpMax; } }

    [Header("Damage")]
    [SerializeField] int _damage = 2;
    public override int damage { get { return _damage; } }
    

    [Header("Defense")]
    [SerializeField] int _defense = 1;
    public override int defense { get { return _defense; } }
    
    [Header("Movement")]
    [SerializeField, Range(0, 1)] float moveProbability = 0.1f; // chance per second
    [SerializeField] float moveDist = 10.0f;
    // monsters should follow their targets even if they run out of the movement
    // radius. the follow dist should always be bigger than the biggest archer's
    // attack range, so that archers will always pull aggro, even when attacking
    // from far away.
    [SerializeField] float followDist = 20.0f;

    [Header("Attack Skill")]
    [SerializeField] float atkRange = 2.0f;
    [SerializeField] float castTime = 2.0f;
    [SyncVar] float castTimeEnd; // needed for client side animation too

    [Header("Experience Reward")]
    public long rewardExp = 10;
    public long rewardSkillExp = 2;

    [Header("Loot")]
    [HideInInspector] public int lootGold = 0;
    [SerializeField] int lootGoldMin = 0;
    [SerializeField] int lootGoldMax = 10;
    [SerializeField] ItemDropChance[] dropChances;
    public SyncListItem lootItems = new SyncListItem();
    // note: PlayerItem has a .valid property that can be used to 'delete' an
    //       item. it's better than .RemoveAt() because we won't run into index-
    //       out-of-range issues

    [Header("Respawn")]
    [SerializeField] float deathTime = 30f; // enough for animation & looting
    float deathTimeElapsed = 0.0f; // not replaceable with deatLastTime atm
    [SerializeField] bool respawn = true;
    [SerializeField] float respawnTime = 10f;

    // save the start position for random movement distance and respawning
    Vector3 start;
    
    // attack //////////////////////////////////////////////////////////////////
    public float CastTimeRemaining() {
        // we use the NetworkTime offset so that client cooldowns match server
        var serverTime = Time.time + NetworkTime.offset;

        // how much time remaining until the casttime ends?
        return serverTime >= castTimeEnd ? 0f : castTimeEnd - serverTime;
    }

    // networkbehaviour ////////////////////////////////////////////////////////
    protected override void Awake() {
        base.Awake();
        start = tr.position;
    }

    [Server]
    public override void OnStartServer() {
        // call Entity's OnStartServer
        base.OnStartServer();

        // all monsters should spawn with full health and mana
        hp = hpMax;
        mp = mpMax;
    }

    void UpdateIDLE() {
        if (isServer) {
            // event: died
            if (hp == 0) {                
                state = "DEAD";
                OnDeath();
            // event: aggro
            } else if (target != null && target.hp > 0) {                
                // still in follow dist?
                if (Vector3.Distance(start, target.tr.position) <= followDist) {
                    // in attack range?
                    if (Vector3.Distance(tr.position, target.tr.position) <= atkRange) {
                        // start casting and set the casting end time
                        castTimeEnd = Time.time + castTime;
                        state = "CASTING";
                        return;
                    } else {
                        // move to it first
                        // note: we walk into attackrange * 0.8 so that there is some
                        // space and we can still attack a bit if he steps back slightly
                        agent.stoppingDistance = atkRange * 0.8f;
                        agent.destination = target.tr.position;
                        state = "MOVING";
                        return;
                    }
                } else {
                    target = null;
                }
            // event: move somewhere randomly (probability scaled by deltaTime)
            // in 1s the probability is 10%
            // in 1/10s the probability is 10/10%
            // -> time.deltaTime is 0.06 = 1/16 of a second
            // -> we need probability / '16' or probability * 1/16 == probability * 0.06
            // => tested: with 10% it really happens about once per second.
            } else if (Random.value <= moveProbability * Time.deltaTime) {
                // walk to a random position, from 'start'
                var p = Utils.RandVec3XZ() * moveDist;
                agent.stoppingDistance = 0;
                agent.destination = start + p;
                state = "MOVING";
            }
        }
    }

    void UpdateMOVING() {
        if (isServer) {
            // event: died
            if (hp == 0) {                
                state = "DEAD";
                OnDeath();
            // event: stopped moving
            } else if (!IsMoving()) {
                state = "IDLE";
            // event: aggro? then just walk close enough to the entity
            } else if (target != null && target.hp > 0) {
                // still in follow dist?
                if (Vector3.Distance(start, target.tr.position) <= followDist) {
                    // move there
                    agent.stoppingDistance = atkRange * 0.8f;
                    agent.destination = target.tr.position;
                // otherwise go back to start
                } else {
                    agent.destination = start;
                    target = null;
                }
            }
        }
    }

    void UpdateCASTING() {
        // keep looking at the target for server & clients (only Y rotation)
        if (target) LookAtY(target.tr.position);

        // note: no need to check died event, entity.HpDecrease does that.
        // note: skill and target were validated in TryCast already
        if (isServer) {
            // event: died
            if (hp == 0) {                
                state = "DEAD";
                OnDeath();
            // event: target disappeared, dead or not in follow distance
            } else if (target == null || target.hp == 0 ||
                       Vector3.Distance(start, target.tr.position) > followDist) {
                state = "IDLE";
                target = null;
            // apply the skill after casting is finished
            } else if (CastTimeRemaining() == 0.0) {
                DealDamageAt(target, damage);                                                
                // go back to idle
                state = "IDLE";
            }
        }
    }

    void UpdateDEAD() {
        if (isServer) {
            // stop any movement, clear target
            agent.ResetPath();
            target = null;

            // hang around for a while so that animations can be played etc.
            deathTimeElapsed += Time.deltaTime;
            if (deathTimeElapsed >= deathTime) {
                deathTimeElapsed = 0.0f; // reset it for next time
                // respawn?
                if (respawn) {
                    Hide(); // hide while respawning
                    Invoke("Respawn", respawnTime);
                // or just disappear forever
                } else {
                    NetworkServer.Destroy(gameObject);
                }
            }
        }
    }

    float lastCastTimeEnd = 0f;
    [ClientCallback] // no need to do animations on the server
    void LateUpdate() {
        // pass parameters to animation state machine
        // note: speed gives a better monster animation than distance, because
        //       the monster may follow the player in small steps, which wouldnt
        //       be animated when using distance.
        anim.SetInteger("Hp", hp);
        anim.SetFloat("Speed", agent.velocity.magnitude);

        // skill cast trigger should only be fired once when starting casting
        // note: checking CASTING->IDLE->CASTING state changes wont work in
        //   clients if it happens too fast, because server might just sync the
        //   CASTING state and skip IDLE.
        if (castTimeEnd != lastCastTimeEnd) {
            anim.SetFloat("Speed", 0f); // looks better
            anim.SetTrigger("Attack");
            lastCastTimeEnd = castTimeEnd;
        }
    }

    // OnDrawGizmos only happens while the Script is not collapsed
    void OnDrawGizmos() {

        // draw the movement area (around 'start' if game running,
        // or around current position if still editing)
        var pos = Application.isPlaying ? start : transform.position;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(pos, moveDist);

        // draw the follow dist
        Gizmos.color = Color.gray;
        Gizmos.DrawWireSphere(pos, followDist);
    }

    // aggro ///////////////////////////////////////////////////////////////////
    // this function is called by the AggroArea (if any) on clients and server
    [ServerCallback]
    public override void OnAggro(Entity entity) {
        // alive? and is player? (dead players have colliders too)
        if (entity != null && entity.hp > 0 && entity is Player) {
            // no target yet(==self), or closer than current target?
            if (target == null || Vector3.Distance(tr.position, entity.tr.position) < Vector3.Distance(tr.position, target.tr.position))
                target = entity;
        }
    }

    // respawn /////////////////////////////////////////////////////////////////
    [Server]
    void Respawn() {
        // respawn at the start position with full health, visibility, no loot
        lootGold = 0;
        lootItems.Clear();
        Show();
        agent.Warp(start); // recommended over transform.position
        Revive();
        state = "IDLE";
    }

    // death ///////////////////////////////////////////////////////////////////
    [Server]
    void OnDeath() {
        // generate gold
        lootGold = Random.Range(lootGoldMin, lootGoldMax);

        // generate items (note: can't use Linq because of SyncList)
        foreach (ItemDropChance idc in dropChances)
            if (Random.value <= idc.probability)
                lootItems.Add(new Item(idc.template));
    }
}
