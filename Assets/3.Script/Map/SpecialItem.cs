using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class SpecialItem : NetworkBehaviour
{
    [SerializeField] float returnDelay = 10f;

    [SyncVar(hook = nameof(OnHeldChanged))] bool isHeld;
    [SyncVar(hook = nameof(OnPosChanged))] Vector3 groundPos;

    private Vector3 homePos;
    private Coroutine return_co;
    private Renderer[] renderers;
    private Collider[] colliders;

    private void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
    }

    public override void OnStartServer()
    {
        homePos = transform.position;
        groundPos = homePos;
    }

    [Server]
    public bool TryPickUp()
    {
        if (isHeld) return false;    
        StopReturn();
        isHeld = true;
        ApplyHeld(true);
        return true;
    }

    [Server]
    public void Drop(Vector3 pos)     
    {
        groundPos = pos;
        transform.position = pos;
        isHeld = false;
        ApplyHeld(false);
        StopReturn();
        return_co = StartCoroutine(ReturnAfterDelay());
    }

    [Server]
    public void ReleaseForThrow()
    {
        StopReturn();
        isHeld = false;
        ApplyHeld(false);
    }

    private IEnumerator ReturnAfterDelay()
    {
        yield return new WaitForSeconds(returnDelay);
        groundPos = homePos;
        transform.position = homePos;
        return_co = null;
    }

    private void StopReturn()
    {
        if (return_co != null) { StopCoroutine(return_co); return_co = null; }
    }

    private void OnHeldChanged(bool _, bool held) => ApplyHeld(held);
    private void OnPosChanged(Vector3 _, Vector3 pos) => transform.position = pos;

    private void ApplyHeld(bool held)
    {
        foreach (var r in renderers) r.enabled = !held;
        foreach (var c in colliders) c.enabled = !held;
    }
}
