using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DaksshDev.Toaster;

public class UserEdit : MonoBehaviour
{
    [CoolHeader("User Edit (SettingsDrawer)")]

    [Space]
    [Header("Display References")]
    public TMP_Text UsernameDisplay;
    public TMP_Text ClassDisplay;
    public TMP_Text AccountCreationText;

    [Header("Edit UI")]
    public TMP_InputField UsernameInput;
    public TMP_Dropdown   ClassDropdown;
    public Button         EditUsernameButton;
    public Button         EditClassButton;
    public TMP_Text       EditUsernameButtonText;
    public TMP_Text       EditClassButtonText;
    public GameObject     EditUsernameIcon;
    public GameObject     EditClassIcon;

    [Header("Cancel Panel")]
    public GameObject CancelPanel;

    [Header("Guest State")]
    [Tooltip("Button or panel to show when user is a guest (prompts sign-up)")]
    public GameObject signUpPromptObject;

    private bool isEditingUsername = false;
    private bool isEditingClass    = false;

    // ==========================================================================
    //  Lifecycle
    // ==========================================================================

    void Start()
    {
        SetupEditButtons();

        if (CancelPanel != null)
        {
            Button cancelButton = CancelPanel.GetComponent<Button>();
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancelEdit);
        }

        if (UserDataService.Instance != null)
            UserDataService.Instance.OnUserDataLoaded.AddListener(OnDataLoaded);

        if (Onboarding.IsOnboarded)
            UserDataService.Instance?.RefreshAllListeners();
    }

    void OnDestroy()
    {
        if (UserDataService.Instance != null)
            UserDataService.Instance.OnUserDataLoaded.RemoveListener(OnDataLoaded);
    }

    // ==========================================================================
    //  Data
    // ==========================================================================

    private void OnDataLoaded(UserDataService.UserDataPayload data)
    {
        if (!isEditingUsername) UsernameDisplay.text = data.username;
        if (!isEditingClass)    ClassDisplay.text    = data.userClass;

        if (AccountCreationText != null)
            AccountCreationText.text = string.IsNullOrEmpty(data.createdAt)
                ? "Creation date unavailable offline"
                : data.createdAt;

        if (signUpPromptObject != null)
            signUpPromptObject.SetActive(data.isGuest);
    }

    public void LoadUserData()
    {
        if (!Onboarding.IsOnboarded)
        {
            ShowError("Onboarding not complete.");
            return;
        }

        ResetEditState();
        UserDataService.Instance?.RefreshAllListeners();
    }

    // ==========================================================================
    //  Button Setup
    // ==========================================================================

    void SetupEditButtons()
    {
        EditUsernameButton.onClick.AddListener(OnEditUsernameClick);
        EditClassButton.onClick.AddListener(OnEditClassClick);
    }

    // ==========================================================================
    //  Username Edit
    // ==========================================================================

    void OnEditUsernameClick()
    {
        if (!(UserDataService.Instance?.IsOnline ?? false))
        {
            ShowError("No internet. Editing is disabled.");
            return;
        }

        if (!isEditingUsername)
        {
            isEditingUsername           = true;
            UsernameInput.interactable  = true;
            UsernameInput.text          = UsernameDisplay.text;
            EditUsernameButtonText.text = "Save";
            if (EditUsernameIcon != null) EditUsernameIcon.SetActive(false);
            if (CancelPanel      != null) CancelPanel.SetActive(true);
        }
        else
        {
            SaveUsername();
        }
    }

    void SaveUsername()
    {
        string newUsername = UsernameInput.text.Trim();
        if (string.IsNullOrEmpty(newUsername))
        {
            ShowError("Username can't be empty.");
            return;
        }

        string currentClass = UserDataService.Instance?.UserClass ?? "";
        UserDataService.Instance?.SaveProfile(newUsername, currentClass);

        UsernameDisplay.text = newUsername;
        ResetUsernameEdit();
    }

    // ==========================================================================
    //  Class Edit
    // ==========================================================================

    void OnEditClassClick()
    {
        if (!(UserDataService.Instance?.IsOnline ?? false))
        {
            ShowError("No internet. Editing is disabled.");
            return;
        }

        if (!isEditingClass)
        {
            isEditingClass = true;
            ClassDropdown.gameObject.SetActive(true);
            ClassDropdown.interactable = true;

            string current = ClassDisplay.text;
            for (int i = 0; i < ClassDropdown.options.Count; i++)
            {
                if (ClassDropdown.options[i].text == current)
                {
                    ClassDropdown.value = i;
                    break;
                }
            }

            EditClassButtonText.text = "Save";
            if (EditClassIcon != null) EditClassIcon.SetActive(false);
            if (CancelPanel   != null) CancelPanel.SetActive(true);
        }
        else
        {
            SaveClass();
        }
    }

    void SaveClass()
    {
        string newClass = ClassDropdown.options[ClassDropdown.value].text;
        if (string.IsNullOrEmpty(newClass) || newClass == "Select Class")
        {
            ShowError("Please select a valid class.");
            return;
        }

        string currentUsername = UserDataService.Instance?.Username ?? "";
        UserDataService.Instance?.SaveProfile(currentUsername, newClass);

        ClassDisplay.text = newClass;
        ResetClassEdit();
    }

    // ==========================================================================
    //  Cancel / Reset
    // ==========================================================================

    void OnCancelEdit()
    {
        if (isEditingUsername) ResetUsernameEdit();
        if (isEditingClass)    ResetClassEdit();
    }

    void ResetEditState()
    {
        if (isEditingUsername) ResetUsernameEdit();
        if (isEditingClass)    ResetClassEdit();
    }

    void ResetUsernameEdit()
    {
        UsernameInput.text          = "";
        UsernameInput.interactable  = false;
        isEditingUsername           = false;
        EditUsernameButtonText.text = "Edit";
        if (EditUsernameIcon != null) EditUsernameIcon.SetActive(true);
        if (!isEditingClass && CancelPanel != null) CancelPanel.SetActive(false);
    }

    void ResetClassEdit()
    {
        ClassDropdown.gameObject.SetActive(false);
        ClassDropdown.interactable  = false;
        isEditingClass              = false;
        EditClassButtonText.text    = "Edit";
        if (EditClassIcon != null) EditClassIcon.SetActive(true);
        if (!isEditingUsername && CancelPanel != null) CancelPanel.SetActive(false);
    }

    // ==========================================================================
    //  Toast Error
    // ==========================================================================

    void ShowError(string message)
    {
        if (ToastManager.Instance != null)
            ToastManager.Instance.ShowError(message);
        else
            Debug.LogWarning("[UserEdit] ToastManager instance not found. Error: " + message);
    }
}