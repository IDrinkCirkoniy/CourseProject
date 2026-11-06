using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct TrashSpawnData
{
    public TrashType type;
    public float beatToSpawn; // На каком такте должен появиться мусор
}

public class TrashSpawner : MonoBehaviour
{
    public GameObject[] trashPrefabs;   // 0 - пластик, 1 - картон, 2 - металл
    public Transform[] laneSpawnPoints; // Стартовые точки линий
    public Transform[] laneHitPoints;   // Точки хит-зон линий

    public float beatsToTarget = 4f;    // За сколько битов мусор доезжает до хит-зоны

    [Header("Список спавна (Уровень)")]
    public List<TrashSpawnData> levelWave = new List<TrashSpawnData>();
    private int spawnedIndex = 0;

    void Start()
    {
        // Для теста: сгенерируем автоматическую волну мусора на первые 20 тактов, 
        // если список пуст.
        if (levelWave.Count == 0)
        {
            GenerateTestWave();
        }
    }

    void Update()
    {
        if (Conductor.Instance == null) return;

        float currentBeat = Conductor.Instance.songPositionInBeats;

        // Проверяем, пора ли спавнить следующий объект из списка
        if (spawnedIndex < levelWave.Count)
        {
            // Если текущий бит песни дошел до момента спавна (с учетом времени полета ноты)
            // Мы спавним объект заранее, за `beatsToTarget` битов до того, как он должен приехать
            if (currentBeat >= levelWave[spawnedIndex].beatToSpawn - beatsToTarget)
            {
                SpawnTrashFromData(levelWave[spawnedIndex]);
                spawnedIndex++;
            }
        }
    }

    void SpawnTrashFromData(TrashSpawnData data)
    {
        int laneIndex = (int)data.type;

        if (trashPrefabs[laneIndex] == null || laneSpawnPoints[laneIndex] == null)
        {
            Debug.LogError($"Префаб или точка спавна для линии {laneIndex} не назначена!");
            return;
        }

        GameObject trashObj = Instantiate(
            trashPrefabs[laneIndex],
            laneSpawnPoints[laneIndex].position,
            Quaternion.identity
        );

        TrashController controller = trashObj.GetComponent<TrashController>();
        if (controller != null)
        {
            controller.trashType = data.type;
            // Целевой бит, когда объект должен быть в хит-зоне
            controller.targetBeat = data.beatToSpawn;
            controller.spawnPos = laneSpawnPoints[laneIndex].position;
            controller.hitPos = laneHitPoints[laneIndex].position;
            controller.beatsToTarget = beatsToTarget;
        }
    }

    // Временный генератор тестовой волны, чтобы мусор сам падал под музыку
    void GenerateTestWave()
    {
        for (int i = 4; i < 50; i += 2) // Каждые 2 такта
        {
            TrashSpawnData data = new TrashSpawnData();
            data.type = (TrashType)Random.Range(0, 3); // Случайный мусор
            data.beatToSpawn = i; // На каком такте ждем его в хит-зоне
            levelWave.Add(data);
        }
    }
}