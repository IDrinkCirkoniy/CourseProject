using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct TrashSpawnData
{
    public TrashType type;
    public float beatToSpawn;
}

public class TrashSpawner : MonoBehaviour
{
    public GameObject[] trashPrefabs;   // 0 - пластик, 1 - картон, 2 - металл
    public Transform[] laneSpawnPoints;
    public Transform[] laneHitPoints;

    public float beatsToTarget = 4f;

    [Header("Список спавна (Уровень)")]
    public List<TrashSpawnData> levelWave = new List<TrashSpawnData>();
    private int spawnedIndex = 0;

    void Start()
    {
        if (levelWave.Count == 0)
        {
            GenerateTestWave();
        }
    }

    void Update()
    {
        if (Conductor.Instance == null)
        {
            return;
        }

        float currentBeat = Conductor.Instance.songPositionInBeats;

        if (spawnedIndex < levelWave.Count)
        {
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

        bool invalidPrefabs = trashPrefabs == null || laneIndex < 0 || laneIndex >= trashPrefabs.Length || trashPrefabs[laneIndex] == null;
        bool invalidSpawns = laneSpawnPoints == null || laneIndex < 0 || laneIndex >= laneSpawnPoints.Length || laneSpawnPoints[laneIndex] == null;
        bool invalidHits = laneHitPoints == null || laneIndex < 0 || laneIndex >= laneHitPoints.Length || laneHitPoints[laneIndex] == null;

        if (invalidPrefabs || invalidSpawns || invalidHits)
        {
            Debug.LogError($"Префаб, точка спавна или точка попадания для линии {laneIndex} не назначена!");
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
            controller.targetBeat = data.beatToSpawn;
            controller.spawnPos = laneSpawnPoints[laneIndex].position;
            controller.hitPos = laneHitPoints[laneIndex].position;
            controller.beatsToTarget = beatsToTarget;
        }
    }

    void GenerateTestWave()
    {
        for (int i = 4; i < 50; i += 2)
        {
            TrashSpawnData data = new TrashSpawnData();
            data.type = (TrashType)Random.Range(0, 3);
            data.beatToSpawn = i;
            levelWave.Add(data);
        }
    }
}