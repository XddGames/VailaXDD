using UnityEngine;
using UnityEngine.UI;

public class PageManager : MonoBehaviour
{
    public static PageManager Instance;

    [Header("UI References")]
    public GameObject journalCanvas;
    public GameObject PageCanvas;
    public GameObject uiTop;
    public GameObject uiMid;
    public GameObject uiBot;

    private bool hasTop = false;
    private bool hasMid = false;
    private bool hasBot = false;

    void Awake() { Instance = this; }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            bool isActive = journalCanvas.activeSelf;
            journalCanvas.SetActive(!isActive);
            
            Cursor.lockState = isActive ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !isActive;
        }
    }

    public void CollectPiece(int id)
    {
        switch (id)
        {
            case 1:
                hasTop = true;
                uiTop.SetActive(true);
                break;
            case 2:
                hasMid = true;
                uiMid.SetActive(true);
                break;
            case 3:
                hasBot = true;
                uiBot.SetActive(true);
                break;
        }

        PageCanvas.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        // IMPORTANT:: IDK HOW TO ALLOW MOUSE AFTER THIS
    }

    public bool HasCompletePage()
    {
        return hasTop && hasMid && hasBot;
    }
}