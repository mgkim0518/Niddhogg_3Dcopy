using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class WorldSwordManager : MonoBehaviour
{
    public static WorldSwordManager Instance;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    [SerializeField] private GameObject swordPrefab;
    private const int MaxSwords = 20;
    private readonly List<GameObject> swords = new();

    public GameObject SpawnSword(Vector3 pos, Quaternion rot)
    {
        if (!NetworkServer.active)
        {
            return null;
        }

        if (swords.Count >= MaxSwords)
        {
            GameObject oldest = swords[0];
            swords.RemoveAt(0);
            if (oldest != null)
            {
                NetworkServer.Destroy(oldest);
            }
        }

        GameObject s = Instantiate(swordPrefab, pos, rot);
        NetworkServer.Spawn(s);
        swords.Add(s);
        return s;
    }

    public bool TryPickUp(GameObject sword)
    {
        if (!swords.Remove(sword))
        {
            return false;
        }
        NetworkServer.Destroy(sword);
        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

    }
}
