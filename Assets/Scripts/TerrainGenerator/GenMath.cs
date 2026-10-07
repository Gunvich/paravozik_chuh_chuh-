using System.Collections.Generic;
using UnityEngine;

public static class GenMath
{
    public static bool IsInSector(Vector3 fromPos, Vector3 toPos, Vector3 forwardDir, float minDot)
    {
        Vector3 dirToTarget = (toPos - fromPos).normalized;
        dirToTarget.y = 0;
        forwardDir.y = 0;
        dirToTarget.Normalize();
        forwardDir.Normalize();

        return Vector3.Dot(forwardDir, dirToTarget) >= minDot;
    }

    public static bool IsZoneFree(Collider2D zoneA, LayerMask coreLayer)
    {
        if (zoneA == null) return true; // ���� �� ������ ����� ��� Zone A

        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(coreLayer);
        filter.useLayerMask = true;

        List<Collider2D> results = new List<Collider2D>();
        zoneA.Overlap(filter, results);

        foreach (var col in results)
        {
            if (col != zoneA) return false; // ������� ������� � ����� �����
        }
        return true;
    }
}
