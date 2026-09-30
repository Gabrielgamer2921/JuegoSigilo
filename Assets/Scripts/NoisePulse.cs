using UnityEngine;

public class NoisePulse : MonoBehaviour
{
    private const int Segments = 64;

    private LineRenderer line;
    private Color color;
    private float maxRadius;
    private float duration;
    private float elapsed;

    public static void Spawn(Vector3 center, float radius, Color color, float duration)
    {
        GameObject pulseObject = new GameObject("PulsoRuido");
        pulseObject.transform.position = center;
        pulseObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        NoisePulse pulse = pulseObject.AddComponent<NoisePulse>();
        pulse.Begin(radius, color, duration);
    }

    private void Begin(float radius, Color pulseColor, float time)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Destroy(gameObject);
            return;
        }

        maxRadius = radius;
        color = pulseColor;
        duration = Mathf.Max(0.1f, time);

        line = gameObject.AddComponent<LineRenderer>();
        line.loop = true;
        line.useWorldSpace = true;
        line.positionCount = Segments;
        line.widthMultiplier = 0.12f;
        line.alignment = LineAlignment.TransformZ;
        line.material = new Material(shader);
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    private void Update()
    {
        if (line == null) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        float eased = 1f - (1f - t) * (1f - t);
        float radius = Mathf.Lerp(0.5f, maxRadius, eased);

        Color current = color;
        current.a = color.a * (1f - t);
        line.startColor = current;
        line.endColor = current;

        Vector3 center = transform.position;
        for (int i = 0; i < Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / Segments;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            line.SetPosition(i, center + offset);
        }

        if (t >= 1f) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (line != null) Destroy(line.material);
    }
}
