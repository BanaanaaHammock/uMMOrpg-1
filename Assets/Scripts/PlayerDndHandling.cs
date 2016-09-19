// Takes care of Drag and Drop events for the player.
using UnityEngine;
using UnityEngine.Networking;
using System;

[RequireComponent(typeof(Player))]
public class PlayerDndHandling : NetworkBehaviour {
    // cache
    Player player;

    // networkbehaviour ////////////////////////////////////////////////////////
    [ClientCallback]
    void Awake() {
        player = GetComponent<Player>();
    }

    // message sent from UI
    [Client]
    void OnDragAndDrop(UIDragAndDropable[] entries) {
        if (!isLocalPlayer) return;

        print("OnDragAndDrop:" + entries[0].tag + "=>" + entries[1].tag);

        // get the corresponding synclist indices from their names
        int from = entries[0].name.ToInt();
        int to = entries[1].name.ToInt();

        // call one of the dnd_slot_slot functions
        SendMessage("OnDnd_" + entries[0].tag + "_" + entries[1].tag, new int[]{from, to}, SendMessageOptions.DontRequireReceiver);
    }

    // message sent from UI
    [Client]
    void OnDragAndClear(UIDragAndDropable entry) {
        if (!isLocalPlayer) return;

        // get the corresponding synclist index from the name
        int from = entry.name.ToInt();

        // clear it for some slot types
        if (entry.tag == "SkillbarSlot") player.skillbar[from] = "";
        if (entry.tag == "TradingSlot") player.CmdTradeOfferItemClear(from);
    }

    [Client]
    void OnDnd_InventorySlot_InventorySlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo

        // split? (just check the name, rest is done server sided)
        if (player.inventory[slotIndices[0]].valid && player.inventory[slotIndices[1]].valid &&
            player.inventory[slotIndices[0]].name == player.inventory[slotIndices[1]].name) {
            player.CmdInventoryMerge(slotIndices[0], slotIndices[1]);
        // merge?
        } else if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) {
            player.CmdInventorySplit(slotIndices[0], slotIndices[1]);
        // swap?
        } else {
            player.CmdSwapInventoryInventory(slotIndices[0], slotIndices[1]);
        }
    }
    
    [Client]
    void OnDnd_InventorySlot_TrashSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.CmdSwapInventoryTrash(slotIndices[0]);
    }

    [Client]
    void OnDnd_InventorySlot_EquipmentSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.CmdSwapInventoryEquip(slotIndices[0], slotIndices[1]);
    }

    [Client]
    void OnDnd_InventorySlot_SkillbarSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.skillbar[slotIndices[1]] = player.inventory[slotIndices[0]].name; // just save it clientsided
    }

    [Client]
    void OnDnd_InventorySlot_NpcSellSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        FindObjectOfType<UIRefresh>().npcSellIndex = slotIndices[0];
        FindObjectOfType<UIRefresh>().npcTradingSellAmount.text = player.inventory[slotIndices[0]].amount.ToString();
    }

    [Client]
    void OnDnd_InventorySlot_TradingSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.CmdTradeOfferItem(slotIndices[0], slotIndices[1]);
    }

    [Client]
    void OnDnd_TrashSlot_InventorySlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.CmdSwapTrashInventory(slotIndices[1]);
    }

    [Client]
    void OnDnd_EquipmentSlot_InventorySlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.CmdSwapInventoryEquip(slotIndices[1], slotIndices[0]); // reversed
    }

    [Client]
    void OnDnd_EquipmentSlot_SkillbarSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.skillbar[slotIndices[1]] = player.equipment[slotIndices[0]].name; // just save it clientsided
    }

    [Client]
    void OnDnd_SkillsSlot_SkillbarSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        player.skillbar[slotIndices[1]] = player.skills[slotIndices[0]].name; // just save it clientsided
    }

    [Client]
    void OnDnd_SkillbarSlot_SkillbarSlot(int[] slotIndices) {
        // slotIndices[0] = slotFrom; slotIndices[1] = slotTo
        // just swap them clientsided
        var temp = player.skillbar[slotIndices[0]];
        player.skillbar[slotIndices[0]] = player.skillbar[slotIndices[1]];
        player.skillbar[slotIndices[1]] = temp;
    }
}
