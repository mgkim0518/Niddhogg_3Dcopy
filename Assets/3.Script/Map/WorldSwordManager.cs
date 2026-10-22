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

    [SerializeField] private GameObject swordPrefab; // NetworkIdentity 필요, NetworkManager Spawnable Prefabs에 등록
    private const int MaxSwords = 20;
    private readonly List<GameObject> swords = new();

    public void SpawnSword(Vector3 pos, Quaternion rot)
    {
        if (!NetworkServer.active)
        {
            return;
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
