// The Npc class is rather simple. It contains state Update functions that do
// nothing at the moment, because Npcs are supposed to stand around all day.
//
// Npcs first show the welcome text and then have options for item trading and
// quests.
using UnityEngine;
using UnityEngine.Networking;

public class Npc : Entity {
    [Header("Health")]
    [SerializeField] int _hpMax = 1;
    public override int hpMax { get { return _hpMax; } }

    [Header("Mana")]
    [SerializeField] int _mpMax = 1;
    public override int mpMax { get { return _mpMax; } }

    [Header("Welcome Text")]
    [TextArea(1, 30)] public string welcome;

    [Header("Items for Sale")]
    public ItemTemplate[] saleItems;

    [Header("Quest")]
    public QuestTemplate quest;

    // other properties
    public override int damage { get { return 0; } }
    public override int defense { get { return 0; } }
    
    // networkbehaviour ////////////////////////////////////////////////////////
    [Server]
    public override void OnStartServer() {
        base.OnStartServer();

        // all npcs should spawn with full health and mana
        hp = hpMax;
        mp = mpMax;
    }

    void UpdateIDLE() {}
    void UpdateDEAD() {}
}
