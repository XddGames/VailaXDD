using UnityEngine;

public class LightingManager : MonoBehaviour
{
    public PowerGenerator gen1;
    public PowerGenerator gen2;
    public GameObject[] buildingLights; // drag lights here

    void Start()
    {
        gen1.OnStateChanged.AddListener((state) => CheckLights());
        gen2.OnStateChanged.AddListener((state) => CheckLights());
        
        CheckLights();
    }

    void CheckLights()
    {
        bool powerRestored = gen1.IsOn && gen2.IsOn;

        foreach (GameObject lightObj in buildingLights)
        {
            lightObj.SetActive(powerRestored);
        }

        if(powerRestored) Debug.Log("BUILDING POWER ONLINE");
        else Debug.Log("POWER OFFLINE");
    }
}