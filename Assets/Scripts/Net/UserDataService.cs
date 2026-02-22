using UnityEngine;
using UnityEngine.Events;
using Firebase.Firestore;
using Firebase.Extensions;
using System;
using System.Collections.Generic;

/// <summary>
/// Single source of truth for all user data.
/// Guest play is allowed — Firebase is optional until leaderboard/social features.
/// </summary>
public class UserDataService : MonoBehaviour
{
    public static UserDataService Instance { get; private set; }

    // ── Auth State ────────────────────────────────────────────────────────────
    public enum UserAuthState
    {
        Guest,          // playing locally, no Firebase account — totally fine
        Authenticated,  // has Firebase UID, may or may not be onboarded
        Ready           // fully onboarded + synced
    }

    public UserAuthState AuthState { get; private set; } = UserAuthState.Guest;

    // ── Events ────────────────────────────────────────────────────────────────
    [System.Serializable] public class UserDataEvent  : UnityEvent<UserDataPayload> {}
    [System.Serializable] public class AuthStateEvent : UnityEvent<UserAuthState> {}

    [Header("Events")]
    public UserDataEvent  OnUserDataLoaded;
    public UnityEvent     OnUserDataSaved;
    public UnityEvent     OnOfflineLoad;
    public AuthStateEvent OnAuthStateChanged;
    public UnityEvent     OnCloudSyncRequired; // fired when user tries a cloud-only feature as Guest

    // ── Cached Data ───────────────────────────────────────────────────────────
    public string Username  { get; private set; } = "Player";
    public string UserClass { get; private set; } = "";
    public int    Streak    { get; private set; } = 0;
    public int    Coins     { get; private set; } = 0;
    public int    XP        { get; private set; } = 0;
    public int    Level     { get; private set; } = 0;
    public string UserId    { get; private set; } = "";
    public string CreatedAt { get; private set; } = "";
    public bool   IsOnline  => Application.internetReachability != NetworkReachability.NotReachable;
    public bool   IsGuest   => AuthState == UserAuthState.Guest;
    public bool   DataLoaded { get; private set; } = false;

    private FirebaseFirestore db;

    // ── Payload ───────────────────────────────────────────────────────────────
    [System.Serializable]
    public class UserDataPayload
    {
        public string username;
        public string userClass;
        public int    streak;
        public int    coins;
        public int    xp;
        public int    level;
        public string createdAt;
        public bool   isGuest;
    }

    // ==========================================================================
    //  Lifecycle
    // ==========================================================================

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        db     = FirebaseFirestore.DefaultInstance;
        UserId = PlayerPrefs.GetString("SavedUserId", "");

        bool hasAccount  = PlayerPrefs.GetInt("AnonUserLoggedIn",  0) == 1;
        bool hasOnboarded = PlayerPrefs.GetInt("OnboardingComplete", 0) == 1;

