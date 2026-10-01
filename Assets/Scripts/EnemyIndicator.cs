using UnityEngine;

[RequireComponent(typeof(EnemyAI))]
public class EnemyIndicator : MonoBehaviour
{
    [SerializeField] private float height = 2.6f;
    [SerializeField] private float characterSize = 0.18f;
    [SerializeField] private int fontSize = 64;

    private EnemyAI ai;
    private TextMesh text;
    private Transform marker;
    private Transform cam;

    private void Awake()
    {
        ai = GetComponent<EnemyAI>();

        GameObject go = new GameObject("Indicador");
        marker = go.transform;
        marker.SetParent(transform, false);

        text = go.AddComponent<TextMesh>();
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = characterSize;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.text = "";

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
        {
            text.font = font;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }
    }

    private void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main == null) return;
            cam = Camera.main.transform;
        }

        marker.position = transform.position + Vector3.up * height;
        marker.rotation = Quaternion.LookRotation(marker.position - cam.position);

        float inverseScale = 1f / Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.y));
        marker.localScale = Vector3.one * inverseScale;

        switch (ai.CurrentState)
        {
            case EnemyState.Alert:
                text.text = "?";
                text.color = new Color(1f, 0.9f, 0.2f);
                break;
            case EnemyState.Investigate:
                text.text = "?";
                text.color = new Color(1f, 0.55f, 0.1f);
                break;
            case EnemyState.Chase:
                text.text = "!";
                text.color = new Color(1f, 0.2f, 0.2f);
                break;
            default:
                text.text = "";
                break;
        }
    }
}
