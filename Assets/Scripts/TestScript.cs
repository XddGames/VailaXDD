//for tests only, to be deleted later

using UnityEngine;

public class TestScript : MonoBehaviour
{
    public Camera cam;
    public EnemyBase enemy;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit))
            {
                // Assuming the plane has a collider
                enemy.SetDestination(hit.point);
            }
        }
    }
}