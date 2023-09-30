using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[System.Serializable]
public record LabelSettings
{
    public float zOffset = -10f;
    public float Padding = .1f;
    public Color textColor = Color.white;
    public float textScale = 0.05f;
    public int fontSize = 32;
}

public class LabelGfx
{
    public record LabelID(GameObject Icon, string Target);
    public record Label(LabelID ID, Vector3 Position);
    private List<Label> labels = new List<Label>();
    private Dictionary<LabelID, GameObject> labelRenderers = new Dictionary<LabelID, GameObject>();
    private HashSet<LabelID> unusedRenderers = new HashSet<LabelID>();
    public Bounds Add(Label label)
    {
        labels.Add(label);
        var bounds = BoundsDictionary.Get(label.ID.Icon);
        bounds.center += label.Position;
        return bounds;
    }
    public void Clear()
    {
        labels.Clear();
    }
    public void Update(LabelSettings settings)
    {
        unusedRenderers.Clear();
        unusedRenderers.AddRange(labelRenderers.Keys);
        foreach (var label in labels)
        {
            if (!labelRenderers.ContainsKey(label.ID))
            {
                var go = new GameObject("Label " + label.ID.Target);
                // Add icon prefab as a child
                var iconBounds = BoundsDictionary.Get(label.ID.Icon);
                var icon = Object.Instantiate(label.ID.Icon, go.transform);
                icon.transform.localPosition = Vector3.zero;
                var iconMaxX = iconBounds.max.x - icon.transform.position.x;
                // Add label text as a child
                var text = new GameObject("Text");
                text.transform.parent = go.transform;
                text.transform.localPosition = Vector3.zero + Vector3.right * iconMaxX + Vector3.right * settings.Padding;
                text.transform.localScale = Vector3.one * settings.textScale;
                var textMesh = text.AddComponent<TextMesh>();
                textMesh.text = label.ID.Target;
                textMesh.fontSize = settings.fontSize;
                textMesh.anchor = TextAnchor.MiddleLeft;
                textMesh.alignment = TextAlignment.Left;
                textMesh.color = settings.textColor;
                // Add renderer
                labelRenderers[label.ID] = go;
            }
            unusedRenderers.Remove(label.ID);

            var labelRenderer = labelRenderers[label.ID];
            labelRenderer.transform.position = label.Position + Vector3.forward * settings.zOffset;
        }
        // Clean unused
        foreach (var leftover in unusedRenderers)
        {
            Object.Destroy(labelRenderers[leftover]);
            labelRenderers.Remove(leftover);
        }
    }
}