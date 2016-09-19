// All player logic was put into this class. We could also split it into several
// smaller components, but this would result in many GetComponent calls and a
// more complex syntax.
//
// The default Player class takes care of the basic player logic like the state
// machine and some properties like damage and defense.
//
// The Player class stores the maximum experience for each level in a simple
// array. So the maximum experience for level 1 can be found in expMax[0] and
// the maximum experience for level 2 can be found in expMax[1] and so on. The
// player's health and mana are also level dependent in most MMORPGs, hence why
// there are hpMax and mpMax arrays too. We can find out a players's max health
// in level 1 by using hpMax[0] and so on.
//
// The class also takes care of selection handling, which detects 3D world
// clicks and then targets/navigates somewhere/interacts with someone.
//
// Animations are not handled by the NetworkAnimator because it's still very
// buggy and because it can't really react to movement stops fast enough, which
// results in moonwalking. Not synchronizing animations over the network will
// also save us bandwidth. 
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

[RequireComponent(typeof(PlayerChat))]
[RequireComponent(typeof(NetworkName))]
public class Player : Entity {
    // some properties have to be stored for saving
    [HideInInspector] public string account = "";
    [HideInInspector] public string className = "";

    [Header("Health")]
    [SerializeField] int[] _hpMax = {100, 110, 120};
    public override int hpMax {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipHpBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsHpMax).Sum();

            // calculate strength bonus (1 strength means 1% of hpMax bonus)
            var attrBonus = Convert.ToInt32(_hpMax[level-1] * (strength * 0.01f));

