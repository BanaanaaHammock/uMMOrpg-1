// The Entity class is rather simple. It contains a few basic entity properties
// like health, mana and level _(which are not public)_ and then offers several
// public functions to read and modify them.
// 
// Entities also have a _target_ Entity that can't be synchronized with a
// SyncVar. Instead we created a EntityTargetSync component that takes care of
// that for us.
// 
// Entities have a _state_ property that takes care of the entity's current
// state. For example, when the state is "DEAD" then an entity shouldn't be
// allowed to move around or cast spells. When the state is "IDLE" then the
// entity should be able to move around and cast spells. And after casting a
// spell, we can set the state to "CASTING" and take care of all the casting
// logic. Every class that derives from the Entity class will receive Update
// calls like UpdateIDLE or UpdateDEATH or UpdateCASTING depending on the
// current state. Those Update functions can be used to handle whatever actions
// and events are needed in those states.
//
// Each entity needs two colliders. First of all, the proximity checks don't
// work if there is no collider on that same GameObject, hence why all Entities
// have a very small trigger BoxCollider on them. They also need a real trigger
// that always matches their position, so that Raycast selection works. The
// real trigger is always attached to the pelvis in the bone structure, so that
// it automatically follows the animation. Otherwise we wouldn't be able to
// select dead entities because their death animation often throws them far
// behind.
//
// Entities also need a kinematic Rigidbody so that OnTrigger functions can be
// called. Note that there is currently a Unity bug that slows down the agent
// when having lots of FPS(300+) if the Rigidbody's Interpolate option is
// enabled. So for now it's important to disable Interpolation - which is a good
// idea in general to increase performance.
using UnityEngine;
using UnityEngine.Networking;

[RequireComponent(typeof(Rigidbody))] // kinematic, only needed for OnTrigger
[RequireComponent(typeof(NetworkProximityChecker))]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NetworkNavMeshAgent))]
[RequireComponent(typeof(EntityTargetSync))]
[RequireComponent(typeof(Animator))]
public abstract class Entity : NetworkBehaviour {
    // properties
    [Header("State")]
    [SyncVar] public string state = "IDLE";

    // [SyncVar] NetworkIdentity wouldnt work because targets can get null if
    // they disappear or disconnect, which would result in unet exceptions.
    [Header("Target")]
    public Entity target;

    [Header("Level")]
    [SyncVar] public int level = 1;
    
    [Header("Health")]
    [SyncVar, SerializeField] protected bool invincible = false; // GMs, Npcs, ...
    [SyncVar, SerializeField] protected bool hpRecovery = true; // can be disabled in combat etc.
    [SyncVar, SerializeField] protected int hpRecoveryRate = 1; // per second
    [SyncVar                ] private int _hp = 1;
    public int hp {
        get { return _hp; }
        set { _hp = Mathf.Clamp(value, 0, hpMax); }
    }
    public abstract int hpMax{ get; }

    [Header("Mana")]
    [SyncVar, SerializeField] protected bool mpRecovery = true; // can be disabled in combat etc.
    [SyncVar, SerializeField] protected int mpRecoveryRate = 1; // per second
    [SyncVar                ] private int _mp = 1;
    public int mp {
        get { return _mp; }
        set { _mp = Mathf.Clamp(value, 0, mpMax); }
    }
    public abstract int mpMax{ get; }

    [Header("Damage Popup")]
    [SerializeField] GameObject damagePopupPrefab;

    // other properties
    public float speed { get{ return agent.speed; } }
    public abstract int damage { get; }
    public abstract int defense { get; }

    // cache
    [HideInInspector] public Transform tr;
    [HideInInspector] public NavMeshAgent agent;
    [HideInInspector] public NetworkProximityChecker proxchecker;
    [HideInInspector] public Animator anim;
    [HideInInspector] public NetworkIdentity netIdentity;

    // networkbehaviour ////////////////////////////////////////////////////////
    // cache components on server and clients
    protected virtual void Awake() {
        tr = GetComponent<Transform>();
        agent = GetComponent<NavMeshAgent>();
        proxchecker = GetComponent<NetworkProximityChecker>();
        anim = GetComponent<Animator>();
        netIdentity = GetComponent<NetworkIdentity>();
    }

    [Server]
    public override void OnStartServer() {
        // health recovery every second
        InvokeRepeating("Recover", 1, 1);

        // HpDecreaseBy changes to "DEAD" state when hp drops to 0, but there is
        // a case where someone might instantiated a Entity with hp set to 0,
        // hence we have to check that case once at start
        if (hp == 0) state = "DEAD";
    }

