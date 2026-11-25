using System.Collections;
using UnityEngine;

public class TrackApiTester : MonoBehaviour
{
    [SerializeField] private TrackApiClient apiClient;
    [SerializeField] private string testAudioPath = @"C:\path\to\test.mp3";

    private void Start()
    {
        if (apiClient == null)
            apiClient = GetComponent<TrackApiClient>();

        if (apiClient == null)
        {
            Debug.LogError("TrackApiTester: не найден компонент TrackApiClient!");
            return;
        }

        StartCoroutine(RunChecks());
    }

    private IEnumerator RunChecks()
    {
        if (apiClient == null)
        {
            Debug.LogError("TrackApiTester: apiClient равен null, проверка прервана.");
            yield break;
        }

        // 1. Проверяем получение списка треков (работает даже для Guest)
        yield return apiClient.GetTracks(
            tracks => Debug.Log($"Треков получено: {tracks.Length}"),
            err => Debug.LogError($"Ошибка GetTracks: {err}")
        );

        // 2. Загрузка тестового трека (нужны роль Creator/Admin и валидный Current User Id)
        yield return apiClient.UploadTrack(
            testAudioPath,
            "Test Track",
            "Test Artist",
            128f,
            created => Debug.Log($"Трек загружен: {created.title} (id: {created.id})"),
            err => Debug.LogError($"Ошибка UploadTrack: {err}")
        );
    }
}