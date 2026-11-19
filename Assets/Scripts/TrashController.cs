using UnityEngine;

public class TrashController : MonoBehaviour
{
    public TrashType trashType;
    public float targetBeat;
    public Vector3 spawnPos;
    public Vector3 hitPos;
    public float beatsToTarget = 4f;

    private void Update()
    {
        if (Conductor.Instance == null) return;

        float currentBeat = Conductor.Instance.songPositionInBeats;
        float progress = 1f - ((targetBeat - currentBeat) / beatsToTarget);

        transform.position = Vector3.Lerp(spawnPos, hitPos, progress);

        if (progress > 1.2f)
        {
            // Объект пролетел мимо пресса
            Destroy(gameObject);
        }
    }
}