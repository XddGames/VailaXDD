using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class DiaryUI : MonoBehaviour
{
    [Header("References")]
    public GameObject diaryBookVisuals; 
    public GameObject[] pageImages;     

    void Start()
    {
        if(diaryBookVisuals != null) 
            diaryBookVisuals.SetActive(false);
    }

    public void ToggleDiary(bool isOpen, List<int> collectedIDs)
    {
        if(diaryBookVisuals == null) return;

        diaryBookVisuals.SetActive(isOpen);

        if (isOpen)
        {
            UpdatePages(collectedIDs);
        }
    }

    private void UpdatePages(List<int> collectedIDs)
    {
        foreach (var page in pageImages)
        {
            if(page != null) page.SetActive(false);
        }

        foreach (int id in collectedIDs)
        {
            int index = id - 1;

            if (index >= 0 && index < pageImages.Length)
            {
                UnityEngine.Debug.Log($"updated {index}");
                pageImages[index].SetActive(true);
            }
        }
    }
}