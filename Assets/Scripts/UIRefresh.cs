// All Player UI logic in one place. We can't attach this component to the
// player prefab, because prefabs can't refer to Scene objects. So we could
// either use GameObject.Find to find the UI elements dynamically, or we can
// have this component attached to the canvas and find the local Player from it.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class UIRefresh : MonoBehaviour {
    [Header("Chat")]
    public InputField chatInput;
    public Button chatButton;
    public Transform chatContent;
    public Scrollbar chatScroll;
    public GameObject chatTextPrefab;
    public KeyCode[] chatActivationKeys = {KeyCode.Return, KeyCode.KeypadEnter};

    [Header("Experience")]
    public Slider expBar;
    public Text expStatus;

    [Header("Health & Mana")]
    public Slider hpBar;
    public Text hpStatus;
    public Slider mpBar;
    public Text mpStatus;

    [Header("Minimap")]
    public float zoomMin = 5.0f;
    public float zoomMax = 50.0f;
    public float zoomStepSize = 5.0f;
    public Text miniLevelName;
    public Button miniButtonPlus;
    public Button miniButtonMinus;

    [Header("Target")]
    public GameObject targetPanel;
    public Slider targetHpBar;
    public Text targetName;
    public Button targetTrade;

    [Header("Character Info")]
    public GameObject charInfoPanel;
    public Text charDamage;
    public Text charDefense;
    public Text charHealth;
    public Text charMana;
    public Text charSpeed;
    public Text charLevel;
    public Text charExpCur;
    public Text charExpMax;
    public Text charSkillExp;
    public Text charStrength;
    public Button charButtonStrength;
    public Text charIntelligence;
    public Button charButtonIntelligence;

    [Header("Respawn")]
    public GameObject respawnPanel;
    public Button respawnButton;

    [Header("Inventory")]
    public GameObject invPanel;
    public Transform invContent;
    public Text invGold;
    public UIDragAndDropable trash;
    public GameObject trashOverlay;

    [Header("Equipment")]
    public GameObject equipPanel;
    public Transform equipContent;

    [Header("Skills")]
    public GameObject skillsPanel;
    public Transform skillsContent;
    public Text skillExpAvailable;

    [Header("Skillbar")]
    public Transform skillbarContent;

    [Header("Buffs")]
    public Transform buffsContent;

    [Header("Quests")]
    public GameObject questsPanel;
    public Transform questsContent;

    [Header("Npc Dialogue")]
    public GameObject npcDialoguePanel;
    public Text npcWelcomeText;
    public Button npcTradingButton;
    public Button npcQuestButton;

    [Header("Npc Trading")]
    public GameObject npcTradingPanel;
    public Transform npcTradingContent;
    public UIDragAndDropable npcTradingBuy;
    public InputField npcTradingBuyAmount;
    public Text npcTradingBuyCosts;
    public Button npcTradingBuyButton;
    public UIDragAndDropable npcTradingSell;
    public InputField npcTradingSellAmount;
    public Text npcTradingSellCosts;
    public Button npcTradingSellButton;
    [HideInInspector] public int npcBuyIndex = -1;
    [HideInInspector] public int npcSellIndex = -1;

    [Header("Npc Quests")]
    public GameObject npcQuestPanel;
    public Text npcQuestDescription;
    public Button npcQuestButtonAction;

    [Header("Loot")]
    public GameObject lootPanel;
    public GameObject lootGoldSlot;
    public Text lootGoldText;
    public Transform lootItemContent;

    [Header("Player Trading Request")]
    public GameObject playerTradingRequestPanel;
    public Text playerTradingRequestName;
    public Button playerTradingRequestAccept;
    public Button playerTradingRequestDecline;

    [Header("Player Trading")]
    public GameObject playerTradingPanel;
    public Transform playerTradingOtherContent;
    public Text playerTradingOtherStatus;
    public InputField playerTradingOtherGold;
    public Transform playerTradingMyContent;
    public Text playerTradingMyStatus;
    public InputField playerTradingMyGold;
    public Button playerTradingLock;
    public Button playerTradingAccept;
    public Button playerTradingCancel;

    void ChatAutoScroll() {
        chatScroll.value = 0;
    }

    public void ChatAddMessage(MessageInfo msg) {
        // instantiate the text
        var g = (GameObject)Instantiate(chatTextPrefab);

        // set parent to Content object
        g.transform.SetParent(chatContent.transform, false);

        // set text and color
        g.GetComponent<Text>().text = msg.content;
        g.GetComponent<Text>().color = msg.color;

        // TODO set sender for click reply
        //g.GetComponent<ChatTextEntry>().sender = sender;

        // autoscrolling immediately doesn't work perfectly, hence do it later
        Invoke("ChatAutoScroll", 0.1f);
    }

    void UpdateChat(Player player) {
        // character limit
        var chat = player.GetComponent<PlayerChat>();
        chatInput.characterLimit = chat.maxLength;

        // activation        
        if (Utils.AnyKeyUp(chatActivationKeys)) chatInput.Select();

        // edit listener
        chatInput.onEndEdit.SetListener((value) => {
            // submit key pressed?
            if (Utils.AnyKeyDown(chatActivationKeys)) {
                // submit
                var newinput = chat.OnSubmit(value);

                // set new input text
                chatInput.text = newinput;
                chatInput.MoveTextEnd(false);
            }
        });

        // send button
        chatButton.onClick.SetListener(() => {
            // submit
            var newinput = chat.OnSubmit(chatInput.text);

            // set new input text
            chatInput.text = newinput;
            chatInput.MoveTextEnd(false);
        });
    }

    void UpdateExperienceBar(Player player) {
        expBar.value = player.ExpPercent();
        expStatus.text = "Lv." + player.level + " (" + (player.ExpPercent() * 100f).ToString("F2") + "%)";
    }

    void UpdateHealthMana(Player player) {
        // health
        hpBar.value = player.HpPercent();
        hpStatus.text = player.hp + " / " + player.hpMax;

        // mana
        mpBar.value = player.MpPercent();
        mpStatus.text = player.mp + " / " + player.mpMax;
    }

    void UpdateMinimap(Player player) {
        miniLevelName.GetComponent<Text>().text = Application.loadedLevelName;
        miniButtonPlus.onClick.SetListener(() => {
            var cam = GameObject.FindWithTag("MinimapCamera").GetComponent<Camera>();
            cam.orthographicSize = Mathf.Max(cam.orthographicSize - zoomStepSize, zoomMin);
        });
        miniButtonMinus.onClick.SetListener(() => {
            var cam = GameObject.FindWithTag("MinimapCamera").GetComponent<Camera>();
            cam.orthographicSize = Mathf.Min(cam.orthographicSize + zoomStepSize, zoomMax);
        });
    }

    void UpdateTarget(Player player) {
        // target
        if (player.target != null && player.target != player) {
            // name and health
            targetPanel.SetActive(true);
            targetHpBar.value = player.target.HpPercent();
            targetName.text = player.target.name;

            // trade button
            if (player.target is Player) {
                targetTrade.gameObject.SetActive(true);
                targetTrade.interactable = player.CanStartTradeWithTarget();
                targetTrade.onClick.SetListener(() => {
                    player.CmdTradeRequest();
                });
            } else {
                targetTrade.gameObject.SetActive(false);
            }
        } else {
            targetPanel.SetActive(false);
        }
    }

    void UpdateCharacterInfo(Player player) {
        // only if visible
        if (!charInfoPanel.activeSelf) return;

        // stats
        charDamage.text = player.damage.ToString();
        charDefense.text = player.defense.ToString();
        charHealth.text = player.hpMax.ToString();
        charMana.text = player.mpMax.ToString();
        charSpeed.text = player.speed.ToString();
        charLevel.text = player.level.ToString();
        charExpCur.text = player.exp.ToString();
        charExpMax.text = player.expMax.ToString();
        charSkillExp.text = player.skillExp.ToString();

        // attributes
        charStrength.text = player.strength.ToString();
        charButtonStrength.interactable = player.AttributesSpendable() > 0;
        charButtonStrength.onClick.SetListener(() => {
            player.CmdIncreaseStrength();
        });
        charIntelligence.text = player.intelligence.ToString();
        charButtonIntelligence.interactable = player.AttributesSpendable() > 0;
        charButtonIntelligence.onClick.SetListener(() => {
            player.CmdIncreaseIntelligence();
        });
    }

    void UpdateRespawn(Player player) {
        // visibile if player is dead
        respawnPanel.SetActive(player.hp == 0);
        respawnButton.onClick.SetListener(() => { player.CmdRespawn(); });
    }

    void UpdateInventory(Player player) {
        // only if visible
        if (!invPanel.activeSelf) return;

        // items
        for (int i = 0; i < invContent.childCount; ++i) {
            var entry = invContent.GetChild(i).GetChild(0);

            // get the item
            if (i < player.inventory.Count && player.inventory[i].valid) {
                var item = player.inventory[i];

                // click event
                int icopy = i; // needed for lambdas, otherwise i is Count
                entry.GetComponent<Button>().onClick.SetListener(() => {
                    player.CmdUseInventoryItem(icopy);
                });
                
                // set state
                entry.GetComponent<UIShowToolTip>().enabled = true;
                entry.GetComponent<UIDragAndDropable>().dragable = true;
                // note: entries should be dropable at all times

                // image
                entry.GetComponent<Image>().color = Color.white;
                entry.GetComponent<Image>().sprite = item.image;
                entry.GetComponent<UIShowToolTip>().text = item.Tooltip(showSellPrice:true);

                // amount overlay
                var amount = item.amount;
                entry.transform.GetChild(0).gameObject.SetActive(amount > 1);
                if (amount > 1) entry.GetComponentInChildren<Text>().text = amount.ToString();
            } else {
                // remove listeners
                entry.GetComponent<Button>().onClick.RemoveAllListeners();

                // set state
                entry.GetComponent<UIShowToolTip>().enabled = false;
                entry.GetComponent<UIDragAndDropable>().dragable = false;

                // image
                entry.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                entry.GetComponent<Image>().sprite = null;

                // amount overlay
                entry.transform.GetChild(0).gameObject.SetActive(false);
            }
        }
        if (player.inventory.Count > invContent.childCount)
            Debug.LogWarning("not enough UI slots for inventory");

        // gold
        invGold.text = player.gold.ToString();

        // trash
        // note: tooltip should always be enabled here and dropable should be
        //       true at all times
        trash.dragable = player.trash.valid;

        // set other properties
        if (player.trash.valid) {
            // image
            trash.GetComponent<Image>().color = Color.white;
            trash.GetComponent<Image>().sprite = player.trash.image;

            // amount overlay
            var amount = player.trash.amount;
            trashOverlay.gameObject.SetActive(amount > 1);
            if (amount > 1) trashOverlay.GetComponentInChildren<Text>().text = amount.ToString();
        } else {
            // image
            trash.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
            trash.GetComponent<Image>().sprite = null;

            // amount overlay
            trashOverlay.gameObject.SetActive(false);
        }
    }

    void UpdateEquipment(Player player) {
        // only if visible
        if (!equipPanel.activeSelf) return;

        // equipment
        for (int i = 0; i < equipContent.childCount; ++i) {
            var entry = equipContent.GetChild(i).GetChild(0);

            // get the item
            if (i < player.equipment.Count && player.equipment[i].valid) {
                var item = player.equipment[i];

                // click event (done more than once but w/e)
                entry.GetComponent<Button>().onClick.RemoveAllListeners();
                
                // set state
                entry.GetComponent<UIShowToolTip>().enabled = item.valid;
                entry.GetComponent<UIDragAndDropable>().dragable = item.valid;
                // note: entries should be dropable at all times

                // image
                entry.GetComponent<Image>().color = Color.white;
                entry.GetComponent<Image>().sprite = item.image;
                entry.GetComponent<UIShowToolTip>().text = item.Tooltip(showSellPrice:true);
            } else {
                // remove listeners
                entry.GetComponent<Button>().onClick.RemoveAllListeners();
                
                // image
                entry.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                entry.GetComponent<Image>().sprite = null;
            }
        }
        if (player.equipment.Count > equipContent.childCount)
            Debug.LogWarning("not enough UI slots for equipment");
    }

    void UpdateSkills(Player player) {
        // only if visible
        if (!skillsPanel.activeSelf) return;

        // skill exp available
        skillExpAvailable.text = player.skillExp.ToString();

        // skills
        for (int i = 0; i < skillsContent.childCount; ++i) {
            var entry = skillsContent.GetChild(i).GetChild(0).GetChild(0);

            // get the skill
            if (i < player.skills.Count) {
                var skill = player.skills[i];

                skillsContent.GetChild(i).gameObject.SetActive(true);

                // click event (done more than once but w/e)
                entry.GetComponent<Button>().onClick.RemoveAllListeners();
                int icopy = i; // needed for lambdas, otherwise i is Count
                if (skill.learned) {
                    entry.GetComponent<Button>().onClick.SetListener(() => {
                        player.CmdUseSkill(icopy);
                    });
                }
                entry.GetComponent<Button>().interactable = skill.learned;
                
                // set state
                entry.GetComponent<UIShowToolTip>().enabled = false; // in description
                entry.GetComponent<UIDragAndDropable>().dragable = skill.learned;
                // note: entries should be dropable at all times

                // image
                if (skill.learned) {
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = skill.image;
                }

                // description
                var descPanel = entry.transform.parent.parent.GetChild(1);
                descPanel.GetChild(0).GetComponent<Text>().text = skill.Tooltip(showRequirements:!skill.learned);

                // learn button
                var btnLearn = descPanel.GetChild(1).GetComponent<Button>();
                btnLearn.gameObject.SetActive(!skill.learned);
                btnLearn.interactable = player.level >= skill.learnLevel && player.skillExp >= skill.learnSkillExp;
                btnLearn.onClick.SetListener(() => { player.CmdLearnSkill(icopy); });

                // cooldown overlay
                var cd = skill.CooldownRemaining();
                if (skill.learned && cd > 0) {
                    entry.transform.GetChild(0).gameObject.SetActive(true);
                    entry.GetComponentInChildren<Text>().text = cd.ToString("F0");
                } else {
                    entry.transform.GetChild(0).gameObject.SetActive(false);
                }
            } else {
                skillsContent.GetChild(i).gameObject.SetActive(false);
            }
        }
        if (player.skills.Count > skillsContent.childCount)
            Debug.LogWarning("not enough UI slots for skills");
    }

    void UpdateSkillbar(Player player) {
        for (int i = 0; i < skillbarContent.childCount; ++i) {
            var entry = skillbarContent.GetChild(i).GetChild(0);

            // get the reference
            if (i < player.skillbar.Length) {
                // any reference in this entry?   
                var found = false;                 
                if (player.skillbar[i] != "") {
                    // skill, inventory item or equipment item?
                    var skillIndex = player.GetLearnedSkillIndexByName(player.skillbar[i]);
                    var invIndex = player.GetInventoryIndexByName(player.skillbar[i]);
                    var equipIndex = player.GetEquipmentIndexByName(player.skillbar[i]);
                    if (skillIndex != -1) {
                        found = true;

                        // click event (done more than once but w/e)
                        entry.GetComponent<Button>().onClick.SetListener(() => {
                            player.CmdUseSkill(skillIndex);
                        });
                        
                        // set state
                        entry.GetComponent<UIShowToolTip>().enabled = true;
                        entry.GetComponent<UIDragAndDropable>().dragable = true;
                        // note: entries should be dropable at all times

                        // image
                        entry.GetComponent<Image>().color = Color.white;
                        entry.GetComponent<Image>().sprite = player.skills[skillIndex].image;
                        entry.GetComponent<UIShowToolTip>().text = player.skills[skillIndex].Tooltip();

                        // overlay cooldown
                        var cd = player.skills[skillIndex].CooldownRemaining();
                        entry.transform.GetChild(0).gameObject.SetActive(cd > 0);
                        if (cd > 1) entry.transform.GetChild(0).GetComponentInChildren<Text>().text = cd.ToString("F0");

                        // hotkey pressed?
                        if (Input.GetKeyDown(player.skillbarHotkeys[i]))
                            player.CmdUseSkill(skillIndex);
                    } else if (invIndex != -1) {
                        found = true;

                        // click event (done more than once but w/e)
                        entry.GetComponent<Button>().onClick.SetListener(() => {
                            player.CmdUseInventoryItem(invIndex);
                        });
                        
                        // set state
                        entry.GetComponent<UIShowToolTip>().enabled = true;
                        entry.GetComponent<UIDragAndDropable>().dragable = true;
                        // note: entries should be dropable at all times

                        // image
                        entry.GetComponent<Image>().color = Color.white;
                        entry.GetComponent<Image>().sprite = player.inventory[invIndex].image;
                        entry.GetComponent<UIShowToolTip>().text = player.inventory[invIndex].Tooltip();

                        // overlay amount
                        var amount = player.inventory[invIndex].amount;
                        entry.transform.GetChild(0).gameObject.SetActive(amount > 1);
                        if (amount > 1) entry.transform.GetChild(0).GetComponentInChildren<Text>().text = amount.ToString();
                        
                        // hotkey pressed?
                        if (Input.GetKeyDown(player.skillbarHotkeys[i]))
                            player.CmdUseInventoryItem(invIndex);
                    } else if (equipIndex != -1) {
                        found = true;

                        // click event (done more than once but w/e)
                        entry.GetComponent<Button>().onClick.RemoveAllListeners();
                        
                        // set state
                        entry.GetComponent<UIShowToolTip>().enabled = true;
                        entry.GetComponent<UIDragAndDropable>().dragable = true;
                        // note: entries should be dropable at all times

                        // image
                        entry.GetComponent<Image>().color = Color.white;
                        entry.GetComponent<Image>().sprite = player.equipment[equipIndex].image;
                        entry.GetComponent<UIShowToolTip>().text = player.equipment[equipIndex].Tooltip();

                        // overlay
                        entry.transform.GetChild(0).gameObject.SetActive(false);
                    } else {
                        // outdated reference. clear it.
                        player.skillbar[i] = "";
                    }
                }

                // not found? then clear
                if (!found) {
                    // remove listeners                    
                    entry.GetComponent<Button>().onClick.RemoveAllListeners();

                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = false;
                    entry.GetComponent<UIDragAndDropable>().dragable = false;

                    // image
                    entry.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                    entry.GetComponent<Image>().sprite = null;

                    // overlay
                    entry.transform.GetChild(0).gameObject.SetActive(false);
                }
            }
        }
        if (player.skillbar.Length > skillbarContent.childCount)
            Debug.LogWarning("not enough UI slots for skillbar");
    }

    void UpdateBuffs(Player player) {
        // go through all skills and find buffs
        var idx = 0;
        for (int i = 0; i < player.skills.Count; ++i) {
            // is this an active buff?
            var skill = player.skills[i];
            if (skill.BuffTimeRemaining() > 0) {
                // are we still in ui slot range?
                if (idx < buffsContent.childCount) {
                    // enable
                    buffsContent.GetChild(idx).gameObject.SetActive(true);

                    // get entry
                    var entry = buffsContent.GetChild(idx).GetChild(0);

                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = skill.image;
                    entry.GetComponent<UIShowToolTip>().text = skill.Tooltip();

                    // time bar
                    var slider = entry.transform.GetChild(0).GetComponent<Slider>();
                    slider.maxValue = skill.buffTime;
                    slider.value = skill.BuffTimeRemaining();
                    
                    // increase index
                    ++idx;
                } else {
                    Debug.LogWarning("not enough UI slots for buffs");
                }
            }
        }

        // hide all the remaining slots
        for (; idx < buffsContent.childCount; ++idx)
            buffsContent.GetChild(idx).gameObject.SetActive(false);
    }

    void UpdateLoot(Player player) {
        // only if visible
        if (!lootPanel.activeSelf) return;

        // loot
        if (player.target != null &&
            player.target.hp == 0 &&
            Vector3.Distance(player.transform.position, player.target.transform.position) <= player.lootRange &&
            player.target is Monster &&
            ((Monster)player.target).lootItems.Count > 0) {
            // cache monster
            var mob = (Monster)player.target;

            // gold
            if (mob.lootGold > 0) {
                lootGoldSlot.SetActive(true);

                // button
                lootGoldSlot.GetComponentInChildren<Button>().onClick.SetListener(() => { player.CmdTakeLootGold(); });

                // amount
                lootGoldText.text = mob.lootGold.ToString();
            } else {
                lootGoldSlot.SetActive(false);
            }

            // loot items
            for (int i = 0; i < lootItemContent.childCount; ++i) {
                var entry = lootItemContent.GetChild(i).GetChild(0).GetChild(0);

                // get the item
                if (i < mob.lootItems.Count && mob.lootItems[i].valid) {
                    lootItemContent.GetChild(i).gameObject.SetActive(true);

                    var item = mob.lootItems[i];

                    // click event (done more than once but w/e)
                    int icopy = i; // otherwise listener uses the last i value
                    entry.GetComponent<Button>().onClick.SetListener(() => {
                        player.CmdTakeLootItem(icopy);
                    });
                    
                    // set state
                    entry.GetComponent<UIShowToolTip>().enabled = item.valid;
                    entry.GetComponent<UIDragAndDropable>().dragable = item.valid;
                    // note: entries should be dropable at all times

                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = item.image;
                    entry.GetComponent<UIShowToolTip>().text = item.Tooltip(showSellPrice:true);

                    // name
                    entry.parent.parent.GetChild(1).GetComponent<Text>().text = item.name;

                    // amount overlay
                    var amount = item.amount;
                    entry.transform.GetChild(0).gameObject.SetActive(amount > 1);
                    if (amount > 1) entry.GetComponentInChildren<Text>().text = amount.ToString();
                } else {
                    lootItemContent.GetChild(i).gameObject.SetActive(false);
                }
            }
            if (mob.lootItems.Count > lootItemContent.childCount)
                Debug.LogWarning("not enough UI slots for skillbar");
        } else {
            // hide
            lootPanel.SetActive(false);
        }
    }

    void UpdateQuests(Player player) {
        // only if visible
        if (!questsPanel.activeSelf) return;

        // quests
        for (int i = 0; i < questsContent.childCount; ++i) {
            var entry = questsContent.GetChild(i).GetChild(0);

            // get the quest
            if (i < player.quests.Count) {
                questsContent.GetChild(i).gameObject.SetActive(true);
                entry.GetComponent<Text>().text = player.quests[i].Tooltip();
            } else {
                questsContent.GetChild(i).gameObject.SetActive(false);
            }
        }
        if (player.questLimit > questsContent.childCount)
            Debug.LogWarning("not enough UI slots for quests");
    }

    void UpdateNpcDialogue(Player player) {
        // only if visible
        if (!npcDialoguePanel.activeSelf) return;
        
        // npc dialogue
        if (player.target != null && player.target is Npc &&
            Vector3.Distance(player.transform.position, player.target.transform.position) <= player.talkRange) {
            var npc = (Npc)player.target;
            // welcome text
            npcWelcomeText.text = npc.welcome;

            // trading button
            npcTradingButton.gameObject.SetActive(npc.saleItems.Length > 0);
            npcTradingButton.onClick.SetListener(() => {
                npcTradingPanel.SetActive(true);
                npcDialoguePanel.SetActive(false);
            });

            // quest button
            npcQuestButton.gameObject.SetActive(npc.quest != null);
            npcQuestButton.onClick.SetListener(() => {
                npcQuestPanel.SetActive(true);
                npcDialoguePanel.SetActive(false);
            });
            if (npc.quest != null)
                npcQuestButton.GetComponentInChildren<Text>().text = "Quest: " + npc.quest.name;
        }
    }

    void UpdateNpcQuest(Player player) {
        // only if visible
        if (!npcQuestPanel.activeSelf) return;

        // npc quest
        if (player.target != null && player.target is Npc &&
            ((Npc)player.target).quest != null &&
            Vector3.Distance(player.transform.position, player.target.transform.position) <= player.talkRange) {
            var npc = (Npc)player.target;

            npcQuestDescription.text = npc.quest.Tooltip();

            var idx = player.GetQuestIndexByName(npc.quest.name);
            if (idx != -1) {
                // complete
                npcQuestButtonAction.GetComponentInChildren<Text>().text = "Complete";
                npcQuestButtonAction.onClick.SetListener(() => {
                    player.CmdCompleteQuest();
                    npcQuestPanel.SetActive(false);
                });
                npcQuestButtonAction.interactable = player.quests[idx].IsFinished();
            } else {
                // accept
                npcQuestButtonAction.GetComponentInChildren<Text>().text = "Accept";
                npcQuestButtonAction.onClick.SetListener(() => {
                    player.CmdAcceptQuest();
                });
            }
        } else {
            // hide
            npcQuestPanel.SetActive(false);
        }
    }

    void UpdateNpcTrading(Player player) {
        // only if visible
        if (!npcTradingPanel.activeSelf) return;
        
        // npc trading
        if (player.target != null && player.target is Npc &&
            Vector3.Distance(player.transform.position, player.target.transform.position) <= player.talkRange) {
            var npc = (Npc)player.target;

            // items for sale            
            for (int i = 0; i < npcTradingContent.childCount; ++i) {
                var entry = npcTradingContent.GetChild(i).GetChild(0);

                // get the item
                if (i < npc.saleItems.Length) {
                    var item = npc.saleItems[i];

                    // set the click event
                    int icopy = i;
                    entry.GetComponent<Button>().onClick.SetListener(() => {
                        npcBuyIndex = icopy;
                    });
                    
                    // image
                    entry.GetComponent<Image>().color = Color.white;
                    entry.GetComponent<Image>().sprite = item.image;

                    // tooltip
                    entry.GetComponent<UIShowToolTip>().enabled = true;
                    entry.GetComponent<UIShowToolTip>().text = item.Tooltip(showBuyPrice:true);
                } else {
                    // remove listeners
                    entry.GetComponent<Button>().onClick.RemoveAllListeners();

                    // image
                    entry.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                    entry.GetComponent<Image>().sprite = null;

                    // tooltip
                    entry.GetComponent<UIShowToolTip>().enabled = false;
                }
            }
            if (npc.saleItems.Length > npcTradingContent.childCount)
                Debug.LogWarning("not enough UI slots for npc trading");

            // buy
            if (npcBuyIndex != -1 && npcBuyIndex < npc.saleItems.Length) {
                var item = npc.saleItems[npcBuyIndex];

                // make valid amount
                int amount = npcTradingBuyAmount.text.ToInt();
                amount = Mathf.Clamp(amount, 1, item.maxStack);
                npcTradingBuyAmount.text = amount.ToString();

                // image
                npcTradingBuy.GetComponent<Image>().color = Color.white;
                npcTradingBuy.GetComponent<Image>().sprite = item.image;

                // tooltip
                npcTradingBuy.GetComponent<UIShowToolTip>().enabled = true;
                npcTradingBuy.GetComponent<UIShowToolTip>().text = item.Tooltip(showBuyPrice:true);

                // price
                long price = amount * item.buyPrice;
                npcTradingBuyCosts.text = price.ToString();

                // button
                npcTradingBuyButton.interactable = amount > 0 && price <= player.gold;
                npcTradingBuyButton.onClick.SetListener(() => {
                    player.CmdNpcBuyItem(npcBuyIndex, amount);
                    npcBuyIndex = -1;
                    npcTradingBuyAmount.text = "1";
                });
            } else {
                // image
                npcTradingBuy.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                npcTradingBuy.GetComponent<Image>().sprite = null;

                // tooltip
                npcTradingBuy.GetComponent<UIShowToolTip>().enabled = false;

                // price
                npcTradingBuyCosts.text = "0";

                // button
                npcTradingBuyButton.interactable = false;
            }

            // sell
            if (npcSellIndex != -1 && npcSellIndex < player.inventory.Count) {
                var item = player.inventory[npcSellIndex];

                // make valid amount
                int amount = npcTradingSellAmount.text.ToInt();
                amount = Mathf.Clamp(amount, 1, item.amount);
                npcTradingSellAmount.text = amount.ToString();

                // image
                npcTradingSell.GetComponent<Image>().color = Color.white;
                npcTradingSell.GetComponent<Image>().sprite = item.image;

                // tooltip
                npcTradingSell.GetComponent<UIShowToolTip>().enabled = true;
                npcTradingSell.GetComponent<UIShowToolTip>().text = item.Tooltip(showSellPrice:true);

                // price
                long price = amount * item.sellPrice;
                npcTradingSellCosts.text = price.ToString();

                // button
                npcTradingSellButton.interactable = amount > 0;
                npcTradingSellButton.onClick.SetListener(() => {
                    player.CmdNpcSellItem(npcSellIndex, amount);
                    npcSellIndex = -1;
                    npcTradingSellAmount.text = "1";
                });
            } else {
                // image
                npcTradingSell.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                npcTradingSell.GetComponent<Image>().sprite = null;

                // tooltip
                npcTradingSell.GetComponent<UIShowToolTip>().enabled = true;

                // price
                npcTradingSellCosts.text = "0";

                // button
                npcTradingSellButton.interactable = false;
            }
        } else {
            // hide
            npcTradingPanel.SetActive(false);
        }
    }

    void UpdatePlayerTradingRequest(Player player) {
        // only if there is a request
        if (player.tradeRequestFrom != "") {
            playerTradingRequestPanel.SetActive(true);
            // name
            playerTradingRequestName.text = player.tradeRequestFrom;

            // button accept
            playerTradingRequestAccept.onClick.SetListener(() => {
                player.CmdTradeRequestAccept();
            });

            // button decline
            playerTradingRequestDecline.onClick.SetListener(() => {
                player.CmdTradeRequestDecline();
            });
        } else {
            playerTradingRequestPanel.SetActive(false);
        }            
    }

    void UpdatePlayerTrading(Player player) {
        // only if trading, otherwise set inactive
        if (player.state == "TRADING" && player.target != null && player.target is Player) {
            var other = (Player)player.target;

            playerTradingPanel.SetActive(true);

            // OTHER
            // status text
            if (other.tradeOfferAccepted) playerTradingOtherStatus.text = "[ACCEPTED]";
            else if (other.tradeOfferLocked) playerTradingOtherStatus.text = "[LOCKED]";
            else playerTradingOtherStatus.text = "";
            // gold input
            playerTradingOtherGold.text = other.tradeOfferGold.ToString();
            // items
            for (int i = 0; i < playerTradingOtherContent.childCount; ++i) {
                var entry = playerTradingOtherContent.GetChild(i).GetChild(0);

                // get the inventory index
                if (i < other.tradeOfferItems.Count) {
                    // get the item (if valid index)
                    var idx = other.tradeOfferItems[i];
                    if (0 <= idx && idx < other.inventory.Count && other.inventory[idx].valid) {
                        var item = other.inventory[idx];
                        
                        // set state
                        entry.GetComponent<UIShowToolTip>().enabled = true;

                        // image
                        entry.GetComponent<Image>().color = Color.white;
                        entry.GetComponent<Image>().sprite = item.image;
                        entry.GetComponent<UIShowToolTip>().text = item.Tooltip(showSellPrice:true);

                        // amount overlay
                        var amount = item.amount;
                        entry.transform.GetChild(0).gameObject.SetActive(amount > 1);
                        if (amount > 1) entry.GetComponentInChildren<Text>().text = amount.ToString();
                    } else {
                        // remove listeners
                        entry.GetComponent<Button>().onClick.RemoveAllListeners();

                        // set state
                        entry.GetComponent<UIShowToolTip>().enabled = false;

                        // image
                        entry.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                        entry.GetComponent<Image>().sprite = null;

                        // amount overlay
                        entry.transform.GetChild(0).gameObject.SetActive(false);
                    }
                }
            }
            if (other.tradeOfferItems.Count > playerTradingOtherContent.childCount)
                Debug.LogWarning("not enough UI slots for other sale items");

            // SELF
            // status text
            if (player.tradeOfferAccepted) playerTradingMyStatus.text = "[ACCEPTED]";
            else if (player.tradeOfferLocked) playerTradingMyStatus.text = "[LOCKED]";
            else playerTradingMyStatus.text = "";
            // gold input
            if (player.tradeOfferLocked) {
                playerTradingMyGold.interactable = false;
                playerTradingMyGold.text = player.tradeOfferGold.ToString();
            } else {
                playerTradingMyGold.interactable = true;
                playerTradingMyGold.onValueChanged.SetListener((val) => {
                    var n = Utils.ClampLong(val.ToLong(), 0, player.gold);
                    playerTradingMyGold.text = n.ToString();
                    player.CmdTradeOfferGold(n);
                });
            }
            // items
            for (int i = 0; i < playerTradingMyContent.childCount; ++i) {
                var entry = playerTradingMyContent.GetChild(i).GetChild(0);

                // get the inventory index
                if (i < player.tradeOfferItems.Count) {
                    // get the item (if valid index)
                    var idx = player.tradeOfferItems[i];
                    if (0 <= idx && idx < player.inventory.Count && player.inventory[idx].valid) {
                        var item = player.inventory[idx];
                        
                        // set state
                        entry.GetComponent<UIShowToolTip>().enabled = true;
                        entry.GetComponent<UIDragAndDropable>().dragable = !player.tradeOfferLocked;
                        // note: entries should be dropable at all times

                        // image
                        entry.GetComponent<Image>().color = Color.white;
                        entry.GetComponent<Image>().sprite = item.image;
                        entry.GetComponent<UIShowToolTip>().text = item.Tooltip(showSellPrice:true);

                        // amount overlay
                        var amount = item.amount;
                        entry.transform.GetChild(0).gameObject.SetActive(amount > 1);
                        if (amount > 1) entry.GetComponentInChildren<Text>().text = amount.ToString();
                    } else {
                        // remove listeners
                        entry.GetComponent<Button>().onClick.RemoveAllListeners();

                        // set state
                        entry.GetComponent<UIShowToolTip>().enabled = false;
                        entry.GetComponent<UIDragAndDropable>().dragable = false;

                        // image
                        entry.GetComponent<Image>().color = new Color(0,0,0,0); // transparent
                        entry.GetComponent<Image>().sprite = null;

                        // amount overlay
                        entry.transform.GetChild(0).gameObject.SetActive(false);
                    }
                }
            }
            if (player.tradeOfferItems.Count > playerTradingMyContent.childCount)
                Debug.LogWarning("not enough UI slots for own sale items");

            // button lock
            playerTradingLock.interactable = !player.tradeOfferLocked;
            playerTradingLock.onClick.SetListener(() => {
                player.CmdTradeOfferLock();
            });
            
            // button accept (only if both have locked the trade and if not
            // accepted yet)
            playerTradingAccept.interactable = player.tradeOfferLocked && other.tradeOfferLocked && !player.tradeOfferAccepted;
            playerTradingAccept.onClick.SetListener(() => {
                player.CmdTradeOfferAccept();
            });

            // button cancel
            playerTradingCancel.onClick.SetListener(() => {
                player.CmdTradeCancel();
            });
        } else {
            playerTradingPanel.SetActive(false);
            playerTradingMyGold.text = "0"; // reset
        }
    }

    void Update() {
        // find the local player (if any)
        if (ClientScene.localPlayers.Count > 0) {
            var go = ClientScene.localPlayers[0].gameObject;
            // it's sometimes null after disconnecting
            if (go != null) {
                var player = go.GetComponent<Player>();

                UpdateChat(player);
                UpdateExperienceBar(player);
                UpdateHealthMana(player);
                UpdateMinimap(player);
                UpdateTarget(player);
                UpdateCharacterInfo(player);
                UpdateRespawn(player);
                UpdateInventory(player);
                UpdateEquipment(player);
                UpdateSkills(player);
                UpdateSkillbar(player);
                UpdateBuffs(player);
                UpdateLoot(player);
                UpdateQuests(player);
                UpdateNpcDialogue(player);
                UpdateNpcQuest(player);
                UpdateNpcTrading(player);
                UpdatePlayerTradingRequest(player);
                UpdatePlayerTrading(player);
            }
        }
    }

    public void ShowLoot() {
        lootPanel.SetActive(true);
    }

    public void ShowNpcDialogue() {
        npcDialoguePanel.SetActive(true);
    }
}
