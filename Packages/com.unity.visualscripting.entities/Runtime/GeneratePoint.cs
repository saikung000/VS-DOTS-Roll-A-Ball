using System;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.VisualScripting;
using Random = UnityEngine.Random;

[Node]
[PublicAPI]
public static class GeneratePoint
{
    public static NativeArray<float3> RandomPointsOnCircle(float3 center, float radius, int count)
    {
        var points = new NativeArray<float3>(count, Allocator.Temp);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0.0f, Mathf.PI * 2.0f);
            points[i] = center + new float3
            {
                x = math.sin(angle) * radius,
                y = 0,
                z = math.cos(angle) * radius
            };
        }
        return points;
    }
}
