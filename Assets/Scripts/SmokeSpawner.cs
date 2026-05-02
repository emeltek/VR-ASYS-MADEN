using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SmokeSpawner : MonoBehaviour
{
    [Header("Particle System")]
    public ParticleSystem particlePrefab;

    [Header("Spawn Settings")]
    private float startX;
    public float endX = 50f;
    public float stepX = 5f;
    private float fixedY = 0f;
    private float fixedZ = 0f;
    public float spawnInterval = 3f;

    public Action onAllSmokesSpawned;

    private List<ParticleSystem> spawnedSystems = new List<ParticleSystem>();

    private void Start()
    {
        fixedY = particlePrefab.transform.position.y;
        fixedZ = particlePrefab.transform.position.z;
        startX = particlePrefab.transform.position.x;
    }

    public void SpawnSmoke()
    {
        ClearSmoke();
        StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        float step = startX > endX ? -Mathf.Abs(stepX) : Mathf.Abs(stepX);

        for (float x = startX; startX < endX ? x <= endX : x >= endX; x += step)
        {
            Vector3 spawnPos = new Vector3(x, fixedY, fixedZ);
            ParticleSystem ps = Instantiate(particlePrefab, spawnPos, Quaternion.identity);
            spawnedSystems.Add(ps);

            yield return new WaitForSeconds(spawnInterval);
        }

        onAllSmokesSpawned?.Invoke();
    }

    public void ClearSmoke()
    {
        StopAllCoroutines();
        foreach (ParticleSystem ps in spawnedSystems)
        {
            if (ps != null)
                Destroy(ps.gameObject);
        }
        spawnedSystems.Clear();
    }
}