            // return base + attribute + equip + buffs
            return _hpMax[level-1] + equipBonus + buffBonus + attrBonus;
        }
    }
    
    [Header("Mana")]
    [SerializeField] int[] _mpMax = {10, 20, 30};
    public override int mpMax {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipMpBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsMpMax).Sum();

            // calculate intelligence bonus (1 intelligence means 1% of hpMax bonus)
            var attrBonus = Convert.ToInt32(_mpMax[level-1] * (intelligence * 0.01f));
            
            // return base + attribute + equip + buffs
            return _mpMax[level-1] + equipBonus + buffBonus + attrBonus;
        }
    }

    [Header("Damage")]
    [SyncVar, SerializeField] int baseDamage = 1;
    public override int damage {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipDamageBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsDamage).Sum();
            
            // return base + equip + buffs
            return baseDamage + equipBonus + buffBonus;
        }
    }

    [Header("Defense")]
    [SyncVar, SerializeField] int baseDefense = 1;
    public override int defense {
        get {
            // calculate equipment bonus
            var equipBonus = (from item in equipment
                              where item.valid
                              select item.equipDefenseBonus).Sum();

            // calculate buff bonus
            var buffBonus = (from skill in skills
                             where skill.BuffTimeRemaining() > 0
                             select skill.buffsDefense).Sum();
            
            // return base + equip + buffs
            return baseDefense + equipBonus + buffBonus;
        }
    }

    [Header("Attributes")]
    [SyncVar, SerializeField] public int strength = 0;
    [SyncVar, SerializeField] public int intelligence = 0;

    [Header("Experience")] // note: int is not enough (can have > 2 mil. easily)
    [SyncVar, SerializeField] long _exp = 0;
    public long exp {
        get { return _exp; }
        set {
            if (value <= exp) {
                // decrease
                _exp = Utils.MaxLong(value, 0);
            } else {
                // increase with level ups
                // set the new value (which might be more than expMax)
                _exp = value;

                // now see if we leveled up (possibly more than once too)
                // (can't level up if already max level)
                while (_exp >= expMax && level < levelMax) {
                    // subtract current level's required exp, then level up
                    _exp -= expMax;
                    ++level;
                }

                // set to expMax if there is still too much exp remaining
                if (_exp > expMax) _exp = expMax;
            }
        }
    }
    [SerializeField] long[] _expMax = {10, 20, 30};
    public long expMax { get { return _expMax[level-1]; } }
    public int levelMax { get { return _expMax.Length; } }

    [Header("Skill Experience")]
    [SyncVar] public long skillExp = 0;
    
    [Header("Indicator")]
    [SerializeField] GameObject indicatorPrefab;
    GameObject indicator;

    [Header("Inventory")]
    public int inventorySize = 30;
    public SyncListItem inventory = new SyncListItem();
    public ItemTemplate[] defaultItems;

    [Header("Trash")]
    [SyncVar] public Item trash = new Item();

    [Header("Gold")] // note: int is not enough (can have > 2 mil. easily)
    [SerializeField, SyncVar] long _gold = 0;
    public long gold { get { return _gold; } set { _gold = Utils.MaxLong(value, 0); } }

    [Header("Equipment")]
    public string[] equipmentTypes = new string[]{"EquipmentWeapon", "EquipmentHead", "EquipmentChest", "EquipmentLegs", "EquipmentShield", "EquipmentShoulders", "EquipmentHands", "EquipmentFeet"};
    public SyncListItem equipment = new SyncListItem();
    public List<ItemTemplate> defaultEquipment;

    // 'skillTemplates' holds all skillTemplates and can be modified in the
    // Inspector 'skills' holds the dynamic skills that were based on the
    // skillTemplates (with cooldown, learned, etc.)
    [Header("Skills")]
    public SkillTemplate[] skillTemplates;
    public SyncListSkill skills = new SyncListSkill();
    // current and next skill (Queue can't be synced, two ints are just easier)
    [SyncVar, HideInInspector] public int skillCur = -1; // sync for animation
    [         HideInInspector] public int skillNext = -1;

    [Header("Skillbar")]
    public KeyCode[] skillbarHotkeys = new KeyCode[] {KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0};
    public string[] skillbar = new string[] {"", "", "", "", "", "", "", "", "", ""};

    [Header("Loot")]
    public float lootRange = 4f;

    // all active quests
    [Header("Quests")]
    public int questLimit = 10;
    public SyncListQuest quests = new SyncListQuest();

    [Header("Interaction")]
    public float talkRange = 4f;

    [Header("Trading")]
    [SyncVar, HideInInspector] public string tradeRequestFrom = "";
    [SyncVar, HideInInspector] public bool tradeOfferLocked = false;
    [SyncVar, HideInInspector] public bool tradeOfferAccepted = false;
    [SyncVar, HideInInspector] public long tradeOfferGold = 0;
    public SyncListInt tradeOfferItems = new SyncListInt(); // inventory indices

    [Header("Death")]
    [SerializeField] float deathExpLossPercent = 0.05f;

    // networkbehaviour ////////////////////////////////////////////////////////
    protected override void Awake() {
        // cache base components
        base.Awake();
    }

    public override void OnStartLocalPlayer() {
        // setup camera targets
        Camera.main.GetComponent<CameraMMO>().target = transform;
        GameObject.FindWithTag("MinimapCamera").GetComponent<CopyPosition>().target = transform;

        // load skillbar after player data was loaded
        print("loading skillbar for " + name);
        if (isLocalPlayer) LoadSkillbar();
    }

    public override void OnStartServer() {
        base.OnStartServer();
        // initialize trade item indices
        for (int i = 0; i < 6; ++i) tradeOfferItems.Add(-1);
    }

    void UpdateIDLE() {
        // note: no need to check died event, entity.HpDecrease does that.
        if (isServer) {
            // event: died
            if (hp == 0) {                
                state = "DEAD";
                OnDeath();
            // event: casting a skill
            } else if (0 <= skillCur && skillCur < skills.Count) {
                TryCastSkill();
            }
        }
        if (isLocalPlayer) {
            // simply accept input
            SelectionHandling();

            // canel action if escape key was pressed
            if (Input.GetKeyDown(KeyCode.Escape)) CmdCancelAction();
        }
    }

    void UpdateMOVING() {
        // note: no need to check died event, entity.HpDecrease does that.
        if (isServer) {            
            // event: died
            if (hp == 0) {                
                state = "DEAD";
                OnDeath();
            // event: finished moving do whatever we did before.
            } if (!IsMoving()) {
                state = "IDLE";
            // event: casting a skill
            } else if (0 <= skillCur && skillCur < skills.Count) {
                TryCastSkill();
            }
        }
        if (isLocalPlayer) {
            // simply accept input
            SelectionHandling();

            // canel action if escape key was pressed
            if (Input.GetKeyDown(KeyCode.Escape)) CmdCancelAction();
        }
    }

    [Server]
    void TryCastSkill() {
        // needed in IDLE and MOVE, so let's put it into one function
        var skill = skills[skillCur];

        // only if we have a weapon; don't allow fist fights for now, otherwise
        // all classes would need a fist fight animation. (there is virtually no
        // value in attacking something without a weapon)
        if (HasEquipmentWeapon()) {        
            // can the skill be casted?
            if (skills[skillCur].IsReady()) {
                // enough mana is needed in any case
                if (mp >= skill.manaCosts) {
                    // attack?
                    if (skill.category == "Attack") {
                        // only pve for now (pvp comes later)
                        if (target != null && target.hp > 0 && target is Monster) {
                            // check range
                            if (Vector3.Distance(tr.position, target.tr.position) > skill.castRange) {
                                // move to it first
                                agent.stoppingDistance = skill.castRange;
                                agent.destination = target.tr.position;
                                state = "MOVING";
                                return;
                            } else {
                                // start casting and set the casting end time
                                skill.castTimeEnd = Time.time + skill.castTime;
                                skills[skillCur] = skill;
                                state = "CASTING";
                                return;
                            }
                        }
                    // heal?
                    } else if (skill.category == "Heal") {
                        // heal another player?
                        if (target != null && target is Player && target != this) {
                            // check range
                            if (Vector3.Distance(tr.position, target.tr.position) > skill.castRange) {
                                // move to it first
                                agent.stoppingDistance = skill.castRange;
                                agent.destination = target.tr.position;
                                state = "MOVING";
                                return;
                            } else {
                                // start casting and set the casting end time
                                skill.castTimeEnd = Time.time + skill.castTime;
                                skills[skillCur] = skill;
                                state = "CASTING";
                                return;
                            }
                        // heal self in any other case (if self/nothing/monster/npc...)
                        } else {
                            // start casting and set the casting end time
                            skill.castTimeEnd = Time.time + skill.castTime;
                            skills[skillCur] = skill;
                            state = "CASTING";
                            return;
                        }
                    // buff? (only buff self for now)
                    } else if (skill.category == "Buff") {
                        // start casting and set the casting end time
                        skill.castTimeEnd = Time.time + skill.castTime;
                        skills[skillCur] = skill;
                        state = "CASTING";
                        return;
                    }
                }

                // if we get here, then something was wrong => cancel the cast
                skillCur = -1;
                state = "IDLE";
            }
            // note: do NOT cancel the cast if it wasn't ready yet. otherwise
            // cooldown skills can't be automatically repeated etc.
        } else {
            // cancel the cast
            skillCur = -1;
            state = "IDLE";

            // send an info chat message
            var msg = new ChatInfoMsg();
            msg.text = "Can't cast a skill without a weapon.";
            netIdentity.connectionToClient.Send(ChatInfoMsg.MsgId, msg);
        }
    }

    void UpdateCASTING() {
        // keep looking at the target for server & clients (only Y rotation)
        if (target) LookAtY(target.tr.position);

        // note: no need to check died event, entity.HpDecrease does that.
        if (isServer) {
            // note: no general target check here because it depends on category
            // event: died
            if (hp == 0) {                
                state = "DEAD";
                OnDeath();
            // event: skill cast canceled
            } else if (skillCur == -1) {
                state = "IDLE";
            // apply the skill after casting is finished
            } else if (skills[skillCur].CastTimeRemaining() == 0.0) {
                var skill = skills[skillCur];

                // only if we have a weapon; don't allow fist fights for now, otherwise
                // all classes would need a fist fight animation. (there is virtually no
                // value in attacking something without a weapon)
                if (HasEquipmentWeapon()) {     
                    // attack?
                    if (skill.category == "Attack") {
                        // targeting a monster that is alive?
                        if (target is Monster && target.hp > 0) {
                            // decrease mana in any case
                            mp -= skill.manaCosts;

                            // deal damage directly or shoot a projectile?
                            if (skill.projectile == null) {
                                // deal damage directly
                                DealDamageAt(target, damage + skill.damage);
                            } else {
                                // spawn the projectile and shoot it towards target
                                // -> make sure that the weapon prefab has a
                                //    ProjectileMount somewhere in the hierarchy
                                var pos = tr.FindRecursively("ProjectileMount").position;
                                var go = (GameObject)Instantiate(skill.projectile.gameObject, pos, Quaternion.identity);
                                var proj = go.GetComponent<Projectile>();
                                proj.target = target;
                                proj.caster = this;
                                proj.damage = damage + skill.damage;
                                NetworkServer.Spawn(go);
                            }
                        } else {
                            // otherwise this target is invalid and we are done
                            state = "IDLE";
                        }
                    // heal?
                    } else if (skill.category == "Heal") {
                        // usability improvement: user should heal himself in he has
                        // no target or if the target is not a player
                        // (and that without actually switching targets)
                        var castTarget = target;
                        if (target == null || !(target is Player)) castTarget = this;
                        
                        castTarget.hp += skill.healsHp;
                        castTarget.mp += skill.healsMp;
                        mp -= skill.manaCosts;
                        print("healed " + castTarget.name);
                    // buff? (always buff self for now)
                    } else if (skill.category == "Buff") {
                        // (buff increasements are calculated into Damage() etc.
                        //  functions automatically)
                        // set the buff end time
                        skill.buffTimeEnd = Time.time + skill.buffTime;
                        mp -= skill.manaCosts;
                        print("buffed self");
                    }

                    // start the cooldown (and save it in the struct)
                    skill.cooldownEnd = Time.time + skill.cooldown;

                    // save any skill modifications in any case
                    skills[skillCur] = skill;

                    // casting finished. any next skill?
                    if (skillNext != -1) {
                        skillCur = skillNext;
                        skillNext = -1;
                    // otherwise clear it (unless it automatically repeats)
                    } else if (!skill.autorepeat) {
                        skillCur = -1;
                    }
                }
                
                // go back to idle
                state = "IDLE";
            }
        }
        if (isLocalPlayer) {            
            // simply accept input
            SelectionHandling();

            // canel action if escape key was pressed
            if (Input.GetKeyDown(KeyCode.Escape)) CmdCancelAction();
        }
    }

    void UpdateTRADING() {        
        if (isServer) {            
            // event: died
            if (hp == 0) {                
                state = "DEAD";
                OnDeath();
            // event: target disconnected. then stop trading
            } else if (target == null) {
                TradeClear();
            }
        }
        if (isLocalPlayer) {

        }
    }

    void UpdateDEAD() {        
        if (isServer) {
            // stop any movement, clear target
            agent.ResetPath();
            target = null;

            // event: revived
            if (hp > 0) {
                state = "IDLE";
            }
        }
        if (isLocalPlayer) {

        }
    }

    float lastCastTimeEnd = 0f;
    //[ClientCallback] // no need to do animations on the server
    void LateUpdate() {
        if (isClient) {
            // pass parameters to animation state machine
            // note: distance gives a more exact player animation than speed.
            anim.SetInteger("Hp", hp);
            anim.SetFloat("RemainingDistance", agent.remainingDistance - agent.stoppingDistance);
            
            // skill cast trigger should only be fired once when starting casting
            // note: checking CASTING->IDLE->CASTING state changes wont work in
            //   clients if it happens too fast, because server might just sync the
            //   CASTING state and skip IDLE.
            if (0 <= skillCur && skillCur < skills.Count) {
                var skill = skills[skillCur];
                if (skill.castTimeEnd != lastCastTimeEnd) {
                    if (skill.animation != "") anim.SetTrigger(skill.animation);
                    lastCastTimeEnd = skill.castTimeEnd;
                }
            }
        }

        // we have to update 3D equipment models all the time because the
        // SyncListStruct callbacks are buggy again in the latest UNET version:
        // http://forum.unity3d.com/threads/bug-old-synclist-callback-bug-was-reintroduced-to-5-3-3f1.388637/
        // we will use a callback again as soon as the bug was fixed by Unity.
        // note: this has to happen on server too, to know the equipped weapon
        for (int i = 0; i < equipment.Count; ++i)
            RefreshLocations(equipmentTypes[i], equipment[i]);
    }

    void OnDestroy() {
        // note: this function isn't called if it has a [ClientCallback] tag,
        // so let's use isLocalPlayer etc.
        // note: trying to do this in OnNetworkDestroy doesn't work well
        if (isLocalPlayer) {
            Destroy(indicator);
            SaveSkillbar();
        }
    }

    // attributes //////////////////////////////////////////////////////////////
    public int AttributesSpendable() {
        // calculate the amount of attribute points that can still be spent
        // -> one point per level
        // -> we don't need to store the points in an extra variable, we can
        //    simply decrease the attribute points spent from the level
        return level - (strength + intelligence);
    }

    [Command]
    public void CmdIncreaseStrength() {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            AttributesSpendable() > 0)
            ++strength;
    }

    [Command]
    public void CmdIncreaseIntelligence() {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            AttributesSpendable() > 0)
            ++intelligence;
    }

    // combat //////////////////////////////////////////////////////////////////
    // custom DealDamageAt function that also rewards experience if we killed
    // the monster
    [Server]
    public override void DealDamageAt(Entity entity, int n) {
        // deal damage with the default function
        base.DealDamageAt(target, n);

        // did we attack a monster?
        if (entity is Monster) {
            // did we kill it?
            if (entity.hp == 0) {
                // gain experience reward
                var rewardExp = ((Monster)entity).rewardExp;
                var balancedExp = BalanceExpReward(rewardExp, level, entity.level);
                exp += balancedExp;

                // gain skill experience reward
                var rewardSkillExp = ((Monster)entity).rewardSkillExp;
                skillExp += BalanceExpReward(rewardSkillExp, level, entity.level);
                
                // increase quest kill counters
                IncreaseQuestKillCounterFor(entity.name);
            } else {
                // it's still alive, let's make sure to pull aggro in any case
                // so that archers are still attacked if they are outside of the
                // aggro range
                ((Monster)entity).OnAggro(this);
            }
        }
    }

    // experience //////////////////////////////////////////////////////////////
    public float ExpPercent() {
        return (exp != 0 && expMax != 0) ? (float)exp / (float)expMax : 0.0f;
    }
    
    // players gain exp depending on their level. if a player has a lower level
    // than the monster, then he gains more exp (up to 100% more) and if he has
    // a higher level, then he gains less exp (up to 100% less)
    // -> test with monster level 20 and expreward of 100:
    //   BalanceExpReward( 1, 20, 100)); => 200
    //   BalanceExpReward( 9, 20, 100)); => 200
    //   BalanceExpReward(10, 20, 100)); => 200
    //   BalanceExpReward(11, 20, 100)); => 190
    //   BalanceExpReward(12, 20, 100)); => 180
    //   BalanceExpReward(13, 20, 100)); => 170
    //   BalanceExpReward(14, 20, 100)); => 160
    //   BalanceExpReward(15, 20, 100)); => 150
    //   BalanceExpReward(16, 20, 100)); => 140
    //   BalanceExpReward(17, 20, 100)); => 130
    //   BalanceExpReward(18, 20, 100)); => 120
    //   BalanceExpReward(19, 20, 100)); => 110
    //   BalanceExpReward(20, 20, 100)); => 100
    //   BalanceExpReward(21, 20, 100)); =>  90
    //   BalanceExpReward(22, 20, 100)); =>  80
    //   BalanceExpReward(23, 20, 100)); =>  70
    //   BalanceExpReward(24, 20, 100)); =>  60
    //   BalanceExpReward(25, 20, 100)); =>  50
    //   BalanceExpReward(26, 20, 100)); =>  40
    //   BalanceExpReward(27, 20, 100)); =>  30
    //   BalanceExpReward(28, 20, 100)); =>  20
    //   BalanceExpReward(29, 20, 100)); =>  10
    //   BalanceExpReward(30, 20, 100)); =>   0
    //   BalanceExpReward(31, 20, 100)); =>   0
    public static long BalanceExpReward(long reward, int attackerLevel, int victimLevel) {
        var levelDiff = Mathf.Clamp(victimLevel - attackerLevel, -10, 10);
        var multiplier = 1f + levelDiff*0.1f;
        return Convert.ToInt64(reward * multiplier);
    }

    // death ///////////////////////////////////////////////////////////////////
    [Server]
    void OnDeath() {
        // lose experience
        var loss = Convert.ToInt64(expMax * deathExpLossPercent);
        exp -= loss;

        // send an info chat message
        var msg = new ChatInfoMsg();
        msg.text = "You died and lost " + loss + " experience.";
        netIdentity.connectionToClient.Send(ChatInfoMsg.MsgId, msg);
    }

    [Command]
    public void CmdRespawn() {
        // validate
        if (state == "DEAD") {
            // cancel any previous action
            CancelAction();

            // find closest spawn point and go there
            var start = NetworkManager.singleton.GetStartPosition();
            agent.Warp(start.position); // recommended over transform.position

            // restore health
            Revive();
        }
    }

    // loot ////////////////////////////////////////////////////////////////////
    [Command]
    public void CmdTakeLootGold() {
        // validate: dead monster and close enough?
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            target != null && target is Monster && target.hp == 0 &&
            Vector3.Distance(tr.position, target.transform.position) <= lootRange)
        {
            // take it
            gold += ((Monster)target).lootGold;
            ((Monster)target).lootGold = 0;
        }
    }

    [Command]
    public void CmdTakeLootItem(int index) {
        // validate: dead monster and close enough and valid loot index?
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            target != null && target is Monster && target.hp == 0 &&
            Vector3.Distance(tr.position, target.tr.position) <= lootRange &&
            0 <= index && index < ((Monster)target).lootItems.Count)
        {
            // find a free inventory slot
            var monster = (Monster)target;
            var freeIdx = inventory.FindIndex(item => !item.valid);
            if (freeIdx != -1) {
                // take it
                var item = monster.lootItems[index];
                inventory[freeIdx] = item;

                // clear it
                // note: Item has a .valid property that can be used to 'delete' an
                //       item. it's better than .RemoveAt() because we won't run into index-
                //       out-of-range issues
                item.valid = false;
                monster.lootItems[index] = item;
            }
        }
    }

    // inventory ///////////////////////////////////////////////////////////////
    [Command]
    public void CmdSwapInventoryTrash(int inventoryIndex) {
        // dragging an inventory item to the trash always overwrites the trash
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count) {
            // inventory slot has to be valid and destroyable
            if (inventory[inventoryIndex].valid && inventory[inventoryIndex].destroyable) {
                // overwrite trash
                trash = inventory[inventoryIndex];
                // clear inventory slot
                var temp = inventory[inventoryIndex];
                temp.valid = false;
                inventory[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdSwapTrashInventory(int inventoryIndex) {
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count) {
            // inventory slot has to be empty or destroyable
            if (!inventory[inventoryIndex].valid || inventory[inventoryIndex].destroyable) {
                // swap them
                var temp = inventory[inventoryIndex];
                inventory[inventoryIndex] = trash;
                trash = temp;
            }
        }
    }

    public int GetInventoryIndexByName(string itemName) {
        return inventory.FindIndex(item => item.valid && item.name == itemName);
    }

    public int InventorySlotsFree() {
        return inventory.Where(item => !item.valid).Count();
    }

    [Command]
    public void CmdSwapInventoryInventory(int fromIndex, int toIndex) {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // swap them
            var temp = inventory[fromIndex];
            inventory[fromIndex] = inventory[toIndex];
            inventory[toIndex] = temp;
        }
    }

    [Command]
    public void CmdInventorySplit(int fromIndex, int toIndex) {
        // note: should never send a command with complex types!
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // slotFrom has to have an entry, slotTo has to be empty
            if (inventory[fromIndex].valid && !inventory[toIndex].valid) {
                // from entry needs at least amount of 2
                if (inventory[fromIndex].amount >= 2) {
                    // split them serversided (has to work for even and odd)
                    var itemFrom = inventory[fromIndex];
                    var itemTo = inventory[fromIndex]; // copy the value
                    //inventory[toIndex] = inventory[fromIndex]; // copy value type
                    itemTo.amount = itemFrom.amount / 2;
                    itemFrom.amount -= itemTo.amount; // works for odd too

                    // put back into the list
                    inventory[fromIndex] = itemFrom;
                    inventory[toIndex] = itemTo;
                }
            }
        }
    }

    [Command]
    public void CmdInventoryMerge(int fromIndex, int toIndex) {
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= fromIndex && fromIndex < inventory.Count &&
            0 <= toIndex && toIndex < inventory.Count &&
            fromIndex != toIndex) {
            // both items have to be valid
            if (inventory[fromIndex].valid && inventory[toIndex].valid) {
                // make sure that items are the same type
                if (inventory[fromIndex].name == inventory[toIndex].name) {
                    // merge from -> to
                    var itemFrom = inventory[fromIndex];
                    var itemTo = inventory[toIndex];
                    var stack = Mathf.Min(itemFrom.amount + itemTo.amount, itemTo.maxStack);
                    var put = stack - itemFrom.amount;
                    itemFrom.amount = itemTo.amount - put;
                    itemTo.amount = stack;
                    // 'from' empty now? then clear it
                    if (itemFrom.amount == 0) itemFrom.valid = false;
                    // put back into the list
                    inventory[fromIndex] = itemFrom;
                    inventory[toIndex] = itemTo;
                }
            }
        }
    }

    [Command]
    public void CmdUseInventoryItem(int index) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= index && index < inventory.Count && inventory[index].valid) {
            // what we have to do depends on the category
            //print("use item:" + index);
            var item = inventory[index];
            if (item.category.StartsWith("Potion")) {
                // use
                hp += item.usageHp;
                mp += item.usageMp;
                exp += item.usageExp;

                // decrease amount or destroy
                if (item.usageDestroy) {
                    --item.amount;
                    if (item.amount == 0) item.valid = false;
                    inventory[index] = item; // put new values in there
                }
            } else if (item.category.StartsWith("Equipment")) {
                // for each slot: find out if equipable and then do so
                for (int i = 0; i < equipment.Count; ++i)
                    if (CanEquip(equipmentTypes[i], item))
                        SwapInventoryEquip(index, i);
            }
        }
    }

    // equipment ///////////////////////////////////////////////////////////////
    public int GetEquipmentIndexByName(string itemName) {
        return equipment.FindIndex(item => item.valid && item.name == itemName);
    }

    [Server]
    public bool CanEquip(string slotType, Item item) {
        // note: we use StartsWith because a sword could also have the type
        //       EquipmentWeaponSpecial or whatever, which is fine too
        // note: empty slot types shouldn't be able to equip anything
        return slotType != "" && item.category.StartsWith(slotType) && level >= item.minLevel;
    }

    bool HasEquipmentWeapon() {
        // equipped any 'EquipmentWeapon...' item?
        return equipment.FindIndex(item => item.valid && item.category.StartsWith("EquipmentWeapon")) != -1;
    }

    void OnEquipmentChanged(SyncListItem.Operation op, int index) {
        // update the model for server and clients
        RefreshLocations(equipmentTypes[index], equipment[index]);
    }

    void RefreshLocation(Transform loc, Item item) {
        // clear previous one in any case (when overwriting or clearing)
        if (loc.childCount > 0) Destroy(loc.GetChild(0).gameObject);

        // valid item? and has a model? then set it
        if (item.valid && item.model != null) {                    
            // load the resource
            var g = (GameObject)Instantiate(item.model);
            g.transform.SetParent(loc, false);
        }
    }

    void RefreshLocations(string category, Item item) {
        // find the locations with that category and refresh them
        var locations = GetComponentsInChildren<PlayerEquipmentLocation>().Where(loc => loc.acceptedCategory == category).ToList();
        locations.ForEach(loc => RefreshLocation(loc.transform, item));
    }

    public void SwapInventoryEquip(int inventoryIndex, int equipIndex) {
        // validate: make sure that the slots actually exist in the inventory
        // and that they are not equal
        if (hp > 0 &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count &&
            0 <= equipIndex && equipIndex < equipment.Count) {
            // slotInv has to be empty or equipable
            if (!inventory[inventoryIndex].valid || CanEquip(equipmentTypes[equipIndex], inventory[inventoryIndex])) {
                // swap them
                var temp = equipment[equipIndex];
                equipment[equipIndex] = inventory[inventoryIndex];
                inventory[inventoryIndex] = temp;
            }
        }
    }

    [Command]
    public void CmdSwapInventoryEquip(int inventoryIndex, int equipIndex) {
        // SwapInventoryEquip sometimes needs to be called by the server and
        // sometimes as a Command by clients, but calling a Command from the
        // Server causes a UNET error, so we need it once as a normal function
        // and once as a Command.
        SwapInventoryEquip(inventoryIndex, equipIndex);
    }

    // skills //////////////////////////////////////////////////////////////////
    public int GetLearnedSkillIndexByName(string skillName) {
        return skills.FindIndex(skill => skill.learned && skill.name == skillName);
    }

    [Command]
    public void CmdUseSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            // can the skill be casted?
            if (skills[skillIndex].learned && skills[skillIndex].IsReady()) {
                // add as current or next skill, unless casting same one already
                // (some players might hammer the key multiple times, which
                //  doesn't mean that they want to cast it afterwards again)
                // => also: always set skillCur when moving or idle or whatever
                //  so that the last skill that the player tried to cast while
                //  moving is the first skill that will be casted when attacking
                //  the enemy.
                if (skillCur == -1 || state != "CASTING")
                    skillCur = skillIndex;
                else if (skillCur != skillIndex)
                    skillNext = skillIndex;
            }
        }
    }

    [Command]
    public void CmdLearnSkill(int skillIndex) {
        // validate
        if ((state == "IDLE" || state == "MOVING" || state == "CASTING") &&
            0 <= skillIndex && skillIndex < skills.Count) {
            var skill = skills[skillIndex];

            // not learned already? enough skill exp, required level?
            if (!skill.learned &&
                level >= skill.learnLevel &&
                skillExp >= skill.learnSkillExp) {
                // decrease skill experience
                skillExp -= skill.learnSkillExp;

                // set as learned
                skill.learned = true;
                skills[skillIndex] = skill;
            }
        }
    }
    
    // skillbar ////////////////////////////////////////////////////////////////
    [Client]
    void SaveSkillbar() {
        // save skillbar to player prefs (based on player name, so that
        // each character can have a different skillbar)
        for (int i = 0; i < skillbar.Length; ++i)
            PlayerPrefs.SetString(name + "_skillbar_" + i, skillbar[i]);

        // force saving playerprefs, otherwise they aren't saved for some reason
        PlayerPrefs.Save();
    }

    [Client]
    void LoadSkillbar() {
        // we simply load the entries. if a user modified it to something invalid
        // then it will be sorted out in the OnGUI check automatically
        for (int i = 0; i < skillbar.Length; ++i)
            skillbar[i] = PlayerPrefs.GetString(name + "_skillbar_" + i, "");
    }

    // quests //////////////////////////////////////////////////////////////////
    public int GetQuestIndexByName(string questName) {
        return quests.FindIndex(quest => quest.name == questName);
    }

    [Server]
    public void IncreaseQuestKillCounterFor(string monsterName) {
        for (int i = 0; i < quests.Count; ++i) {
            if (quests[i].killName == monsterName) {
                var quest = quests[i];
                quest.killed = Mathf.Min(quest.killed + 1, quest.killAmount);
                quests[i] = quest;
            }
        }
    }

    [Command]
    public void CmdAcceptQuest() {
        // validate
        if (state == "IDLE" &&
            quests.Count < questLimit &&
            target != null &&
            target.hp > 0 &&
            target is Npc &&
            ((Npc)target).quest != null &&
            Vector3.Distance(tr.position, target.tr.position) <= talkRange)
        {
            var npc = (Npc)target;

            // player doesn't have that quest already? and has required level?
            if (level >= npc.quest.level && GetQuestIndexByName(npc.quest.name) == -1)
                quests.Add(new Quest(npc.quest));
        }
    }

    [Command]
    public void CmdCompleteQuest() {
        // validate
        if (state == "IDLE" &&
            target != null &&
            target.hp > 0 &&
            target is Npc &&
            ((Npc)target).quest != null &&
            Vector3.Distance(tr.position, target.tr.position) <= talkRange)
        {
            var npc = (Npc)target;

            // does the player have that quest?
            var idx = GetQuestIndexByName(npc.quest.name);
            if (idx != -1) {
                // is it finished?
                if (quests[idx].IsFinished()) {
                    // gain rewards
                    gold += quests[idx].rewardGold;
                    exp += quests[idx].rewardExp;

                    // remove quest
                    quests.RemoveAt(idx);
                }
            }
        }
    }

    // npc trading /////////////////////////////////////////////////////////////
    [Command]
    public void CmdNpcBuyItem(int index, int amount) {
        // validate: close enough, npc alive and valid index?
        if (state == "IDLE" &&
            target != null &&
            target.hp > 0 &&
            target is Npc &&
            Vector3.Distance(tr.position, target.tr.position) <= talkRange &&
            0 <= index && index < ((Npc)target).saleItems.Length)
        {
            var npc = (Npc)target;

            // valid amount?
            if (1 <= amount && amount <= npc.saleItems[index].maxStack) {
                var price = npc.saleItems[index].buyPrice * amount;

                // enough gold?
                if (gold >= price) {
                    // find free inventory slot
                    var freeIdx = inventory.FindIndex(item => !item.valid);
                    if (freeIdx != -1) {
                        // buy it
                        gold -= price;
                        var item = new Item(npc.saleItems[index], amount);
                        inventory[freeIdx] = item;
                    }
                }
            }
        }
    }

    [Command]
    public void CmdNpcSellItem(int index, int amount) {
        // validate: close enough, npc alive and valid index and valid item?
        if (state == "IDLE" &&
            target != null && 
            target.hp > 0 &&
            target is Npc &&
            Vector3.Distance(tr.position, target.tr.position) <= talkRange &&
            0 <= index && index < inventory.Count &&
            inventory[index].valid &&
            inventory[index].sellable)
        {
            var item = inventory[index];

            // valid amount?
            if (1 <= amount && amount <= item.amount) {
                // sell the amount
                var price = item.sellPrice * amount;
                gold += price;
                item.amount -= amount;
                if (item.amount == 0) item.valid = false;
                inventory[index] = item;
            }
        }
    }

    // player to player trading ////////////////////////////////////////////////
    public bool CanStartTrade() {
        // a player can only trade if he is not trading already, and not dead
        // etc.
        return state == "IDLE" || state == "MOVING" || state == "CASTING";
    }

    public bool CanStartTradeWithTarget() {
        // only if the player himself can trade,
        // if the target can trade
        // and if they are close enough together
        return target != null && target is Player && target != this &&
               CanStartTrade() && ((Player)target).CanStartTrade() &&
               Vector3.Distance(tr.position, target.tr.position) <= talkRange;
    }

    // request a trade with the target player
    [Command]
    public void CmdTradeRequest() {
        // validate
        if (CanStartTradeWithTarget()) {
            // send trade request to target
            ((Player)target).tradeRequestFrom = name;
            print(target.name + "'s trade request from " + ((Player)target).tradeRequestFrom);
        }
    }

    GameObject FindObserverWithName(string observerName) {
        foreach (var conn in netIdentity.observers)
            if (conn.playerControllers.Count > 0 && conn.playerControllers[0].gameObject.name == observerName)
                return conn.playerControllers[0].gameObject;
        return null;
    }

    [Command]
    public void CmdTradeRequestAccept() {
        // validate
        if (CanStartTrade() &&
            tradeRequestFrom != "") {
            // find the player with that name (searching observers is enough)
            var go = FindObserverWithName(tradeRequestFrom);
            if (go) {
                var other = go.GetComponent<Player>();

                // can the other guy trade right now? and check the distance
                if (other.CanStartTrade() &&
                    Vector3.Distance(tr.position, other.tr.position) <= talkRange) {
                    print(name + " started a trade with: " + other.name);
                    // set target in both of them
                    // (requester may have already cleared the target etc.)
                    target = other;
                    other.target = this;

                    // stop both of them from moving
                    agent.ResetPath();
                    other.agent.ResetPath();

                    // set trading state in both of them
                    state = "TRADING";
                    other.state = "TRADING";
                }
            }
            
            // clear the request in any case
            tradeRequestFrom = "";
        }
    }

    [Command]
    public void CmdTradeRequestDecline() {
        // validate
        if (state == "IDLE" || state == "MOVING" || state == "CASTING" || state == "TRADING")
            tradeRequestFrom = "";
    }

    [Server]
    void TradeClear() {
        // clear all trade relate properties
        tradeOfferGold = 0;
        for (int i = 0; i < tradeOfferItems.Count; ++i) tradeOfferItems[i] = -1;
        tradeOfferLocked = false;        
        tradeOfferAccepted = false;
        state = "IDLE";
    }

    [Command]
    public void CmdTradeCancel() {
        // validate
        if (state == "TRADING") {
            // clear trade for both of them
            TradeClear();
            if (target != null && target is Player) ((Player)target).TradeClear();
        }
    }

    [Command]
    public void CmdTradeOfferLock() {
        // validate
        if (state == "TRADING" && !tradeOfferLocked)
            tradeOfferLocked = true;
    }

    [Command]
    public void CmdTradeOfferGold(long n) {
        // validate
        if (state == "TRADING" && !tradeOfferLocked &&
            0 <= n && n <= gold)
            tradeOfferGold = n;
    }

    [Command]
    public void CmdTradeOfferItem(int inventoryIndex, int offerIndex) {
        // validate
        if (state == "TRADING" && !tradeOfferLocked &&
            0 <= inventoryIndex && inventoryIndex < inventory.Count &&
            inventory[inventoryIndex].valid &&
            0 <= offerIndex && offerIndex < tradeOfferItems.Count &&
            !tradeOfferItems.Contains(inventoryIndex)) // only one reference
            tradeOfferItems[offerIndex] = inventoryIndex;
    }

    [Command]
    public void CmdTradeOfferItemClear(int offerIndex) {
        // validate
        if (state == "TRADING" && !tradeOfferLocked &&
            0 <= offerIndex && offerIndex < tradeOfferItems.Count)
            tradeOfferItems[offerIndex] = -1;
    }

    [Server]
    bool IsTradeOfferStillValid() {
        // not enough gold anymore?
        if (tradeOfferGold > gold) return false;

        // still has the items?
        for (int i = 0; i < tradeOfferItems.Count; ++i) {
            var idx = tradeOfferItems[i];
            if (0 <= idx && idx < inventory.Count) {
                // item not valid anymore?
                if (!inventory[idx].valid)
                    return false;
            }
        }

        // otherwise everything is fine
        return true;
    }

    [Server]
    int TradeOfferItemSlotAmount() {
        return tradeOfferItems.Where(i => i != -1).Count();
    }

    [Server]
    int InventorySlotsNeededForTrade() {
        // if other guy offers 2 items and we offer 1 item then we only need
        // 2-1 = 1 slots. and the other guy would need 1-2 slots and at least 0.
        if (target != null && target is Player) {
            var other = (Player)target;

            var amountMy = TradeOfferItemSlotAmount();
            var amountOther = other.TradeOfferItemSlotAmount();

            return Mathf.Max(amountOther - amountMy, 0);
        }
        return 0;
    }

    [Command]
    public void CmdTradeOfferAccept() {
        // validate
        if (state == "TRADING" && tradeOfferLocked &&
            target != null && target is Player &&
            Vector3.Distance(tr.position, target.tr.position) <= talkRange) {
            // other guy locked the offer too?
            var other = (Player)target;
            if (other.tradeOfferLocked) {
                // are we the first one to accept?
                if (!other.tradeOfferAccepted) {
                    // then simply accept and wait for the other guy
                    tradeOfferAccepted = true;
                    print("first accept by " + name);
                // otherwise both have accepted now, so start the trade
                } else {                        
                    // accept
                    tradeOfferAccepted = true;
                    print("second accept by " + name);

                    // both offers still valid?
                    if (IsTradeOfferStillValid() && other.IsTradeOfferStillValid()) {
                        // both have enough inventory slots?
                        if (InventorySlotsFree() >= InventorySlotsNeededForTrade() &&
                            other.InventorySlotsFree() >= other.InventorySlotsNeededForTrade()) {
                            // exchange the items by first taking them out
                            // into a temporary list and then putting them
                            // in. this guarantees that exchanging even
                            // works with full inventories
                            
                            // take them out
                            var tempMy = new Queue<Item>();
                            for (int i = 0; i < tradeOfferItems.Count; ++i) {
                                var idx = tradeOfferItems[i];
                                if (idx != -1) {
                                    tempMy.Enqueue(inventory[idx]);
                                    var item = inventory[idx];
                                    item.valid = false;
                                    inventory[idx] = item;
                                }
                            }

                            var tempOther = new Queue<Item>();
                            for (int i = 0; i < other.tradeOfferItems.Count; ++i) {
                                var idx = other.tradeOfferItems[i];
                                if (idx != -1) {
                                    tempOther.Enqueue(other.inventory[idx]);
                                    var item = other.inventory[idx];
                                    item.valid = false;
                                    other.inventory[idx] = item;
                                }
                            }

                            // put them into the free slots
                            for (int i = 0; i < inventory.Count; ++i)
                                if (!inventory[i].valid && tempOther.Count > 0)
                                    inventory[i] = tempOther.Dequeue();
                            
                            for (int i = 0; i < other.inventory.Count; ++i)
                                if (!other.inventory[i].valid && tempMy.Count > 0)
                                    other.inventory[i] = tempMy.Dequeue();

                            // did it all work?
                            if (tempMy.Count > 0 || tempOther.Count > 0)
                                Debug.LogWarning("item trade problem");

                            // exchange the gold
                            gold -= tradeOfferGold;
                            other.gold -= other.tradeOfferGold;

                            gold += other.tradeOfferGold;
                            other.gold += tradeOfferGold;
                        }
                    } else {
                        print("trade canceled because offer is not valid anymore");
                    }

                    // in any case, stop the trade
                    TradeClear();
                    other.TradeClear();
                }
            }
        }
    }

    // selection handling //////////////////////////////////////////////////////
    void SetIndicatorViaParent(Transform parent) {
        if (!indicator) indicator = Instantiate(indicatorPrefab);
        indicator.transform.SetParent(parent, true);
        indicator.transform.position = parent.position + Vector3.up * 0.01f;
        indicator.transform.up = Vector3.up;
    }

    void SetIndicatorViaPosition(Vector3 pos, Vector3 normal) {
        if (!indicator) indicator = Instantiate(indicatorPrefab);
        indicator.transform.parent = null;
        indicator.transform.position = pos + Vector3.up * 0.01f;
        indicator.transform.up = normal; // adjust to terrain normal
    }

    [Command]
    void CmdNavigateTo(Vector3 pos, float stoppingDistance) {
        // validate
        if (state == "IDLE" || state == "MOVING" || state == "CASTING") {
            agent.stoppingDistance = stoppingDistance;
            agent.destination = pos;
            state = "MOVING";
            // cancel casting
            skillCur = -1;
        }
    }

    [Command]
    void CmdSetTargetId(NetworkIdentity ni) {
        // validate
        if (state == "IDLE" || state == "MOVING" || state == "CASTING") {
            if (ni != null) target = ni.GetComponent<Entity>();
        }
    }

    // CmdCancelAction has to be called from the Server too
    [Server]
    void CancelAction() {
        // validate
        if (state == "IDLE" || state == "MOVING" || state == "CASTING") {
            // are we casting or moving? then just stop either action, but don't
            // clear the target yet, because the player probably just wants to
            // cast another skill. he will press cancel again to clear it.
            if (state == "CASTING" || state == "MOVING") {
                skillCur = -1;
                agent.ResetPath();
                state = "IDLE";
            // otherwise cancel the target (if any)
            } else {
                target = null;
            }
        }
    }

    [Command]
    public void CmdCancelAction() {
        CancelAction();
    }

    [Client]
    void SelectionHandling() {
        // click raycasting only if not over a UI element
        // note: this only works if the UI's CanvasGroup blocks Raycasts
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject()) {
            var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit)) {
                // valid target?
                var entity = hit.transform.GetComponent<Entity>();
                if (entity) {
                    // set indicator
                    SetIndicatorViaParent(hit.transform);

                    // clicked last target again? and is not self?
                    if (entity != this && entity == target) {
                        // what is it?
                        if (entity is Monster) {
                            // dead or alive?
                            if (entity.hp > 0) {
                                // cast the first skill (if any, and if ready)
                                if (skills.Count > 0 && skills[0].IsReady())
                                    CmdUseSkill(0);
                                // otherwise walk there if still on cooldown etc
                                else
                                    CmdNavigateTo(entity.tr.position, skills.Count > 0 ? skills[0].castRange : 0f);
                            } else {
                                // has loot? and close enough?
                                if (((Monster)entity).lootItems.Count > 0 &&
                                    Vector3.Distance(tr.position, entity.tr.position) <= lootRange)
                                    FindObjectOfType<UIRefresh>().ShowLoot();
                                // otherwise walk there
                                else
                                    CmdNavigateTo(entity.tr.position, lootRange);
                            }
                        } else if (entity is Player) {
                            // cast the first skill (if any)
                            if (skills.Count > 0) CmdUseSkill(0);
                        } else if (entity is Npc) {
                            // close enough to talk?
                            if (Vector3.Distance(tr.position, entity.tr.position) <= talkRange)
                                FindObjectOfType<UIRefresh>().ShowNpcDialogue();                               
                            // otherwise walk there
                            else
                                CmdNavigateTo(entity.tr.position, talkRange);
                        }
                    // clicked a new target
                    } else {
                        // target it
                        CmdSetTargetId(entity.netIdentity);
                    }
                // otherwise it's a movement target
                } else {
                    // set indicator
                    SetIndicatorViaPosition(hit.point, hit.normal);
                    CmdNavigateTo(hit.point, 0f);
                }
            }
        }
    }
}
