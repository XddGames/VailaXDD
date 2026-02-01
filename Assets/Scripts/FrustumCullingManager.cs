using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class FrustumCullingManager : MonoBehaviour
{
    [Header("Performance Settings")]
    [Tooltip("How many objects to process per frame. Higher = faster response, Lower = better FPS.")]
    [SerializeField] private int objectsProcessedPerFrame = 2500; 
    [Tooltip("Global max distance. Objects further than this are instantly hidden.")]
    [SerializeField] private float globalCullDistance = 150f;
    [Tooltip("Objects closer than this are ALWAYS visible (prevents popping when turning).")]
    [SerializeField] private float minCullDistance = 10f; 

    [Header("Strictness")]
    [Tooltip("If true, objects behind the camera are aggressively hidden using Dot Product.")]
    [SerializeField] private bool aggressiveBackCulling = true;
    [Tooltip("How much 'buffer' behind the camera to allow (negative values allow objects slightly behind).")]
    [SerializeField] private float backCullThreshold = -0.2f;

    [Header("Targeting")]
    [SerializeField] private LayerMask searchLayers = -1;
    [SerializeField] private string[] ignoreTags = new string[] { "Player", "MainCamera" };
    [SerializeField] private bool autoFindPlayerCamera = true;
    [SerializeField] private Camera cullingCamera;

    [Header("Debug")]
    [SerializeField] private bool showDebugStats = true;

    // The core list
    private List<CulledItem> trackedObjects = new List<CulledItem>();
    
    // Runtime optimization vars
    private Plane[] camPlanes;
    private int currentIndex = 0;
    private int visibleCount = 0;
    private Vector3 camPos;
    private Vector3 camFwd;

    // Helper class to cache data (avoiding GetComponent calls in Update)
    private class CulledItem
    {
        public GameObject obj;
        public Transform trans;
        public Renderer[] renderers;
        public Bounds bounds; // Cached bounds (local size)
        public bool isVisible;
        public float sphereRadius; // For quick distance checks
    }

    private void Start()
    {
        // Delay slightly to ensure network players are spawned
        Invoke(nameof(Initialize), 0.5f);
    }

    private void Initialize()
    {
        if (autoFindPlayerCamera) FindLocalCamera();
        
        // Scan the scene for objects
        ScanForObjects();
    }

    private void FindLocalCamera()
    {
        // 1. Try finding by Photon View (Most accurate for Multiplayer)
        var photonViews = FindObjectsByType<PhotonView>(FindObjectsSortMode.None);
        foreach (var pv in photonViews)
        {
            if (pv.IsMine)
            {
                Camera cam = pv.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    cullingCamera = cam;
                    Debug.Log($"[Culling] Linked to Local Player Camera: {cam.name}");
                    return;
                }
            }
        }

        // 2. Fallback to MainCamera
        if (Camera.main != null)
        {
            cullingCamera = Camera.main;
            return;
        }

        Debug.LogError("[Culling] CRITICAL: No Camera found!");
        this.enabled = false;
    }

    [ContextMenu("Rescan Scene")]
    public void ScanForObjects()
    {
        trackedObjects.Clear();
        Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        foreach (Renderer r in allRenderers)
        {
            GameObject go = r.gameObject;

            // --- FILTERING ---
            // 1. Skip ignored layers
            if (((1 << go.layer) & searchLayers) == 0) continue;
            
            // 2. Skip ignored tags
            bool isIgnored = false;
            foreach (string tag in ignoreTags) {
                if (go.CompareTag(tag)) { isIgnored = true; break; }
            }
            if (isIgnored) continue;

            // 3. Skip UI
            if (go.GetComponent<RectTransform>() != null) continue;

            // --- REGISTRATION ---
            // Group renderers if they share a parent (optimization)
            // Note: Simplest approach is 1 object = 1 tracked item
            
            // Check if we already tracked this object (in case of multiple renderers)
            // For high performance, we assume 1 GameObject = 1 Renderer usually, 
            // or we track the parent. Here we track individual objects for granularity.
            
            // Calculate a safe radius for the object
            float radius = r.bounds.extents.magnitude;

            trackedObjects.Add(new CulledItem
            {
                obj = go,
                trans = go.transform,
                renderers = new Renderer[] { r }, // If you want to group them, do GetComponentsInChildren here
                bounds = r.bounds, // Initial bounds
                sphereRadius = radius,
                isVisible = true
            });
        }

        Debug.Log($"[Culling] Indexing Complete. Tracked Objects: {trackedObjects.Count}");
    }

    private void Update()
    {
        if (cullingCamera == null)
        {
            if (autoFindPlayerCamera) FindLocalCamera();
            return;
        }

        // 1. Cache Camera Data ONCE per frame
        camPos = cullingCamera.transform.position;
        camFwd = cullingCamera.transform.forward;
        camPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);

        // 2. Process a chunk of objects
        int processedThisFrame = 0;
        int totalItems = trackedObjects.Count;

        // Loop through the list, wrapping around if we hit the end
        while (processedThisFrame < objectsProcessedPerFrame)
        {
            if (currentIndex >= totalItems) currentIndex = 0;
            if (totalItems == 0) break;

            ProcessObject(trackedObjects[currentIndex]);

            currentIndex++;
            processedThisFrame++;
            
            // Safety break if we did a full loop in one frame (small scenes)
            if (processedThisFrame >= totalItems) break;
        }
    }

    private void ProcessObject(CulledItem item)
    {
        if (item.obj == null) return; // Object was destroyed

        bool shouldBeVisible = false;
        Vector3 itemPos = item.trans.position;

        // --- CHECK 1: DISTANCE (Cheapest) ---
        float distSq = (itemPos - camPos).sqrMagnitude;
        
        // If it's super close, ALWAYS show it (prevents popping)
        if (distSq < minCullDistance * minCullDistance)
        {
            shouldBeVisible = true;
        }
        // If it's too far, hide it
        else if (distSq > globalCullDistance * globalCullDistance)
        {
            shouldBeVisible = false;
        }
        else
        {
            // --- CHECK 2: BACK-FACE CULLING (The "Behind" Fix) ---
            bool isBehind = false;
            if (aggressiveBackCulling)
            {
                Vector3 dirToObj = (itemPos - camPos).normalized;
                // Dot Product: 1.0 = directly in front, -1.0 = directly behind
                if (Vector3.Dot(camFwd, dirToObj) < backCullThreshold)
                {
                    isBehind = true;
                }
            }

            if (isBehind)
            {
                shouldBeVisible = false;
            }
            else
            {
                // --- CHECK 3: FRUSTUM (Most Expensive) ---
                // We create a bounding sphere or box at the CURRENT position
                // Using a simple bounds check is usually enough
                Bounds currentBounds = new Bounds(itemPos, Vector3.one * item.sphereRadius * 2);
                
                if (GeometryUtility.TestPlanesAABB(camPlanes, currentBounds))
                {
                    shouldBeVisible = true;
                }
                else
                {
                    shouldBeVisible = false;
                }
            }
        }

        // --- APPLY STATE ---
        // Only toggle if state changed (optimization)
        if (item.isVisible != shouldBeVisible)
        {
            item.isVisible = shouldBeVisible;
            
            // Toggle renderers
            for (int i = 0; i < item.renderers.Length; i++)
            {
                if(item.renderers[i] != null) 
                    item.renderers[i].enabled = shouldBeVisible;
            }
            
            if (shouldBeVisible) visibleCount++;
            else visibleCount--;
        }
    }

    private void OnGUI()
    {
        if (!showDebugStats) return;
        GUI.color = Color.yellow;
        GUILayout.BeginArea(new Rect(10, 10, 300, 150));
        GUILayout.Label("--- CULLING STATS ---");
        GUILayout.Label($"Total Objects: {trackedObjects.Count}");
        GUILayout.Label($"Visible: {visibleCount}");
        GUILayout.Label($"Culled: {trackedObjects.Count - visibleCount}");
        GUILayout.Label($"Processing: {objectsProcessedPerFrame}/frame");
        GUILayout.EndArea();
    }
}
