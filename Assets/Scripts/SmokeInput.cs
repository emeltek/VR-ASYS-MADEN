
using UnityEngine;

public class SmokeInput : MonoBehaviour
{
    public SmokeSpawner smokeSpawner;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            smokeSpawner.SpawnSmoke();
        }
    }
}