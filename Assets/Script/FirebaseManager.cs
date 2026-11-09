using UnityEngine;
using Firebase;
using Firebase.Firestore;
using Firebase.Extensions;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance;
    public FirebaseFirestore Db { get; private set; }
    public bool IsReady { get; private set; }
    public event System.Action Ready;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            DependencyStatus dependencyStatus = task.Result;

            if (dependencyStatus == DependencyStatus.Available)
            {
                Db = FirebaseFirestore.DefaultInstance;
                IsReady = true;
                Debug.Log("Firebase listo.");
                Ready?.Invoke();
            }
            else
            {
                Debug.LogError("No se pudieron resolver las dependencias de Firebase: " + dependencyStatus);
                IsReady = false;
            }
        });
    }
}