// Saves the quest info in a ScriptableObject that can be used ingame by
// referencing it from a MonoBehaviour. It only stores an quest's static data.
//
// We also add each one to a dictionary automatically, so that all of them can
// be found by name without having to put them all in a database. Note that
// OnEnable is only called for ScriptableObjects that are actually used (or
// referred to) in the game - which is fine because we don't really care about
// those objects that aren't referred to in the game.
//
// A Quest can be created by right clicking in the Project Area and selecting
// Create -> Scriptable Object -> QuestTemplate. Existing quests can be found in
// the ScriptableObjects/Quests folder.
using UnityEngine;
using System.Collections.Generic;

public class QuestTemplate : ScriptableObject {
    // general stats
    [TextArea(1, 30)] public string description;

    // requirements
    public int level; // for players with at least level ...

    // rewards
    public int rewardGold;
    public int rewardExp;

    // fulfillment tasks
    public Monster killTarget;
    public int killAmount;

    // tooltip
    public string Tooltip() {
        // name
        var tip = "<b>" + name + "</b>\n";

        // description
        tip += description + "\n\n";

        // fulfillment tasks
        tip += "Tasks:\n";
        if (killAmount > 0)
            tip += "* Kill " + killTarget.name + ": " + killAmount + "\n";
        tip += "\n";

        // rewards
        tip += "Rewards:\n";
        if (rewardGold > 0) tip += "* " + rewardGold + " Gold\n";
        if (rewardExp > 0) tip += "* " + rewardExp + " Experience\n";
        tip += "\n";        

        return tip;
    }

    // caching /////////////////////////////////////////////////////////////////
    public static Dictionary<string, QuestTemplate> dict = new Dictionary<string, QuestTemplate>();
    void OnEnable() { dict[name] = this; }
}
