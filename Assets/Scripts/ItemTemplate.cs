// Saves the item info in a ScriptableObject that can be used ingame by
// referencing it from a MonoBehaviour. It only stores an item's static data.
//
// We also add each one to a dictionary automatically, so that all of them can
// be found by name without having to put them all in a database. Note that
// OnEnable is only called for ScriptableObjects that are actually used (or
// referred to) in the game - which is fine because we don't really care about
// those objects that aren't referred to in the game.
//
// A new Item can be created by right clicking in the Project Area and selecting
// Create -> Scriptable Object -> ItemTemplate. Existing items can be found in
// the ScriptableObjects/Items folder.
using UnityEngine;
using System.Collections.Generic;

public class ItemTemplate : ScriptableObject {
    // base stats
    public string category;
    public int maxStack;
    public int buyPrice;
    public int sellPrice;
    public int minLevel; // level required to use/equip the item
    public bool sellable;
    public bool destroyable;
    [TextArea(1, 30)] public string description;
    public Sprite image;

    // one time usage boosts
    public bool usageDestroy;
    public int usageHp;
    public int usageMp;
    public int usageExp;

    // equipment boosts (quick and dirty, to avoid itemequipment class etc.)
    public int equipHpBonus;
    public int equipMpBonus;
    public int equipDamageBonus;
    public int equipDefenseBonus;
    public GameObject model; // Prefab

    // tooltip
    public string Tooltip(bool showBuyPrice = false, bool showSellPrice = false) {
        var tip = "";

        // name        
        tip += "<b>" + name + "</b>\n";

        // description
        tip += description + "\n\n";

        // equipment attack bonus
        if (equipDamageBonus > 0) tip += "+ " + equipDamageBonus + " Damage\n";

        // equipment defense bonus
        if (equipDefenseBonus > 0) tip += "+ " + equipDefenseBonus + " Defense\n";

        // equipment hp bonus
        if (equipHpBonus > 0) tip += "+ " + equipHpBonus + " Health\n";

        // equipment mp bonus
        if (equipMpBonus > 0) tip += "+ " + equipMpBonus + " Mana\n";

        // sellable / destroyable
        tip += "Destroyable: " + (destroyable ? "Yes" : "No") + "\n";
        tip += "Sellable: " + (sellable ? "Yes" : "No") + "\n";

        // required level
        tip += "Required Level:" + minLevel + "\n";

        // prices
        if (showBuyPrice) tip += "Price: " + buyPrice + " Gold\n";
        if (showSellPrice) tip += "Sells for: " + sellPrice + " Gold\n";

        return tip;
    }

    // caching /////////////////////////////////////////////////////////////////
    public static Dictionary<string, ItemTemplate> dict = new Dictionary<string, ItemTemplate>();
    void OnEnable() { dict[name] = this; }

    // inspector validation ////////////////////////////////////////////////////
    void OnValidate() {
        // make sure that the sell price <= buy price to avoid exploitation
        // (people should never buy an item for 1 gold and sell it for 2 gold)
        sellPrice = Mathf.Min(sellPrice, buyPrice);
    }
}
