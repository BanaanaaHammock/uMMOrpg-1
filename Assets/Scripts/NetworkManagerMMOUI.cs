// Updates the UI to the current NetworkManager state.
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class NetworkManagerMMOUI : MonoBehaviour {
    [Header("Login Mask")]
    [SerializeField] GameObject loginPanel;
    [SerializeField] Text loginStatus;
    [SerializeField] InputField inputName;
    [SerializeField] InputField inputPass;
    [SerializeField] Button btnLogin;
    [SerializeField] Button btnHost;
    [SerializeField] Button btnDedicated;

    [Header("Character Selection")]
    [SerializeField] GameObject characterSelectionPanel;
    [SerializeField] Transform characterSelectionContent;
    // available characters (set after receiving the message from the server)
    [HideInInspector] public string[] characters;

    [Header("Character Creation")]
    [SerializeField] GameObject characterCreationPanel;
    [SerializeField] InputField inputCharacterName;
    [SerializeField] Dropdown dropdownClass;
    [SerializeField] Text dropdownCurrent;

    [Header("Popup")]
    [SerializeField] GameObject popupPanel;
    [SerializeField] Text popupMessage;

    // cache
    NetworkManagerMMO manager;

    void Awake() {
        manager = GetComponent<NetworkManagerMMO>();
    }

    void Update() {
        UpdateLoginMask();
        UpdateCharacterSelection();
        UpdateCharacterCreation();
    }

    public void ShowPopup(string msg) {
        // already shown? then simply add error to it (we only have 1 popup)
        if (popupPanel.activeSelf) {
            popupMessage.text += ";\n" + msg;
        // otherwise show and set new text
        } else {
            popupPanel.SetActive(true);
            popupMessage.text = msg;
        }
    }

    // LOGIN MASK //////////////////////////////////////////////////////////////
    void UpdateLoginMask() {
        // only update if visible
        if (!loginPanel.activeSelf) return;

        // status
        loginStatus.text = manager.IsConnecting() ? "Connecting..." : "";

        // button login
        btnLogin.interactable = !manager.IsConnecting();
        btnLogin.onClick.SetListener(() => { manager.StartClient(); });

        // button host
        btnHost.interactable = !manager.IsConnecting();
        btnHost.onClick.SetListener(() => { manager.StartHost(); });

        // button dedicated
        btnDedicated.interactable = !manager.IsConnecting();
        btnDedicated.onClick.SetListener(() => { manager.StartServer(); });

        // inputs
        manager.id = inputName.text;
        manager.pw = inputPass.text;
    }

    public void HideLoginMask() {
        loginPanel.SetActive(false);
    }

    public void ShowLoginMask() {
        loginPanel.SetActive(true);       
    }

    // CHARACTER SELECTION /////////////////////////////////////////////////////
    void UpdateCharacterSelection() {
        // only update if visible
        if (!characterSelectionPanel.activeSelf) return;

        // hide if disconnected
        if (!NetworkClient.active) HideCharacterSelection();

        // disable character selection as soon as a player is in the world
        if (IsLocalPlayerInWorld()) HideCharacterSelection();

        // update UI
        for (int i = 0; i < characterSelectionContent.childCount; ++i) {
            var entry = characterSelectionContent.GetChild(i);

            // get the character
            if (i < characters.Length) {
                entry.gameObject.SetActive(true);

                var txt = entry.GetChild(0).GetComponent<Text>();
                txt.text = characters[i];
                
                var btnSelect = entry.GetChild(1).GetComponent<Button>();
                btnSelect.interactable = characters.Length < manager.charLimit;
                int icopy = i; // needed for lambdas, otherwise i is Count
                btnSelect.onClick.SetListener(() => {
                    // use ClientScene.AddPlayer with a parameter, which calls
                    // OnServerAddPlayer on the server.
                    var msg = new CharacterSelectMsg();
                    msg.index = icopy;
                    ClientScene.AddPlayer(manager.client.connection, 0, msg);
                });
                
                var btnDelete = entry.GetChild(2).GetComponent<Button>();
                btnDelete.onClick.SetListener(() => {
                    // send delete message
                    var msg = new CharacterDeleteMsg();
                    msg.index = icopy;
                    manager.client.Send(CharacterDeleteMsg.MsgId, msg);
                });
            } else {
                entry.gameObject.SetActive(false);
            }
        }
        if (characters.Length > characterSelectionContent.childCount)
            Debug.LogWarning("not enough UI slots for character selection");
    }

    bool IsLocalPlayerInWorld() {
        // note: ClientScene.localPlayers.Count cant be used as check because
        // nothing is removed from that list, even after disconnect. It still
        // contains entries like: ID=0 NetworkIdentity NetID=null Player=null
        // (which might be a UNET bug)
        foreach (PlayerController pc in ClientScene.localPlayers)
            if (pc.gameObject != null)
                return true;
        return false;
    }

    public void HideCharacterSelection() {
        characterSelectionPanel.SetActive(false);
    }

    public void ShowCharacterSelection() {
        characterSelectionPanel.SetActive(true);
    }

    public void OnButtonCharacterSelectionCreate() {
        HideCharacterSelection();
        ShowCharacterCreation();
    }

    // quit function used by several quit buttons //////////////////////////////
    public void OnButtonQuit() {
        manager.StopClient();
        Application.Quit();
    }

    // CHARACTER CREATION //////////////////////////////////////////////////////
    void UpdateCharacterCreation() {
        // hide if disconnected
        if (!NetworkClient.active) HideCharacterCreation();

        // copy player classes to class selection
        dropdownClass.options.Clear();
        foreach (var p in manager.GetPlayerClasses())
            dropdownClass.options.Add(new Dropdown.OptionData(p.name));
        // we also have to refresh the current text, otherwise it's still
        // 'Option A'
        var idx = dropdownClass.value;
        dropdownCurrent.text = dropdownClass.options[idx].text;
    }

    public void HideCharacterCreation() {
        characterCreationPanel.SetActive(false);
    }

    public void ShowCharacterCreation() {
        characterCreationPanel.SetActive(true);
    }

    public void OnButtonCharacterCreationCreate() {
        var msg = new CharacterCreateMsg();
        msg.name = inputCharacterName.text;
        msg.classIndex = dropdownClass.value;
        manager.client.Send(CharacterCreateMsg.MsgId, msg);
    }

    public void OnButtonCharacterCreationCancel() {
        inputCharacterName.text = "";
        HideCharacterCreation();
        ShowCharacterSelection();
    }
}
