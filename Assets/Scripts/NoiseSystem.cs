using System;
using UnityEngine;

public static class NoiseSystem
{
    public static event Action<Vector3, float> NoiseEmitted;

    public static void Emit(Vector3 position, float radius)
    {
        NoiseEmitted?.Invoke(position, radius);
    }
}
