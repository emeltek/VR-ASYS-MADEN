using System.Collections.Generic;
using UnityEngine;

public class SmokeSpawner : MonoBehaviour
{
    [Header("Particle System")]
    public ParticleSystem particlePrefab;

    [Header("Spawn Settings")]
    public float startX = -10f;
    public float endX = 50f;
    public float stepX = 5f;
    public float fixedY = 0f;
    public float fixedZ = 0f;

    private List<ParticleSystem> spawnedSystems = new List<ParticleSystem>();

    public void SpawnSmoke()
    {
        ClearSmoke();

        for (float x = startX; x <= endX; x += stepX)
        {
            Vector3 spawnPos = new Vector3(x, fixedY, fixedZ);
            ParticleSystem ps = Instantiate(particlePrefab, spawnPos, Quaternion.identity);
            spawnedSystems.Add(ps);
        }
    }

    public void ClearSmoke()
    {
        foreach (ParticleSystem ps in spawnedSystems)
        {
            if (ps != null)
                Destroy(ps.gameObject);
        }
        spawnedSystems.Clear();
    }
}