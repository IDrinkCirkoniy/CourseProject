using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class TrackDto
{
    public string id;
    public string title;
    public string artist;
    public float bpm;
    public float durationSeconds;
    public string audioUrl;
    public string uploadedByUserId;
}

[Serializable]
public class TrackListResponse
{
    public TrackDto[] items;
}

[Serializable]
public class UpdateTrackPayload
{
    public string title;
    public string artist;
    public float bpm;
}

public class TrackApiClient : MonoBehaviour
{
    [Header("Подключение к API")]
    [SerializeField] private string baseUrl = "http://localhost:5026";

    [Header("Текущий пользователь и роль")]
    [SerializeField] private string currentUserId = "";
    [SerializeField] private string currentRole = "Guest"; // Guest | Creator | Admin

    public string CurrentUserId => currentUserId;
    public string CurrentRole => currentRole;

    public bool CanCreateTrack =>
        currentRole == "Creator" || currentRole == "Admin";

    public bool CanEditTrack(TrackDto track)
    {
        if (track == null) return false;
        if (currentRole == "Admin") return true;
        if (currentRole == "Creator")
        {
            return !string.IsNullOrEmpty(currentUserId) &&
                   string.Equals(track.uploadedByUserId, currentUserId, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    public void SetSession(string userId, string role)
    {
        currentUserId = userId ?? "";
        currentRole = string.IsNullOrWhiteSpace(role) ? "Guest" : role;
    }

    // Доступно всем (включая Guest)
    public IEnumerator GetTracks(Action<TrackDto[]> onSuccess, Action<string> onError)
    {
        string url = $"{baseUrl.TrimEnd('/')}/api/tracks";
        using UnityWebRequest req = UnityWebRequest.Get(url);
        AttachUserHeader(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}: {req.downloadHandler?.text}");
            yield break;
        }

        TrackDto[] tracks = ParseTracksJson(req.downloadHandler?.text);
        onSuccess?.Invoke(tracks);
    }

    // Загрузка трека: только Creator или Admin
    public IEnumerator UploadTrack(
        string localAudioPath,
        string title,
        string artist,
        float bpm,
        Action<TrackDto> onSuccess,
        Action<string> onError)
    {
        if (!CanCreateTrack)
        {
            onError?.Invoke($"Роль '{currentRole}' не может загружать треки.");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            onError?.Invoke("Не заполнен Current User Id (заголовок X-User-Id не будет отправлен, сервер вернёт 401).");
            yield break;
        }

        if (string.IsNullOrWhiteSpace(localAudioPath) || !File.Exists(localAudioPath))
        {
            onError?.Invoke($"Файл не найден: {localAudioPath}");
            yield break;
        }

        byte[] audioBytes = File.ReadAllBytes(localAudioPath);
        string fileName = Path.GetFileName(localAudioPath);

        var form = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("File", audioBytes, fileName, "application/octet-stream"),
            new MultipartFormDataSection("Title", title),
            new MultipartFormDataSection("Artist", artist),
            new MultipartFormDataSection("Bpm", bpm.ToString(CultureInfo.InvariantCulture))
        };

        string url = $"{baseUrl.TrimEnd('/')}/api/tracks";
        using UnityWebRequest req = UnityWebRequest.Post(url, form);
        AttachUserHeader(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}: {req.downloadHandler?.text}");
            yield break;
        }

        TrackDto created = JsonUtility.FromJson<TrackDto>(req.downloadHandler?.text);
        onSuccess?.Invoke(created);
    }

    // Редактирование трека: Creator — только свой, Admin — любой
    public IEnumerator UpdateTrack(
        string trackId,
        string title,
        string artist,
        float bpm,
        Action onSuccess,
        Action<string> onError)
    {
        string json = JsonUtility.ToJson(new UpdateTrackPayload
        {
            title = title,
            artist = artist,
            bpm = bpm
        });

        byte[] rawBody = Encoding.UTF8.GetBytes(json);
        string url = $"{baseUrl.TrimEnd('/')}/api/tracks/{trackId}";
        using UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
        req.uploadHandler = new UploadHandlerRaw(rawBody);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        AttachUserHeader(req);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}: {req.downloadHandler?.text}");
            yield break;
        }

        onSuccess?.Invoke();
    }

    // Скачивание аудиофайла (доступно всем ролям, включая Guest)
    public IEnumerator DownloadAudioClip(
        string audioUrl,
        AudioType audioType,
        Action<AudioClip> onSuccess,
        Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            onError?.Invoke("Пустой audioUrl.");
            yield break;
        }

        string trimmedBase = baseUrl.TrimEnd('/');
        string fullUrl = audioUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? audioUrl
            : $"{trimmedBase}/{(audioUrl.TrimStart('/'))}";

        using UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(fullUrl, audioType);
        AttachUserHeader(req);
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke($"{req.responseCode} {req.error}");
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
        onSuccess?.Invoke(clip);
    }

    private void AttachUserHeader(UnityWebRequest req)
    {
        if (!string.IsNullOrWhiteSpace(currentUserId))
            req.SetRequestHeader("X-User-Id", currentUserId.Trim());
    }

    private static TrackDto[] ParseTracksJson(string rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return Array.Empty<TrackDto>();

        string trimmed = rawJson.TrimStart();
        if (trimmed.StartsWith("["))
        {
            string wrapped = $"{{\"items\":{trimmed}}}";
            TrackListResponse arrayResponse = JsonUtility.FromJson<TrackListResponse>(wrapped);
            return arrayResponse != null && arrayResponse.items != null
                ? arrayResponse.items
                : Array.Empty<TrackDto>();
        }

        TrackListResponse response = JsonUtility.FromJson<TrackListResponse>(trimmed);
        return response != null && response.items != null
            ? response.items
            : Array.Empty<TrackDto>();
    }
}