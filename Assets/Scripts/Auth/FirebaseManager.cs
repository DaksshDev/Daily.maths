using UnityEngine;
using Firebase;
using Firebase.Auth;
using Firebase.Firestore;
using Firebase.Extensions;

public class FirebaseManager : MonoBehaviour
{
    [CoolHeader("Firebase check")]
    
    [Space]
    public FirebaseAuth auth;
    public FirebaseFirestore db;
    

    public void FirebaseInit()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;

            if (dependencyStatus == DependencyStatus.Available)
            {
                Debug.Log("Firebase Ready");

                auth = FirebaseAuth.DefaultInstance;
                db = FirebaseFirestore.DefaultInstance;
            }
            else
            {
                Debug.LogError("Firebase not available: " + dependencyStatus);
            }
        });
    }
}
