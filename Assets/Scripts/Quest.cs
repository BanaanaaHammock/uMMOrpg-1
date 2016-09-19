// The Quest struct only contains the dynamic quest properties and a name, so
// that the static properties can be read from the scriptable object. The
// benefits are low bandwidth and easy Player database saving (saves always
// refer to the scriptable quest, so we can change that any time).
//
// Quests have to be structs in order to work with SyncLists.
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public struct Quest {
    // name used to reference the database entry (cant save template directly
    // because synclist only support simple types)
    public string name;

    // dynamic stats
    public int killed;

    // constructors
    public Quest(QuestTemplate template) {
        name = template.name;
        killed = 0;
    }

    // does the template still exist?
    public bool TemplateExists() {
        return QuestTemplate.dict.ContainsKey(name);
    }

    // database quest property access
    public string description {
       get { return QuestTemplate.dict[name].description; }
    }
    public int level {
       get { return QuestTemplate.dict[name].level; }
    }
    public int rewardGold {
       get { return QuestTemplate.dict[name].rewardGold; }
    }
    public int rewardExp {
       get { return QuestTemplate.dict[name].rewardExp; }
    }
    public string killName {
       get { return QuestTemplate.dict[name].killTarget.name; }
    }
    public int killAmount {
       get { return QuestTemplate.dict[name].killAmount; }
    }

    // tooltip
    public string Tooltip() {
       string tip = QuestTemplate.dict[name].Tooltip();

        // progress (if anything to do)
        if (killAmount > 0) {
            tip += "Progress:\n";
            tip += "* Kill " + killName + ": " + killed + "\n";
        }
        
        // status
        if (IsFinished()) tip += "<i>Completed!</i>\n";

        return tip;
    }

    public bool IsFinished() {
        return killed >= killAmount;
    }
}

public class SyncListQuest : SyncListStruct<Quest> { }
