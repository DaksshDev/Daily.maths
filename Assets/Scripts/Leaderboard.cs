using UnityEngine;
using TMPro;
using Firebase.Firestore;
using Firebase.Extensions;
using System.Collections.Generic;
using System.Linq;

public class Leaderboard : MonoBehaviour
{
    [CoolHeader("Simple Leaderboard")]

    [Space]
    [Header("References")]
    public GameObject userItemPrefab;
    public Transform  scrollListContent;

    [Header("Login Gate UI")]
    [Tooltip("Panel shown to guests instead of the leaderboard")]
    public GameObject loginRequiredPanel;
    [Tooltip("Button inside loginRequiredPanel that triggers sign-up")]
    public GameObject signUpButton;

    private FirebaseFirestore db;

    [System.Serializable]
    public class UserData
    {
        public string username;
        public int    streak;
        public int    level;

        public UserData(string username, int streak, int level)
        {
            this.username = username;
            this.streak   = streak;
            this.level    = level;
        }
    }

    // ==========================================================================

    void Start()
    {
        db = FirebaseFirestore.DefaultInstance;

        // Subscribe to auth state changes so UI auto-updates if user signs in
        if (UserDataService.Instance != null)
            UserDataService.Instance.OnAuthStateChanged.AddListener(OnAuthStateChanged);
    }

    void OnDestroy()
    {
        if (UserDataService.Instance != null)
            UserDataService.Instance.OnAuthStateChanged.RemoveListener(OnAuthStateChanged);
    }

    void OnEnable()
    {
        // Re-check every time this panel becomes visible
        TryLoadLeaderboard();
    }

    // ==========================================================================

    private void OnAuthStateChanged(UserDataService.UserAuthState state)
    {
        // If user just signed in while leaderboard is open, load it automatically
        if (state != UserDataService.UserAuthState.Guest && gameObject.activeInHierarchy)
            TryLoadLeaderboard();
    }

    /// <summary>
    /// Entry point. Checks auth state before touching Firebase.
    /// </summary>
    public void TryLoadLeaderboard()
    {
        bool isGuest = UserDataService.Instance?.IsGuest ?? true;

        if (isGuest)
        {
            ShowLoginGate();
            return;
        }

        HideLoginGate();
        LoadLeaderboard();
    }

    private void ShowLoginGate()
    {
        if (loginRequiredPanel != null) loginRequiredPanel.SetActive(true);
        if (scrollListContent  != null) scrollListContent.gameObject.SetActive(false);
        ClearLeaderboard();
    }

    private void HideLoginGate()
    {
        if (loginRequiredPanel != null) loginRequiredPanel.SetActive(false);
        if (scrollListContent  != null) scrollListContent.gameObject.SetActive(true);
    }

    public void LoadLeaderboard()
    {
        ClearLeaderboard();

        db.Collection("users").GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("Failed to fetch leaderboard: " + task.Exception);
                return;
            }

            var usersList = new List<UserData>();

            foreach (DocumentSnapshot doc in task.Result.Documents)
            {
                var data = doc.ToDictionary();

                // Only show users who completed onboarding (have a username)
                if (!data.ContainsKey("username")) continue;

                string username = data["username"].ToString();
                int    streak   = data.ContainsKey("streak") ? System.Convert.ToInt32(data["streak"]) : 0;
                int    level    = data.ContainsKey("level")  ? System.Convert.ToInt32(data["level"])  : 0;

                usersList.Add(new UserData(username, streak, level));
            }

            usersList = usersList.OrderByDescending(u => u.streak).ToList();
            DisplayLeaderboard(usersList);
        });
    }

    private void DisplayLeaderboard(List<UserData> usersList)
    {
        for (int i = 0; i < usersList.Count; i++)
        {
            UserData user     = usersList[i];
            int      position = i + 1;

            GameObject userItem = Instantiate(userItemPrefab, scrollListContent);

            SetChildText(userItem, "name",     user.username);
            SetChildText(userItem, "position", GetPositionText(position));

            Transform streakParent = userItem.transform.Find("streak");
            if (streakParent != null)
                SetChildText(streakParent.gameObject, "text", user.streak.ToString());

            Transform levelParent = userItem.transform.Find("level");
            if (levelParent != null)
                SetChildText(levelParent.gameObject, "text", user.level.ToString());

            Transform honor = userItem.transform.Find("honor");
            if (honor != null) honor.gameObject.SetActive(position <= 3);
        }
    }

    // ==========================================================================
    //  Helpers
    // ==========================================================================

    private void SetChildText(GameObject parent, string childName, string value)
    {
        Transform child = parent.transform.Find(childName);
        if (child == null) return;
        TMP_Text txt = child.GetComponent<TMP_Text>();
        if (txt != null) txt.text = value;
    }

    private string GetPositionText(int position)
    {
        if (position == 1) return "1st";
        if (position == 2) return "2nd";
        if (position == 3) return "3rd";
        return position.ToString();
    }

    private void ClearLeaderboard()
    {
        foreach (Transform child in scrollListContent)
            Destroy(child.gameObject);
    }

    public void RefreshLeaderboard() => TryLoadLeaderboard();
}