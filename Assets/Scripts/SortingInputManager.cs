using UnityEngine;

public class SortingInputManager : MonoBehaviour
{
    public LaneHitZone[] lanes;
    private int activeLaneIndex = 0;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Q))
        {
            activeLaneIndex = 0;
            Debug.Log("Выбрана линия 1 (Пластик)");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.W))
        {
            activeLaneIndex = 1;
            Debug.Log("Выбрана линия 2 (Картон)");
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.E))
        {
            activeLaneIndex = 2;
            Debug.Log("Выбрана линия 3 (Металл)");
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (activeLaneIndex >= 0 && activeLaneIndex < lanes.Length)
            {
                lanes[activeLaneIndex].TryPress();
            }
        }
    }
}