        if      (hasOnboarded) SetAuthState(UserAuthState.Ready);
        else if (hasAccount)   SetAuthState(UserAuthState.Authenticated);
        else                   SetAuthState(UserAuthState.Guest);
    }

    // ==========================================================================
    //  Public API
    // ==========================================================================

    /// <summary>
    /// Fetch data. Guests load from PlayerPrefs immediately — no blocking, no errors.
    /// Authenticated/Ready users fetch from Firebase with PlayerPrefs fallback.
    /// </summary>
    public void FetchUserData()
    {
        if (IsGuest)
        {
            LoadFromPlayerPrefs(); // guests always have local data available
            return;
        }

        if (string.IsNullOrEmpty(UserId)) { LoadFromPlayerPrefs(); return; }
        if (IsOnline) FetchFromFirebase();
        else          LoadFromPlayerPrefs();
    }

    /// <summary>
    /// Re-fire event with cached data. Falls through to FetchUserData if cold.
    /// </summary>
    public void RefreshAllListeners()
    {
        if (!DataLoaded) { FetchUserData(); return; }
        FireUserDataEvent();
    }

    /// <summary>
    /// Check if user can access a cloud feature (leaderboard etc).
    /// Returns false and fires OnCloudSyncRequired if guest.
    /// </summary>
    public bool RequireCloudAccess()
    {
        if (IsGuest)
        {
            OnCloudSyncRequired?.Invoke();
            return false;
        }
        return true;
    }

    /// <summary>
    /// Called by SignUp after anonymous auth succeeds.
    /// Guest → Authenticated. Local progress is preserved.
    /// </summary>
    public void SetUserId(string userId)
    {
        UserId = userId;
        PlayerPrefs.SetString("SavedUserId", userId);
        PlayerPrefs.Save();
        SetAuthState(UserAuthState.Authenticated);
    }

    /// <summary>Called by Onboarding after completion. Authenticated → Ready.</summary>
    public void MarkOnboardingComplete()
    {
        SetAuthState(UserAuthState.Ready);
    }

    /// <summary>Add XP + Coins at end of session. Works for guests too — stored locally.</summary>
    public void CommitSessionScore(int gainedXP, int gainedCoins)
    {
        XP    += gainedXP;
        Coins += gainedCoins;

        PlayerPrefs.SetInt("xp",    XP);
        PlayerPrefs.SetInt("coins", Coins);
        PlayerPrefs.Save();

        // Guests stop here — local only
        if (IsGuest || !IsOnline || string.IsNullOrEmpty(UserId))
        {
            FireUserDataEvent();
            return;
        }

        db.Collection("users").Document(UserId)
          .GetSnapshotAsync()
          .ContinueWithOnMainThread(task =>
          {
              if (!task.IsCompleted || task.IsFaulted) { FireUserDataEvent(); return; }

              var data    = task.Result.ToDictionary();
              int fbXP    = data.ContainsKey("xp")    ? Convert.ToInt32(data["xp"])    : 0;
              int fbCoins = data.ContainsKey("coins")  ? Convert.ToInt32(data["coins"]) : 0;

              db.Collection("users").Document(UserId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "xp",    fbXP    + gainedXP    },
                    { "coins", fbCoins + gainedCoins }
                })
                .ContinueWithOnMainThread(_ =>
                {
                    OnUserDataSaved?.Invoke();
                    FireUserDataEvent();
                });
          });
    }

    /// <summary>Increment streak. Works for guests locally.</summary>
    public void IncrementStreak()
    {
        Streak++;
        PlayerPrefs.SetInt("streak", Streak);
        PlayerPrefs.Save();

        if (!IsGuest && IsOnline && !string.IsNullOrEmpty(UserId))
        {
            db.Collection("users").Document(UserId)
              .UpdateAsync("streak", Streak)
              .ContinueWithOnMainThread(_ => OnUserDataSaved?.Invoke());
        }

        FireUserDataEvent();
    }

    /// <summary>Save calculated level.</summary>
    public void SaveLevel(int level)
    {
        Level = level;
        PlayerPrefs.SetInt("currentLevel", level);
        PlayerPrefs.Save();

        if (!IsGuest && IsOnline && !string.IsNullOrEmpty(UserId))
        {
            db.Collection("users").Document(UserId)
              .UpdateAsync(new Dictionary<string, object> { { "level", level } })
              .ContinueWithOnMainThread(_ => OnUserDataSaved?.Invoke());
        }

        FireUserDataEvent();
    }
    
    /// <summary>
    /// Called by SignUp after anonymous auth succeeds.
    /// Creates the Firestore document with ALL local guest data
    /// (including username + class saved during guest onboarding),
    /// then transitions Guest → Authenticated.
    /// </summary>
    public void RegisterNewAccount(string userId)
    {
        UserId = userId;
        PlayerPrefs.SetString("SavedUserId", userId);
        PlayerPrefs.Save();

        SetAuthState(UserAuthState.Authenticated);

        if (!IsOnline) return;

        // Pull everything from PlayerPrefs — guest onboarding already saved these
        bool localOnboarded = PlayerPrefs.GetInt("OnboardingComplete", 0) == 1;

        var userData = new Dictionary<string, object>
        {
            { "username",           PlayerPrefs.GetString("username",     "Player") },
            { "class",              PlayerPrefs.GetString("class",        "")       },
            { "xp",                 PlayerPrefs.GetInt   ("xp",           0)        },
            { "coins",              PlayerPrefs.GetInt   ("coins",        0)        },
            { "streak",             PlayerPrefs.GetInt   ("streak",       0)        },
            { "level",              PlayerPrefs.GetInt   ("currentLevel", 0)        },
            { "OnboardingComplete", localOnboarded                                  },
            { "CreatedAt",          Firebase.Firestore.FieldValue.ServerTimestamp   }
        };

        db.Collection("users").Document(userId)
            .SetAsync(userData)
            .ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError("❌ Failed to create user doc: " + task.Exception);
                    return;
                }

                Debug.Log("✅ User document created: " + userId);

                if (localOnboarded)
                    MarkOnboardingComplete(); // → Ready
            });
    }
    
    /// <summary>
