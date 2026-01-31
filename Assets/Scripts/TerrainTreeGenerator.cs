using UnityEngine;

public class TerrainTreeGenerator : MonoBehaviour
{
    [Header("Terrain Settings")]
    [SerializeField] private Terrain terrain;

    [Header("Tree Prefabs")]
    [SerializeField] private GameObject[] treePrefabs; // Drag your tree prefabs here

    [Header("Generation Settings")]
    [SerializeField] private int treeCount = 500;
    [SerializeField] private float minScale = 0.8f;
    [SerializeField] private float maxScale = 1.5f;
    [SerializeField] private float minDistance = 5f; // Minimum distance between trees
    [SerializeField] private LayerMask groundLayer = -1; // What layers to spawn on
    
    [Header("Terrain Bounds")]
    [SerializeField] private Vector2 minBounds = Vector2.zero; // Percentage (0-1) of terrain
    [SerializeField] private Vector2 maxBounds = Vector2.one; // Percentage (0-1) of terrain
    
    [Header("Slope Settings")]
    [SerializeField] private bool checkSlope = true;
    [SerializeField] private float maxSlope = 45f; // Maximum slope angle for tree placement

    [Header("Exclusion Zones")]
    [SerializeField] private bool useExclusionZones = true;
    [SerializeField] private LayerMask exclusionLayers; // Objects on these layers prevent tree spawning nearby
    [SerializeField] private float exclusionRadius = 15f; // Don't spawn trees within this distance of exclusion objects

    [Header("Performance Optimization")]
    [SerializeField] private bool forceLODLevel = false;
    [SerializeField] [Range(0, 2)] private int lodLevelToUse = 1; // 0=highest quality, 2=lowest quality
    [SerializeField] private bool removeUnnecessaryComponents = true; // Remove colliders, animations, etc.
    [SerializeField] private bool combineMeshes = false; // Combine trees into fewer objects (WIP)
    
    [Header("Organization")]
    [SerializeField] private bool createParentObject = true;
    [SerializeField] private string parentName = "Generated Trees";

    private Transform treeParent;