    // entity logic will be implemented with a finite state machine. we will use
    // UpdateIDLE etc. so that we have a 100% guarantee that it works properly
    // and we never miss a state or update two states after another
    // note: can still use LateUpdate for Updates that should happen in any case
    // -> we can even use parameters if we need them some day.
    void Update() {
        // monsters, npcs etc. don't have to be updated if no player is around
        // checking observers is enough, because lonely players have at least
        // themselves as observers, so players will always be updated
        // -> update only if:
        //    - observers are null (they are null in clients)
        //    - if they are not null, then only if at least one (on server)
        if (netIdentity.observers == null || netIdentity.observers.Count > 0)
            SendMessage("Update" + state, SendMessageOptions.RequireReceiver);
    }

    // visibility //////////////////////////////////////////////////////////////
    // hide a entity
    // note: using SetActive won't work because its not synced and it would
    //       cause inactive objects to not receive any info anymore
    // note: this won't be visible on the server as it always sees everything.
    [Server]
    public void Hide() {
        proxchecker.forceHidden = true;
    }

    [Server]
    public void Show() {
        proxchecker.forceHidden = false;
    }

    // is the entity currently hidden?
    // note: usually the server is the only one who uses forceHidden, the
    //       client usually doesn't know about it and simply doesn't see the
    //       GameObject. but we don't use a [Server] tag so that our
    //       GetTargetEntityOrSelf() function doesn't throw warnings
    public bool IsHidden() {
        return proxchecker.forceHidden;
    }

    public float VisRange() {
        return proxchecker.visRange;
    }

    // look at a transform while only rotating on the Y axis (to avoid weird
    // tilts)
    public void LookAtY(Vector3 pos) {
        tr.LookAt(new Vector3(pos.x, tr.position.y, pos.z));
    }
    
    // note: client can find out if moving by simply checking the state!
    [Server] // server is the only one who has up-to-date NavMeshAgent
    public bool IsMoving() {
        // -> agent.hasPath will be true if stopping distance > 0, so we can't
        //    really rely on that.
        // -> pathPending is true while calculating the path, which is good
        // -> remainingDistance is the distance to the last path point, so it
        //    also works when clicking somewhere onto a obstacle that isn'
        //    directly reachable.
        return (agent.pathPending ||
                agent.remainingDistance > agent.stoppingDistance ||
                agent.velocity != Vector3.zero);
    }

    // health & mana ///////////////////////////////////////////////////////////
    public float HpPercent() {
        return (hp != 0 && hpMax != 0) ? (float)hp / (float)hpMax : 0.0f;
    }

    [Server]
    public void Revive() {
        hp = hpMax;
    }
    
    public float MpPercent() {
        return (mp != 0 && mpMax != 0) ? (float)mp / (float)mpMax : 0.0f;
    }

    // combat //////////////////////////////////////////////////////////////////
    // no need to instantiate damage popups on the server
    [ClientRpc]
    void RpcShowDamagePopup(int damage, Vector3 pos) {
        // spawn the damage popup (if any) and set the text
        if (damagePopupPrefab) {
            var popup = (GameObject)Instantiate(damagePopupPrefab, pos, Quaternion.identity);
            popup.GetComponent<TextMesh>().text = damage.ToString();
        }
    }

    // deal damage at another entity
    // (can be overwritten for players etc. that need custom functionality)
    [Server]
    public virtual void DealDamageAt(Entity entity, int n) {
        // subtract defense (but leave at least 1 damage, otherwise it may be
        // frustrating for weaker players)
        // [dont deal any damage if invincible]
        var dmg = !entity.invincible ? Mathf.Max(n-entity.defense, 1) : 0;
        entity.hp -= dmg;

        // show damage popup in observers via ClientRpc
        entity.RpcShowDamagePopup(dmg, entity.GetComponentInChildren<Collider>().bounds.center);
    }

    // recovery ////////////////////////////////////////////////////////////////
    // receover health and mana once a second
    // note: when stopping the server with the networkmanager gui, it will
    //       generate warnings that Recover was called on client because some
    //       entites will only be disabled but not destroyed. let's not worry
    //       about that for now.
    [Server]
    void Recover() {
        if (enabled && hp > 0) {
            if (hpRecovery) hp += hpRecoveryRate;
            if (mpRecovery) mp += mpRecoveryRate;
        }
    }

    // aggro ///////////////////////////////////////////////////////////////////
    // this function is called by the AggroArea (if any) on clients and server
    public virtual void OnAggro(Entity entity) { }
}