/// Deletes the Firebase Auth account + Firestore document, wipes PlayerPrefs,
/// then fires OnAuthStateChanged(Guest) so every listener reacts immediately.
/// </summary>
public void ResetAndDeleteAccount(System.Action onComplete = null, System.Action<string> onError = null)
{
    // Step 1 — nuke PlayerPrefs immediately regardless of what happens with Firebase
    PlayerPrefs.DeleteAll();
    PlayerPrefs.Save();

    // Reset all cached state
    Username  = "Player";
    UserClass = "";
    Streak    = 0;
    Coins     = 0;
    XP        = 0;
    Level     = 0;
    CreatedAt = "";
    UserId    = "";
    DataLoaded = false;

    // If guest — nothing to delete in Firebase, we're done
    if (IsGuest)
    {
        SetAuthState(UserAuthState.Guest);
        onComplete?.Invoke();
        return;
    }

    string userIdToDelete = UserId;

    // Step 2 — delete Firestore document first
    if (!string.IsNullOrEmpty(userIdToDelete) && IsOnline)
    {
        db.Collection("users").Document(userIdToDelete)
          .DeleteAsync()
          .ContinueWithOnMainThread(firestoreTask =>
          {
              if (firestoreTask.IsFaulted)
                  Debug.LogWarning("[UserDataService] Firestore delete failed: " + firestoreTask.Exception);

              // Step 3 — delete Firebase Auth account
              DeleteFirebaseAuthUser(onComplete, onError);
          });
    }
    else
    {
        // Offline or no userId — skip Firestore, still try Auth delete
        DeleteFirebaseAuthUser(onComplete, onError);
    }

    // Transition to Guest immediately — UI reacts right away
    SetAuthState(UserAuthState.Guest);
}

