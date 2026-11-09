using System;
using System.Collections.Generic;
using System.Collections;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

public class PlayerProfileManager : MonoBehaviour
{
    private const string ActivePlayerIdKey = "ACTIVE_PLAYER_ID";
    private const string ActivePlayerNameKey = "ACTIVE_PLAYER_NAME";
    private const string ActivePlayerAvatarKey = "ACTIVE_PLAYER_AVATAR";
    private const string CachedProfilesKey = "CACHED_PLAYER_PROFILES";

    [Serializable]
    private class ProfileCache
    {
        public List<PlayerProfileData> profiles = new List<PlayerProfileData>();
    }

    public static PlayerProfileManager Instance { get; private set; }
    public PlayerProfileData ActiveProfile { get; private set; }
    public bool HasActiveProfile => ActiveProfile != null && !string.IsNullOrEmpty(ActiveProfile.playerId);
    public event Action<PlayerProfileData> ActiveProfileChanged;

    [SerializeField] private string[] avatarOptions = { "conejo_azul", "zorro_naranja", "oso_verde" };
    private readonly List<PlayerProfileData> cachedProfiles = new List<PlayerProfileData>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LoadCachedProfiles();
        LoadActiveProfileFromPrefs();
        if (HasActiveProfile && cachedProfiles.Find(profile => profile.playerId == ActiveProfile.playerId) == null)
            CacheProfile(ActiveProfile);
    }

    public string[] GetAvatarOptions()
    {
        return avatarOptions;
    }

    public void LoadProfiles(Action<List<PlayerProfileData>> onSuccess, Action<string> onError)
    {
        FirebaseManager manager = FirebaseManager.Instance;
        if (manager == null || !manager.IsReady)
        {
            onSuccess?.Invoke(GetCachedProfiles());
            onError?.Invoke("Sin conexion: se muestran los perfiles guardados en este dispositivo.");
            return;
        }

        // Una respuesta del servidor es la fuente de verdad; si tarda, permitir jugar sin conexión.
        bool serverResponded = false;
        StartCoroutine(ShowCachedProfilesIfServerSlow(() => serverResponded, onSuccess, onError));
        manager.Db.Collection("players").GetSnapshotAsync(Source.Server).ContinueWithOnMainThread(task =>
        {
            serverResponded = true;
            if (task.IsFaulted || task.IsCanceled)
            {
                Debug.LogWarning("No se pudieron cargar perfiles en linea: " +
                    (task.Exception != null ? task.Exception.GetBaseException().Message : "Firebase no disponible."));
                onSuccess?.Invoke(GetCachedProfiles());
                onError?.Invoke("No se pudieron cargar perfiles en linea. Puedes usar los guardados en este dispositivo.");
                return;
            }

            QuerySnapshot snapshot = task.Result;
            List<PlayerProfileData> profiles = new List<PlayerProfileData>();
            foreach (DocumentSnapshot document in snapshot.Documents)
            {
                Dictionary<string, object> data = document.ToDictionary();
                PlayerProfileData profile = new PlayerProfileData
                {
                    playerId = GetString(data, "playerId", document.Id),
                    name = GetString(data, "name", document.Id),
                    avatar = GetString(data, "avatar", avatarOptions.Length > 0 ? avatarOptions[0] : "avatar_default"),
                    createdAt = GetString(data, "createdAt", ReportUtility.UtcNowString()),
                };
                profiles.Add(profile);
            }

            // No eliminar perfiles nuevos que aún esperan sincronizarse en este dispositivo.
            if (ReportOutbox.Instance != null)
                foreach (PlayerProfileData local in cachedProfiles)
                    if (local != null && ReportOutbox.Instance.HasPendingProfile(local.playerId) &&
                        profiles.Find(item => item.playerId == local.playerId) == null)
                        profiles.Add(local);

            cachedProfiles.Clear();
            cachedProfiles.AddRange(profiles);
            PlayerPrefs.SetString(CachedProfilesKey, JsonUtility.ToJson(new ProfileCache { profiles = cachedProfiles }));
            PlayerPrefs.Save();
            if (HasActiveProfile)
            {
                PlayerProfileData active = profiles.Find(item => item.playerId == ActiveProfile.playerId);
                if (active == null)
                    ClearActiveProfile();
                else
                    SetActiveProfile(active);
            }

            profiles.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
            onSuccess?.Invoke(profiles);
        });
    }

    private IEnumerator ShowCachedProfilesIfServerSlow(Func<bool> serverResponded,
        Action<List<PlayerProfileData>> onSuccess, Action<string> onError)
    {
        yield return new WaitForSecondsRealtime(6f);
        if (serverResponded())
            yield break;

        onSuccess?.Invoke(GetCachedProfiles());
        onError?.Invoke("Esperando conexión: se muestran los perfiles de este dispositivo.");
    }

    public void CreateProfile(string playerName, string avatar, Action<PlayerProfileData> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            onError?.Invoke("Debes escribir un nombre.");
            return;
        }

        PlayerProfileData profile = new PlayerProfileData
        {
            playerId = ReportUtility.GenerateId("player"),
            name = playerName.Trim(),
            avatar = string.IsNullOrWhiteSpace(avatar) ? (avatarOptions.Length > 0 ? avatarOptions[0] : "avatar_default") : avatar,
            createdAt = ReportUtility.UtcNowString(),
        };

        if (ReportOutbox.Instance == null)
        {
            onError?.Invoke("El almacenamiento local no esta disponible.");
            return;
        }

        ReportOutbox.Instance.QueueProfile(profile, () =>
        {
            SetActiveProfile(profile);
            onSuccess?.Invoke(profile);
        }, onError);
    }

    public void SetActiveProfile(PlayerProfileData profile)
    {
        if (profile == null)
            return;

        ActiveProfile = profile;
        CacheProfile(profile);
        PlayerPrefs.SetString(ActivePlayerIdKey, profile.playerId ?? string.Empty);
        PlayerPrefs.SetString(ActivePlayerNameKey, profile.name ?? string.Empty);
        PlayerPrefs.SetString(ActivePlayerAvatarKey, profile.avatar ?? string.Empty);
        PlayerPrefs.Save();
        ActiveProfileChanged?.Invoke(ActiveProfile);
    }

    public void ClearActiveProfile()
    {
        ActiveProfile = null;
        PlayerPrefs.DeleteKey(ActivePlayerIdKey);
        PlayerPrefs.DeleteKey(ActivePlayerNameKey);
        PlayerPrefs.DeleteKey(ActivePlayerAvatarKey);
        PlayerPrefs.Save();
        ActiveProfileChanged?.Invoke(null);
    }

    private void LoadActiveProfileFromPrefs()
    {
        string playerId = PlayerPrefs.GetString(ActivePlayerIdKey, string.Empty);
        if (string.IsNullOrEmpty(playerId))
            return;

        ActiveProfile = new PlayerProfileData
        {
            playerId = playerId,
            name = PlayerPrefs.GetString(ActivePlayerNameKey, string.Empty),
            avatar = PlayerPrefs.GetString(ActivePlayerAvatarKey, avatarOptions.Length > 0 ? avatarOptions[0] : "avatar_default"),
            createdAt = string.Empty,
        };
    }

    private static string GetString(Dictionary<string, object> data, string key, string fallback)
    {
        return data != null && data.TryGetValue(key, out object value) && value != null ? value.ToString() : fallback;
    }

    private void LoadCachedProfiles()
    {
        try
        {
            string json = PlayerPrefs.GetString(CachedProfilesKey, string.Empty);
            if (string.IsNullOrEmpty(json))
                return;

            ProfileCache cache = JsonUtility.FromJson<ProfileCache>(json);
            if (cache != null && cache.profiles != null)
                cachedProfiles.AddRange(cache.profiles);
        }
        catch (Exception error)
        {
            Debug.LogWarning("No se pudieron cargar los perfiles locales: " + error.Message);
        }
    }

    private void CacheProfile(PlayerProfileData profile)
    {
        if (profile == null || string.IsNullOrEmpty(profile.playerId))
            return;

        int index = cachedProfiles.FindIndex(item => item.playerId == profile.playerId);
        if (index >= 0)
            cachedProfiles[index] = profile;
        else
            cachedProfiles.Add(profile);

        PlayerPrefs.SetString(CachedProfilesKey, JsonUtility.ToJson(new ProfileCache { profiles = cachedProfiles }));
        PlayerPrefs.Save();
    }

    private List<PlayerProfileData> GetCachedProfiles()
    {
        List<PlayerProfileData> profiles = new List<PlayerProfileData>(cachedProfiles);
        profiles.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        return profiles;
    }
}
