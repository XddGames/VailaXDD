using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;

public class FrustumCullingManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private Camera cullingCamera;
    [SerializeField] private bool autoFindPlayerCamera = true; // Automatically find local player's camera
    [SerializeField] private float updateInterval = 0.2f; // Check every 0.2 seconds
    [SerializeField] private float cullingDistance = 200f; // Max distance before culling
    [SerializeField] private bool useFrustumCulling = true;
    [SerializeField] private bool useDistanceCulling = true;

    [Header("Target Layers")]
    [SerializeField] private LayerMask cullableLayers = -1; // Which layers to cull

    [Header("Debug")]
    [SerializeField] private bool showDebugInfo = false;

    private List<CullableObject> cullableObjects = new List<CullableObject>();
    private float updateTimer = 0f;
    private Plane[] frustumPlanes;
    private int culledCount = 0;
    private int totalCount = 0;

    private class CullableObject
    {
        public GameObject gameObject;
        public Renderer[] renderers;
        public Bounds bounds;
        public Bounds originalBounds; // Store original bounds
        public bool isVisible;
        public float distanceToCamera;
    }

    private void Start()
    {
        // Delay registration to ensure camera is found
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

        // Only run on local client
        if (!PhotonNetwork.IsConnected || PhotonNetwork.IsConnected)
        {
            RegisterAllCullableObjects();
        }

        if (showDebugInfo)
        {
            Debug.Log($"FrustumCullingManager started with camera: {cullingCamera.name}");
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
        // Auto-find camera if lost (player respawned, etc.)
        if (cullingCamera == null && autoFindPlayerCamera)
        {
            FindLocalPlayerCamera();
        }

        if (cullingCamera == null) return;

        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateCulling();
        }
    }

    [ContextMenu("Register All Cullable Objects")]
    public void RegisterAllCullableObjects()
    {
        cullableObjects.Clear();

        // Find all renderers in the scene
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();

        foreach (Renderer renderer in allRenderers)
        {
            // Skip if not in cullable layers
            if (((1 << renderer.gameObject.layer) & cullableLayers) == 0)
                continue;

            // Skip UI elements
            if (renderer is UnityEngine.UI.Graphic)
                continue;

            // Skip player objects entirely
            if (renderer.gameObject.CompareTag("Player") || renderer.transform.root.CompareTag("Player"))
                continue;

            // Skip the player camera's own objects
            if (cullingCamera != null && renderer.transform.IsChildOf(cullingCamera.transform))
                continue;

            // Register EACH object individually (not by root)
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
            if (alreadyRegistered) continue;

            // Register this individual object with its renderer(s)
            Renderer[] objRenderers = targetObject.GetComponents<Renderer>();
            if (objRenderers.Length > 0)
            {
                Bounds calculatedBounds = CalculateBounds(objRenderers);
                CullableObject cullObj = new CullableObject
                {
                    gameObject = targetObject,
                    renderers = objRenderers,
                    bounds = calculatedBounds,
                    originalBounds = calculatedBounds,
                    isVisible = true
                };
                cullableObjects.Add(cullObj);
                
                // Ensure all renderers are enabled initially
                foreach (Renderer r in objRenderers)
                {
                    if (r != null) r.enabled = true;
                }
            }
        }

        totalCount = cullableObjects.Count;
        if (showDebugInfo)
        {
            Debug.Log($"Registered {totalCount} cullable objects");
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

    private void UpdateCulling()
    {
        if (cullableObjects.Count == 0) return;
        if (cullingCamera == null)
        {
            if (showDebugInfo)
            {
                Debug.LogWarning("Culling camera is null!");
            }
            return;
        }

        // Calculate frustum planes
        if (useFrustumCulling)
        {
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cullingCamera);
        }

        Vector3 cameraPos = cullingCamera.transform.position;
        culledCount = 0;

        foreach (CullableObject obj in cullableObjects)
        {
            if (obj.gameObject == null) continue;

            bool shouldBeVisible = true;

            // Use original bounds centered on the object's current position
            Vector3 objectCenter = obj.gameObject.transform.position;
            Bounds testBounds = new Bounds(objectCenter, obj.originalBounds.size);

            // Distance culling
            if (useDistanceCulling)
            {
                obj.distanceToCamera = Vector3.Distance(cameraPos, objectCenter);
                if (obj.distanceToCamera > cullingDistance)
                {
                    shouldBeVisible = false;
                }
            }

            // Frustum culling
            if (shouldBeVisible && useFrustumCulling)
            {
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, testBounds))
                {
                    shouldBeVisible = false;
                }
            }

            // Debug first few objects
            if (showDebugInfo && Time.frameCount % 300 == 0 && culledCount < 3)
            {
                Debug.Log($"Object {obj.gameObject.name}: visible={shouldBeVisible}, distance={obj.distanceToCamera:F1}m, bounds={testBounds.center}");
            }

            // Update visibility
            if (obj.isVisible != shouldBeVisible)
            {
                SetObjectVisibility(obj, shouldBeVisible);
                obj.isVisible = shouldBeVisible;
            }

            if (!shouldBeVisible)
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

    public void RegisterObject(GameObject obj)
    {
        if (obj == null) return;

        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            CullableObject cullObj = new CullableObject
            {
                gameObject = obj,
                renderers = renderers,
                bounds = CalculateBounds(renderers),
                isVisible = true
            };
            cullableObjects.Add(cullObj);
            totalCount = cullableObjects.Count;
        }
    }

    public void UnregisterObject(GameObject obj)
    {
        cullableObjects.RemoveAll(c => c.gameObject == obj);
        totalCount = cullableObjects.Count;
    }

    private void OnGUI()
    {
        if (!showDebugInfo) return;

        GUI.color = Color.white;
        GUI.Label(new Rect(10, 90, 300, 20), $"Cullable Objects: {totalCount}");
        GUI.Label(new Rect(10, 110, 300, 20), $"Culled Objects: {culledCount}");
        GUI.Label(new Rect(10, 130, 300, 20), $"Visible Objects: {totalCount - culledCount}");
    }
}