private void DeleteFirebaseAuthUser(System.Action onComplete, System.Action<string> onError)
{
    var currentUser = Firebase.Auth.FirebaseAuth.DefaultInstance.CurrentUser;

    if (currentUser == null)
    {
        Debug.Log("[UserDataService] No Firebase Auth user to delete.");
        onComplete?.Invoke();
        return;
    }

    currentUser.DeleteAsync().ContinueWithOnMainThread(authTask =>
    {
        if (authTask.IsFaulted)
        {
            string err = authTask.Exception?.Message ?? "Unknown error";
            Debug.LogError("[UserDataService] Auth delete failed: " + err);
            onError?.Invoke(err);
            return;
        }

        Debug.Log("[UserDataService] Firebase Auth account deleted.");
        Firebase.Auth.FirebaseAuth.DefaultInstance.SignOut();
        onComplete?.Invoke();
    });
}

    /// <summary>Save username + class. For guests this is local only.</summary>
    public void SaveProfile(string username, string userClass, bool mergeToFirebase = true)
    {
        Username  = username;
        UserClass = userClass;

        PlayerPrefs.SetString("username", username);
        PlayerPrefs.SetString("class",    userClass);
        PlayerPrefs.Save();

        if (mergeToFirebase && !IsGuest && IsOnline && !string.IsNullOrEmpty(UserId))
        {
            db.Collection("users").Document(UserId)
              .UpdateAsync(new Dictionary<string, object>
              {
                  { "username", username },
                  { "class",    userClass }
              })
              .ContinueWithOnMainThread(_ =>
              {
                  OnUserDataSaved?.Invoke();
                  FireUserDataEvent();
              });
        }
        else
        {
            FireUserDataEvent();
        }
    }

    // ==========================================================================
    //  Internal
    // ==========================================================================

    private void SetAuthState(UserAuthState newState)
    {
        AuthState = newState;
        OnAuthStateChanged?.Invoke(newState);
        Debug.Log($"[UserDataService] AuthState → {newState}");
    }

    private void FetchFromFirebase()
    {
        db.Collection("users").Document(UserId)
          .GetSnapshotAsync()
          .ContinueWithOnMainThread(task =>
          {
              if (task.IsFaulted || task.IsCanceled) { LoadFromPlayerPrefs(); return; }

              DocumentSnapshot snap = task.Result;
              if (!snap.Exists) { LoadFromPlayerPrefs(); return; }

              var data  = snap.ToDictionary();
              Username  = Get<string>(data, "username", "Player");
              UserClass = Get<string>(data, "class",    "");
              Streak    = Get<int>   (data, "streak",    0);
              Coins     = Get<int>   (data, "coins",     0);
              XP        = Get<int>   (data, "xp",        0);
              Level     = Get<int>   (data, "level",     0);

              CreatedAt = "";
              if (snap.ContainsField("CreatedAt"))
              {
                  try
                  {
                      Timestamp ts       = snap.GetValue<Timestamp>("CreatedAt");
                      DateTime  dateTime = ts.ToDateTime().ToLocalTime();
                      CreatedAt = $"Account created on {dateTime:dd/MM/yyyy} [timestamp {dateTime:HH:mm:ss}]";
                  }
                  catch (Exception e) { Debug.LogWarning("CreatedAt parse failed: " + e.Message); }
              }

              var missing = new Dictionary<string, object>();
              if (!data.ContainsKey("streak")) missing["streak"] = 0;
              if (!data.ContainsKey("coins"))  missing["coins"]  = 0;
              if (!data.ContainsKey("xp"))     missing["xp"]     = 0;
              if (!data.ContainsKey("level"))  missing["level"]  = 0;
              if (missing.Count > 0)
                  db.Collection("users").Document(UserId).UpdateAsync(missing);

              PushToPlayerPrefs();
              DataLoaded = true;
              FireUserDataEvent();
          });
    }

    private void LoadFromPlayerPrefs()
    {
        Username  = PlayerPrefs.GetString("username",     "Player");
        UserClass = PlayerPrefs.GetString("class",        "");
        Streak    = PlayerPrefs.GetInt   ("streak",        0);
        Coins     = PlayerPrefs.GetInt   ("coins",         0);
        XP        = PlayerPrefs.GetInt   ("xp",            0);
        Level     = PlayerPrefs.GetInt   ("currentLevel",  0);
        CreatedAt = "";

        DataLoaded = true;
        OnOfflineLoad?.Invoke();
        FireUserDataEvent();
    }

    private void FireUserDataEvent()
    {
        OnUserDataLoaded?.Invoke(new UserDataPayload
        {
            username  = Username,
            userClass = UserClass,
            streak    = Streak,
            coins     = Coins,
            xp        = XP,
            level     = Level,
            createdAt = CreatedAt,
            isGuest   = IsGuest
        });
    }

    private void PushToPlayerPrefs()
    {
        PlayerPrefs.SetString("username",     Username);
        PlayerPrefs.SetString("class",        UserClass);
        PlayerPrefs.SetInt   ("streak",       Streak);
        PlayerPrefs.SetInt   ("coins",        Coins);
        PlayerPrefs.SetInt   ("xp",           XP);
        PlayerPrefs.SetInt   ("currentLevel", Level);
        PlayerPrefs.Save();
    }

    private T Get<T>(Dictionary<string, object> d, string key, T fallback)
    {
        if (!d.ContainsKey(key)) return fallback;
        try { return (T)Convert.ChangeType(d[key], typeof(T)); }
        catch { return fallback; }
    }
}