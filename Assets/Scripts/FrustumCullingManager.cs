using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class FrustumCullingManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera cullingCamera;
    [SerializeField] private bool autoFindPlayerCamera = true;
    [SerializeField] private float updateInterval = 0.1f; // Faster updates for smoother culling
    [SerializeField] private int maxObjectsPerFrame = 500; // Process more objects per update
    [SerializeField] private float cullingDistance = 150f; // Reduced max distance before culling
    [SerializeField] private bool useFrustumCulling = true;
    [SerializeField] private bool useDistanceCulling = true;

    [Header("Tree-Specific Optimization")]
    [SerializeField] private bool aggressiveTreeCulling = true;
    [SerializeField] private float treeNearDistance = 30f;   // Full detail
    [SerializeField] private float treeMediumDistance = 60f; // Medium LOD
    [SerializeField] private float treeFarDistance = 100f;   // Low LOD / billboard
    [SerializeField] private bool disableTreeGameObjects = true; // Disable entire GameObject instead of just renderer

    [Header("Target Layers")]
    [SerializeField] private LayerMask cullableLayers = -1;
    [SerializeField] private LayerMask treeLayers; // Specific layer for trees (set in inspector)

    [Header("Spatial Partitioning")]
    [SerializeField] private bool useSpatialPartitioning = true;
    [SerializeField] private float cellSize = 50f; // Size of each grid cell

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private List<CullableObject> cullableObjects = new List<CullableObject>();
    private List<CullableObject> treeObjects = new List<CullableObject>(); // Separate list for trees
    private Dictionary<Vector2Int, List<CullableObject>> spatialGrid = new Dictionary<Vector2Int, List<CullableObject>>();
    private float updateTimer = 0f;
    private int currentBatchIndex = 0;
    private int treeBatchIndex = 0;
    private Plane[] frustumPlanes;
    private int culledCount = 0;
    private int totalCount = 0;
    private Vector3 lastCameraPosition;
    private float cameraMovementThreshold = 2f; // Only recalculate if camera moved this much

    private class CullableObject
    {
        public GameObject gameObject;
        public Renderer[] renderers;
        public LODGroup lodGroup;
        public Bounds bounds;
        public Bounds originalBounds;
        public bool isVisible;
        public bool isTree;
        public int currentLODLevel;
        public Vector2Int gridCell;
        public float lastDistance;
    }

    private void Start()
    {
        Invoke(nameof(DelayedStart), 0.5f);
    }

    private void DelayedStart()
    {
        if (cullingCamera == null && autoFindPlayerCamera)
        {
            FindLocalPlayerCamera();
        }

        if (cullingCamera == null)
        {
            cullingCamera = Camera.main;
        }

        if (cullingCamera == null)
        {
            Debug.LogWarning("FrustumCullingManager: No camera found! Culling disabled.");
            enabled = false;
            return;
        }

        lastCameraPosition = cullingCamera.transform.position;
        RegisterAllCullableObjects();

        if (showDebugInfo)
        {
            Debug.Log($"FrustumCullingManager started with camera: {cullingCamera.name}");
            Debug.Log($"Total objects: {cullableObjects.Count}, Trees: {treeObjects.Count}");
        }
    }

    private void FindLocalPlayerCamera()
    {
        // Find all objects with "Player" tag
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            // Check if this is the local player
            PhotonView pv = player.GetComponent<PhotonView>();
            if (pv != null && pv.IsMine)
            {
                // Get camera from local player
                Camera cam = player.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    cullingCamera = cam;
                    if (showDebugInfo)
                    {
                        Debug.Log($"Found local player camera: {cam.name}");
                    }
                    return;
                }
            }
        }

        // Fallback: If no photon view or not connected, just find first player camera
        if (cullingCamera == null && players.Length > 0)
        {
            Camera cam = players[0].GetComponentInChildren<Camera>();
            if (cam != null)
            {
                cullingCamera = cam;
                if (showDebugInfo)
                {
                    Debug.Log($"Found camera on player: {cam.name}");
                }
            }
        }
    }

    private void Update()
    {
        if (cullingCamera == null && autoFindPlayerCamera)
        {
            FindLocalPlayerCamera();
        }

        if (cullingCamera == null) return;

        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            
            // Use spatial partitioning for trees if enabled
            if (useSpatialPartitioning && treeObjects.Count > 0)
            {
                UpdateTreeCullingSpatial();
            }
            else
            {
                UpdateTreeCulling();
            }
            
            UpdateCulling();
        }
    }

    private Vector2Int GetGridCell(Vector3 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt(position.x / cellSize),
            Mathf.FloorToInt(position.z / cellSize)
        );
    }

    private void BuildSpatialGrid()
    {
        spatialGrid.Clear();
        
        foreach (CullableObject obj in treeObjects)
        {
            if (obj.gameObject == null) continue;
            
            obj.gridCell = GetGridCell(obj.gameObject.transform.position);
            
            if (!spatialGrid.ContainsKey(obj.gridCell))
            {
                spatialGrid[obj.gridCell] = new List<CullableObject>();
            }
            spatialGrid[obj.gridCell].Add(obj);
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"Built spatial grid with {spatialGrid.Count} cells for {treeObjects.Count} trees");
        }
    }

    [ContextMenu("Register All Cullable Objects")]
    public void RegisterAllCullableObjects()
    {
        cullableObjects.Clear();
        treeObjects.Clear();

        // Find all renderers in the scene (unsorted is faster)
        Renderer[] allRenderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        foreach (Renderer renderer in allRenderers)
        {
            // Skip if not in cullable layers
            if (((1 << renderer.gameObject.layer) & cullableLayers) == 0)
                continue;

            // Skip UI elements (Canvas Renderers)
            if (renderer.gameObject.GetComponent<Canvas>() != null || 
                renderer.gameObject.GetComponentInParent<Canvas>() != null)
                continue;

            // Skip player objects entirely
            if (renderer.gameObject.CompareTag("Player") || renderer.transform.root.CompareTag("Player"))
                continue;

            // Skip the player camera's own objects
            if (cullingCamera != null && renderer.transform.IsChildOf(cullingCamera.transform))
                continue;

            GameObject targetObject = renderer.gameObject;
            
            // Check if this specific object already registered
            bool alreadyRegistered = false;
            foreach (var obj in cullableObjects)
            {
                if (obj.gameObject == targetObject)
                {
                    alreadyRegistered = true;
                    break;
                }
            }
            foreach (var obj in treeObjects)
            {
                if (obj.gameObject == targetObject)
                {
                    alreadyRegistered = true;
                    break;
                }
            }
            if (alreadyRegistered) continue;

            // Check if this is a tree
            bool isTree = targetObject.CompareTag("Tree") || 
                          targetObject.transform.root.CompareTag("Tree") ||
                          ((1 << targetObject.layer) & treeLayers) != 0 ||
                          targetObject.name.ToLower().Contains("tree");

            Renderer[] objRenderers = targetObject.GetComponents<Renderer>();
            if (objRenderers.Length > 0)
            {
                Bounds calculatedBounds = CalculateBounds(objRenderers);
                LODGroup lodGroup = targetObject.GetComponent<LODGroup>();
                if (lodGroup == null)
                {
                    lodGroup = targetObject.GetComponentInParent<LODGroup>();
                }

                CullableObject cullObj = new CullableObject
                {
                    gameObject = targetObject,
                    renderers = objRenderers,
                    lodGroup = lodGroup,
                    bounds = calculatedBounds,
                    originalBounds = calculatedBounds,
                    isVisible = true,
                    isTree = isTree,
                    currentLODLevel = 0,
                    lastDistance = 0f
                };

                if (isTree)
                {
                    treeObjects.Add(cullObj);
                }
                else
                {
                    cullableObjects.Add(cullObj);
                }
                
                // Ensure all renderers are enabled initially
                foreach (Renderer r in objRenderers)
                {
                    if (r != null) r.enabled = true;
                }
            }
        }

        totalCount = cullableObjects.Count + treeObjects.Count;
        
        // Build spatial grid for trees
        if (useSpatialPartitioning && treeObjects.Count > 0)
        {
            BuildSpatialGrid();
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"Registered {cullableObjects.Count} regular objects and {treeObjects.Count} trees");
        }
    }

    private Bounds CalculateBounds(Renderer[] renderers)
    {
        if (renderers.Length == 0)
            return new Bounds();

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        return bounds;
    }

    private void UpdateTreeCullingSpatial()
    {
        if (cullingCamera == null) return;

        Vector3 cameraPos = cullingCamera.transform.position;
        Vector2Int cameraCell = GetGridCell(cameraPos);
        
        // Calculate frustum planes once
        if (useFrustumCulling)
        {
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
        }

        // Calculate how many cells we need to check based on culling distance
        int cellRadius = Mathf.CeilToInt(cullingDistance / cellSize);

        // Process only cells within range
        for (int x = cameraCell.x - cellRadius; x <= cameraCell.x + cellRadius; x++)
        {
            for (int z = cameraCell.y - cellRadius; z <= cameraCell.y + cellRadius; z++)
            {
                Vector2Int cellKey = new Vector2Int(x, z);
                if (!spatialGrid.ContainsKey(cellKey)) continue;

                List<CullableObject> cellObjects = spatialGrid[cellKey];
                foreach (CullableObject obj in cellObjects)
                {
                    if (obj.gameObject == null) continue;
                    ProcessTreeObject(obj, cameraPos);
                }
            }
        }

        // Disable trees in cells that are too far (they weren't processed above)
        foreach (var kvp in spatialGrid)
        {
            Vector2Int cell = kvp.Key;
            if (Mathf.Abs(cell.x - cameraCell.x) > cellRadius || 
                Mathf.Abs(cell.y - cameraCell.y) > cellRadius)
            {
                foreach (CullableObject obj in kvp.Value)
                {
                    if (obj.isVisible && obj.gameObject != null)
                    {
                        SetTreeVisibility(obj, false);
                        obj.isVisible = false;
                    }
                }
            }
        }
    }

    private void UpdateTreeCulling()
    {
        if (treeObjects.Count == 0) return;
        if (cullingCamera == null) return;

        Vector3 cameraPos = cullingCamera.transform.position;
        
        if (useFrustumCulling)
        {
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
        }

        // Process trees in batches
        int objectsToProcess = Mathf.Min(maxObjectsPerFrame, treeObjects.Count);
        int startIndex = treeBatchIndex;
        int endIndex = Mathf.Min(startIndex + objectsToProcess, treeObjects.Count);
        
        if (endIndex >= treeObjects.Count)
        {
            treeBatchIndex = 0;
        }
        else
        {
            treeBatchIndex = endIndex;
        }

        for (int i = startIndex; i < endIndex; i++)
        {
            CullableObject obj = treeObjects[i];
            if (obj.gameObject == null) continue;
            ProcessTreeObject(obj, cameraPos);
        }
    }

    private void ProcessTreeObject(CullableObject obj, Vector3 cameraPos)
    {
        Vector3 objectCenter = obj.gameObject.transform.position;
        float sqrDistance = (cameraPos - objectCenter).sqrMagnitude;
        obj.lastDistance = sqrDistance;

        bool shouldBeVisible = true;

        // Distance culling - very aggressive for trees
        if (aggressiveTreeCulling)
        {
            if (sqrDistance > treeFarDistance * treeFarDistance)
            {
                shouldBeVisible = false;
            }
        }
        else if (useDistanceCulling)
        {
            if (sqrDistance > cullingDistance * cullingDistance)
            {
                shouldBeVisible = false;
            }
        }

        // Frustum culling
        if (shouldBeVisible && useFrustumCulling)
        {
            Bounds testBounds = new Bounds(objectCenter, obj.originalBounds.size);
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, testBounds))
            {
                shouldBeVisible = false;
            }
        }

        // Handle LOD for visible trees
        if (shouldBeVisible && obj.lodGroup != null && aggressiveTreeCulling)
        {
            int newLODLevel = 0;
            if (sqrDistance > treeMediumDistance * treeMediumDistance)
            {
                newLODLevel = 2; // Lowest detail
            }
            else if (sqrDistance > treeNearDistance * treeNearDistance)
            {
                newLODLevel = 1; // Medium detail
            }

            if (obj.currentLODLevel != newLODLevel)
            {
                obj.lodGroup.ForceLOD(newLODLevel);
                obj.currentLODLevel = newLODLevel;
            }
        }

        // Only update if visibility changed
        if (obj.isVisible != shouldBeVisible)
        {
            SetTreeVisibility(obj, shouldBeVisible);
            obj.isVisible = shouldBeVisible;
        }
    }

    private void SetTreeVisibility(CullableObject obj, bool visible)
    {
        if (disableTreeGameObjects && obj.isTree)
        {
            // Disable the entire GameObject for maximum performance
            obj.gameObject.SetActive(visible);
        }
        else
        {
            // Just disable renderers
            foreach (Renderer renderer in obj.renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
    }

    private void UpdateCulling()
    {
        if (cullableObjects.Count == 0) return;
        if (cullingCamera == null) return;

        // Calculate frustum planes once
        if (useFrustumCulling)
        {
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
        }

        Vector3 cameraPos = cullingCamera.transform.position;
        
        // Process non-tree objects in batches
        int objectsToProcess = Mathf.Min(maxObjectsPerFrame, cullableObjects.Count);
        int startIndex = currentBatchIndex;
        int endIndex = startIndex + objectsToProcess;
        
        if (endIndex >= cullableObjects.Count)
        {
            endIndex = cullableObjects.Count;
            currentBatchIndex = 0;
            culledCount = 0;
        }
        else
        {
            currentBatchIndex = endIndex;
        }

        for (int i = startIndex; i < endIndex; i++)
        {
            CullableObject obj = cullableObjects[i];
            if (obj.gameObject == null) continue;

            bool shouldBeVisible = true;
            Vector3 objectCenter = obj.gameObject.transform.position;
            
            // Fast distance check first
            if (useDistanceCulling)
            {
                float sqrDistance = (cameraPos - objectCenter).sqrMagnitude;
                if (sqrDistance > cullingDistance * cullingDistance)
                {
                    shouldBeVisible = false;
                }
            }

            // Frustum culling
            if (shouldBeVisible && useFrustumCulling)
            {
                Bounds testBounds = new Bounds(objectCenter, obj.originalBounds.size);
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, testBounds))
                {
                    shouldBeVisible = false;
                }
            }

            // Only update if visibility changed
            if (obj.isVisible != shouldBeVisible)
            {
                SetObjectVisibility(obj, shouldBeVisible);
                obj.isVisible = shouldBeVisible;
            }

            if (!shouldBeVisible && startIndex == 0)
            {
                culledCount++;
            }
        }
    }

    private void SetObjectVisibility(CullableObject obj, bool visible)
    {
        foreach (Renderer renderer in obj.renderers)
        {
            if (renderer != null)
            {
                renderer.enabled = visible;
            }
        }
    }

    public void RegisterObject(GameObject obj, bool isTree = false)
    {
        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            LODGroup lodGroup = obj.GetComponent<LODGroup>();
            CullableObject cullObj = new CullableObject
            {
                gameObject = obj,
                renderers = renderers,
                lodGroup = lodGroup,
                bounds = CalculateBounds(renderers),
                originalBounds = CalculateBounds(renderers),
                isVisible = true,
                isTree = isTree || obj.CompareTag("Tree"),
                currentLODLevel = 0
            };
            
            if (cullObj.isTree)
            {
                treeObjects.Add(cullObj);
                if (useSpatialPartitioning)
                {
                    cullObj.gridCell = GetGridCell(obj.transform.position);
                    if (!spatialGrid.ContainsKey(cullObj.gridCell))
                    {
                        spatialGrid[cullObj.gridCell] = new List<CullableObject>();
                    }
                    spatialGrid[cullObj.gridCell].Add(cullObj);
                }
            }
            else
            {
                cullableObjects.Add(cullObj);
            }
            
            totalCount = cullableObjects.Count + treeObjects.Count;
        }
    }

    public void UnregisterObject(GameObject obj)
    {
        cullableObjects.RemoveAll(c => c.gameObject == obj);
        treeObjects.RemoveAll(c => c.gameObject == obj);
        
        // Also remove from spatial grid
        foreach (var kvp in spatialGrid)
        {
            kvp.Value.RemoveAll(c => c.gameObject == obj);
        }
        
        totalCount = cullableObjects.Count + treeObjects.Count;
    }

    // Force immediate full culling update (useful when teleporting)
    public void ForceFullUpdate()
    {
        if (cullingCamera == null) return;
        
        Vector3 cameraPos = cullingCamera.transform.position;
        frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
        
        foreach (CullableObject obj in treeObjects)
        {
            if (obj.gameObject == null) continue;
            ProcessTreeObject(obj, cameraPos);
        }
        
        foreach (CullableObject obj in cullableObjects)
        {
            if (obj.gameObject == null) continue;
            
            Vector3 objectCenter = obj.gameObject.transform.position;
            float sqrDistance = (cameraPos - objectCenter).sqrMagnitude;
            bool shouldBeVisible = sqrDistance <= cullingDistance * cullingDistance;
            
            if (shouldBeVisible && useFrustumCulling)
            {
                Bounds testBounds = new Bounds(objectCenter, obj.originalBounds.size);
                shouldBeVisible = GeometryUtility.TestPlanesAABB(frustumPlanes, testBounds);
            }
            
            if (obj.isVisible != shouldBeVisible)
            {
                SetObjectVisibility(obj, shouldBeVisible);
                obj.isVisible = shouldBeVisible;
            }
        }
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        int visibleTrees = 0;
        foreach (var t in treeObjects) if (t.isVisible) visibleTrees++;

        GUI.color = Color.white;
        GUI.Label(new Rect(10, 90, 350, 20), $"Regular Objects: {cullableObjects.Count}");
        GUI.Label(new Rect(10, 110, 350, 20), $"Trees Total: {treeObjects.Count}");
        GUI.Label(new Rect(10, 130, 350, 20), $"Trees Visible: {visibleTrees}");
        GUI.Label(new Rect(10, 150, 350, 20), $"Trees Culled: {treeObjects.Count - visibleTrees}");
        GUI.Label(new Rect(10, 170, 350, 20), $"Spatial Grid Cells: {spatialGrid.Count}");
    }
}
