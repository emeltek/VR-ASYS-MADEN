
using UnityEngine;

public class SmokeInput : MonoBehaviour
{
    public SmokeSpawner smokeSpawner;

    // Test amaçlı: Space ile göçüğü başlatır. Build'de (sınavda) çalışmaz.
    void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.Space))
        {
            smokeSpawner.SpawnSmoke();
        }
#endif
    }
}