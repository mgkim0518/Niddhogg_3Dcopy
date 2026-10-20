using System.Collections.Generic;
using UnityEngine;

// 각 성의 계단 밑 공간에 빈 오브젝트로 두고 team을 지정
public class RespawnPoint : MonoBehaviour
{
    public Team team;

    static readonly List<RespawnPoint> all = new List<RespawnPoint>();

    void OnEnable() => all.Add(this);
    void OnDisable() => all.Remove(this);

    public static Transform For(Team team)
    {
        foreach (RespawnPoint p in all)
            if (p.team == team) return p.transform;
        return null;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = team == Team.Red ? Color.red : team == Team.Blue ? Color.blue : Color.gray;
        Vector3 center = transform.position + Vector3.up;
        Gizmos.DrawWireSphere(center, 0.5f);
        Gizmos.DrawRay(center, transform.forward);   // 부활 후 바라볼 방향
    }
}
