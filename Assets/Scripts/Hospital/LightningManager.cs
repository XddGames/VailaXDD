using UnityEngine;

public class LightingManager : MonoBehaviour
{
    public PowerGenerator gen1;
    public PowerGenerator gen2;

    [Header("Parent")]
    public GameObject hospitalLightsParent;

    void Start()
    {
        gen1.OnStateChanged.AddListener((state) => CheckLights());
        gen2.OnStateChanged.AddListener((state) => CheckLights());
        
        CheckLights();
    }

    void CheckLights()
    {
        bool powerRestored = gen1.IsOn && gen2.IsOn;

        if(hospitalLightsParent != null)
            hospitalLightsParent.SetActive(powerRestored);
        else
            Debug.LogError("Dont have lights to update");

        if(powerRestored) Debug.Log("BUILDING POWER ONLINE");
        else Debug.Log("POWER OFFLINE");
    }
}