    public void GenerateTrees()
    {
        if (terrain == null)
        {
            Debug.LogError("No terrain assigned!");
            return;
        }

        if (treePrefabs == null || treePrefabs.Length == 0)
        {
            Debug.LogError("No tree prefabs assigned!");
            return;
        }

        // Create parent object
        if (createParentObject)
        {
            GameObject parentObj = GameObject.Find(parentName);
            if (parentObj == null)
            {
                parentObj = new GameObject(parentName);
            }
            treeParent = parentObj.transform;
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        int treesPlaced = 0;
        int attempts = 0;
        int maxAttempts = treeCount * 10; // Prevent infinite loop

        while (treesPlaced < treeCount && attempts < maxAttempts)
        {
            attempts++;

            // Random position within terrain bounds (percentage)
            float randomX = Random.Range(minBounds.x, maxBounds.x);
            float randomZ = Random.Range(minBounds.y, maxBounds.y);

            // Convert to world position
            Vector3 worldPos = terrainPos + new Vector3(
                randomX * terrainSize.x,
                0,
                randomZ * terrainSize.z
            );

            // Raycast down to find ground
            RaycastHit hit;
            if (Physics.Raycast(worldPos + Vector3.up * 1000f, Vector3.down, out hit, 2000f, groundLayer))
            {
                // Check slope
                if (checkSlope)
                {
                    float slope = Vector3.Angle(hit.normal, Vector3.up);
                    if (slope > maxSlope)
                    {
                        continue; // Too steep
                    }
                }

                // Check exclusion zones (buildings, roads, etc.)
                if (useExclusionZones && exclusionRadius > 0)
                {
                    Collider[] exclusionObjects = Physics.OverlapSphere(hit.point, exclusionRadius, exclusionLayers);
                    if (exclusionObjects.Length > 0)
                    {
                        continue; // Too close to excluded object
                    }
                }

                // Check minimum distance from other trees
                if (minDistance > 0)
                {
                    Collider[] nearbyObjects = Physics.OverlapSphere(hit.point, minDistance);
                    bool tooClose = false;
                    foreach (Collider col in nearbyObjects)
                    {
                        if (col.gameObject.CompareTag("Tree") || (treeParent != null && col.transform.IsChildOf(treeParent)))
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;
                }

                // Pick random tree prefab
                GameObject treePrefab = treePrefabs[Random.Range(0, treePrefabs.Length)];

                // Instantiate tree
                GameObject tree = Instantiate(treePrefab, hit.point, Quaternion.identity);

                // Random rotation
                tree.transform.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

                // Random scale
                float scale = Random.Range(minScale, maxScale);
                tree.transform.localScale = Vector3.one * scale;

                // Optimization: Force LOD level if enabled
                if (forceLODLevel)
                {
                    LODGroup lodGroup = tree.GetComponentInChildren<LODGroup>();
                    if (lodGroup != null)
                    {
                        lodGroup.ForceLOD(lodLevelToUse);
                    }
                }

                // Optimization: Remove unnecessary components
                if (removeUnnecessaryComponents)
                {
                    OptimizeTree(tree);
                }

                // Set parent
                if (treeParent != null)
                {
                    tree.transform.SetParent(treeParent);
                }

                // Optional: Tag for future reference
                if (!tree.CompareTag("Tree"))
                {
                    tree.tag = "Tree";
                }

                treesPlaced++;
            }
        }

        Debug.Log($"Successfully placed {treesPlaced} trees out of {treeCount} requested (took {attempts} attempts)");

#if UNITY_EDITOR
        // Mark scene as dirty so changes are saved
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
#endif
    }

    private void OptimizeTree(GameObject tree)
    {
        // Remove Animator components (animations usually not needed for static trees)
        Animator[] animators = tree.GetComponentsInChildren<Animator>();
        foreach (Animator anim in animators)
        {
            DestroyImmediate(anim);
        }

        // Remove Animation components
        Animation[] animations = tree.GetComponentsInChildren<Animation>();
        foreach (Animation anim in animations)
        {
            DestroyImmediate(anim);
        }

        // Remove colliders (trees usually don't need physics collisions)
        Collider[] colliders = tree.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            DestroyImmediate(col);
        }

        // Remove Rigidbody components
        Rigidbody[] rigidbodies = tree.GetComponentsInChildren<Rigidbody>();
        foreach (Rigidbody rb in rigidbodies)
        {
            DestroyImmediate(rb);
        }

        // Mark renderers as static for better batching
        MeshRenderer[] renderers = tree.GetComponentsInChildren<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            renderer.gameObject.isStatic = true;
        }
    }

    [ContextMenu("Clear All Trees")]
    public void ClearAllTrees()
    {
        if (treeParent != null)
        {
            DestroyImmediate(treeParent.gameObject);
            Debug.Log("Cleared all trees.");
        }
        else
        {
            GameObject parentObj = GameObject.Find(parentName);
            if (parentObj != null)
            {
                DestroyImmediate(parentObj);
                Debug.Log("Cleared all trees.");
            }
            else
            {
                Debug.LogWarning("No tree parent found to clear.");
            }
        }

#if UNITY_EDITOR
        // Mark scene as dirty so changes are saved
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
#endif
    }

    private void OnDrawGizmosSelected()
    {
        if (terrain == null) return;

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPos = terrain.transform.position;
        Vector3 terrainSize = terrainData.size;

        // Draw bounds
        Vector3 minPos = terrainPos + new Vector3(minBounds.x * terrainSize.x, 0, minBounds.y * terrainSize.z);
        Vector3 maxPos = terrainPos + new Vector3(maxBounds.x * terrainSize.x, 0, maxBounds.y * terrainSize.z);

        Gizmos.color = Color.green;
        Vector3 size = new Vector3(
            (maxBounds.x - minBounds.x) * terrainSize.x,
            10f,
            (maxBounds.y - minBounds.y) * terrainSize.z
        );
        Vector3 center = (minPos + maxPos) / 2f;
        center.y = terrainPos.y + 5f;
        Gizmos.DrawWireCube(center, size);
    }
}
