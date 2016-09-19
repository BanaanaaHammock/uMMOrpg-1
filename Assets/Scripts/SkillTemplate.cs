// Saves the skill info in a ScriptableObject that can be used ingame by
// referencing it from a MonoBehaviour. It only stores an skill's static data.
//
// We also add each one to a dictionary automatically, so that all of them can
// be found by name without having to put them all in a database. Note that
// OnEnable is only called for ScriptableObjects that are actually used (or
// referred to) in the game - which is fine because we don't really care about
// those objects that aren't referred to in the game.
//
// A new Skill can be created by right clicking in the Project Area and
// selecting Create -> Scriptable Object -> SkillTemplate. Existing skills can
// be found in the ScriptableObjects/Items folder.
using UnityEngine;
using System.Collections.Generic;

public class SkillTemplate : ScriptableObject {
    // general stats
    // we can use the category to decide what to do on use. example categories:
    // Attack, Stun, Buff, Heal, ...
    public string category;
    public int damage;
    public float castTime;
    public float cooldown;
    public float castRange;
    public int manaCosts;
    public int healsHp;
    public int healsMp;
    public float buffTime;
    public int buffsHpMax;
    public int buffsMpMax;
    public int buffsDamage;
    public int buffsDefense;
    public bool autorepeat;
    [TextArea(1, 30)] public string description;
    public Sprite image;
    public string animation; // Fired as Trigger
    public Projectile projectile; // Arrows, Bullets, Fireballs, ...

    // skill learning
    public bool learnDefault; // normal attack etc.
    public int learnLevel; // level needed to learn it
    public long learnSkillExp; // skill experience costs

    // tooltip
    public string Tooltip(bool showRequirements = false) {
        var tip = "";

        // name
        tip += "<b>" + name + "</b>\n";

        // description
        tip += description + "\n\n";

        // damage
        tip += "Damage: " + damage + "\n";

        // cast time
        tip += "Cast Time: " + castTime + "s\n";

        // cooldown
        tip += "Cooldown: " + cooldown + "s\n";

        // cast range
        tip += "Cast Range: " + castRange + "\n";

        // healing
        if (healsHp > 0) tip += "Heals Health: " + healsHp + "\n";
        if (healsMp > 0) tip += "Heals Mana: " + healsMp + "\n";

        // buff
        if (buffTime > 0) tip += "Buff Time: " + buffTime + "\n";
        if (buffsHpMax > 0) tip += "Buffs max Health: " + buffsHpMax + "\n";
        if (buffsMpMax > 0) tip += "Buffs max Mana: " + buffsMpMax + "\n";
        if (buffsDamage > 0) tip += "Buffs Damage: " + buffsDamage + "\n";
        if (buffsDefense > 0) tip += "Buffs Defense: " + buffsDefense + "\n";

        // mana costs
        tip += "Mana Costs: " + manaCosts + "\n";

        // requirements
        if (showRequirements) {
            tip += "\n<b><i>Required Level: " + learnLevel + "</i></b>\n";
            tip += "<b><i>Required Skill Exp.: " + learnSkillExp + "</i></b>";
        }

        return tip;
    }

    // caching /////////////////////////////////////////////////////////////////
    public static Dictionary<string, SkillTemplate> dict = new Dictionary<string, SkillTemplate>();
    void OnEnable() { dict[name] = this; }
}
