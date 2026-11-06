using UnityEngine;

public class LaneHitZone : MonoBehaviour
{
    public TrashType laneType;
    private TrashController currentTrashInZone;

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<TrashController>(out TrashController trash))
        {
            currentTrashInZone = trash;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent<TrashController>(out TrashController trash))
        {
            if (trash == currentTrashInZone)
            {
                currentTrashInZone = null;
            }
        }
    }

    public void TryPress()
    {
        if (currentTrashInZone != null)
        {
            float currentBeat = Conductor.Instance.songPositionInBeats;
            float timingError = Mathf.Abs(currentTrashInZone.targetBeat - currentBeat);
            float timing = 0.75f;

            if (timingError <= timing) // Окно идеального попадания
            {
                Debug.Log($"Пресс сработал идеально! Ошибка: {timingError:F2}");
            }
            else
            {
                Debug.Log($"Мимо по такту! Ошибка была: {timingError:F2} (больше {timing})");
            }

            Destroy(currentTrashInZone.gameObject);
            currentTrashInZone = null;
        }
        else
        {
            Debug.Log("Нажатие пресса впустую (нет мусора в зоне)");
        }
    }
}