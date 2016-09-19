// The Skill struct only contains the dynamic skill properties and a name, so
// that the static properties can be read from the scriptable object. The
// benefits are low bandwidth and easy Player database saving (saves always
// refer to the scriptable skill, so we can change that any time).
//
// Skills have to be structs in order to work with SyncLists.
//
// We implemented the cooldowns in a non-traditional way. Instead of counting
// and increasing the elapsed time since the last cast, we simply set the
// 'end' Time variable to Time.time + cooldown after casting each time. This
// way we don't need an extra Update method that increases the elapsed time for
// each skill all the time.
//
//
// Note: the file can't be named "Skill.cs" because of the following UNET bug:
// http://forum.unity3d.com/threads/bug-syncliststruct-only-works-with-some-file-names.384582/
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public struct Skill {
    // name used to reference the database entry (cant save template directly
    // because synclist only support simple types)
    public string name;

    // dynamic stats (cooldowns etc.)
    public bool learned;
    public float castTimeEnd; // server time
    public float cooldownEnd; // server time
    public float buffTimeEnd; // server time

    // constructors
    public Skill(SkillTemplate template) {
        name = template.name;

        // learned only if learned by default
        learned = template.learnDefault;

        // ready immediately
        castTimeEnd = Time.time;
        cooldownEnd = Time.time;
        buffTimeEnd = Time.time;
    }

    // does the template still exist?
    public bool TemplateExists() {
        return SkillTemplate.dict.ContainsKey(name);
    }

    // database quest property access
    public string category {
        get { return SkillTemplate.dict[name].category; }
    }
    public int damage {
        get { return SkillTemplate.dict[name].damage; }
    }
    public float castTime {
        get { return SkillTemplate.dict[name].castTime; }
    }
    public float cooldown {
        get { return SkillTemplate.dict[name].cooldown; }
    }
    public float castRange {
        get { return SkillTemplate.dict[name].castRange; }
    }
    public int manaCosts {
        get { return SkillTemplate.dict[name].manaCosts; }
    }
    public int healsHp {
        get { return SkillTemplate.dict[name].healsHp; }
    }
    public int healsMp {
        get { return SkillTemplate.dict[name].healsMp; }
    }
    public float buffTime {
        get { return SkillTemplate.dict[name].buffTime; }
    }
    public int buffsHpMax {
        get { return SkillTemplate.dict[name].buffsHpMax; }
    }
    public int buffsMpMax {
        get { return SkillTemplate.dict[name].buffsMpMax; }
    }
    public int buffsDamage {
        get { return SkillTemplate.dict[name].buffsDamage; }
    }
    public int buffsDefense {
        get { return SkillTemplate.dict[name].buffsDefense; }
    }
    public bool autorepeat {
        get { return SkillTemplate.dict[name].autorepeat; }
    }
    public string description {
        get { return SkillTemplate.dict[name].description; }
    }
    public Sprite image {
        get { return SkillTemplate.dict[name].image; }
    }
    public string animation {
        get { return SkillTemplate.dict[name].animation; }
    }
    public Projectile projectile {
        get { return SkillTemplate.dict[name].projectile; }
    }
    public bool learnDefault {
        get { return SkillTemplate.dict[name].learnDefault; }
    }
    public int learnLevel {
        get { return SkillTemplate.dict[name].learnLevel; }
    }
    public long learnSkillExp {
        get { return SkillTemplate.dict[name].learnSkillExp; }
    }

    // tooltip
    public string Tooltip(bool showRequirements = false) {
        return SkillTemplate.dict[name].Tooltip(showRequirements);
    }

    public float CastTimeRemaining() {
        // we use the NetworkTime offset so that client cooldowns match server
        var serverTime = Time.time + NetworkTime.offset;

        // how much time remaining until the casttime ends?
        return serverTime >= castTimeEnd ? 0f : castTimeEnd - serverTime;
    }

    public bool IsCasting() {
        // we are casting a skill if the casttime remaining is > 0
        return CastTimeRemaining() > 0f;
    }

    public float CooldownRemaining() {
        // we use the NetworkTime offset so that client cooldowns match server
        var serverTime = Time.time + NetworkTime.offset;

        // how much time remaining until the cooldown ends?
        return serverTime >= cooldownEnd ? 0f : cooldownEnd - serverTime;
    }

    public float BuffTimeRemaining() {
        // we use the NetworkTime offset so that client cooldowns match server
        var serverTime = Time.time + NetworkTime.offset;

        // how much time remaining until the buff ends?
        return serverTime >= buffTimeEnd ? 0f : buffTimeEnd - serverTime;        
    }

    public bool IsReady() {
        return CooldownRemaining() == 0f;
    }    
}

public class SyncListSkill : SyncListStruct<Skill> { }
