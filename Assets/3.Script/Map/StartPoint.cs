using System.Collections.Generic;
using UnityEngine;

public class StartPoint : MonoBehaviour
{
    public Team team;

    static readonly List<StartPoint> all = new List<StartPoint>();

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public static Transform For(Team team)
    {
        foreach (StartPoint p in all)
            if (p.team == team) return p.transform;
        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = team == Team.Red ? Color.red : team == Team.Blue ? Color.blue : Color.gray;
        Vector3 center = transform.position + Vector3.up;
        Gizmos.DrawWireCube(center, new Vector3(0.8f, 2f, 0.8f));
        Gizmos.DrawRay(center, transform.forward); 
    }
}
