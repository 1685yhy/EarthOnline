using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Applies a vertical color gradient to a Unity legacy Text component.
/// Used for the gold gradient score display (white top -> #ffd700 bottom).
/// </summary>
[RequireComponent(typeof(Text))]
public class GradientText : BaseMeshEffect
{
    [SerializeField] private Color topColor = Color.white;
    [SerializeField] private Color bottomColor = new Color32(0xFF, 0xD7, 0x00, 0xFF); // #ffd700

    public Color TopColor { get => topColor; set { topColor = value; graphic.SetVerticesDirty(); } }
    public Color BottomColor { get => bottomColor; set { bottomColor = value; graphic.SetVerticesDirty(); } }

    public override void ModifyMesh(VertexHelper vh)
    {
        if (!IsActive()) return;

        var vertexList = new List<UIVertex>();
        vh.GetUIVertexStream(vertexList);

        if (vertexList.Count == 0) return;

        float bottomY = vertexList[0].position.y;
        float topY = vertexList[0].position.y;

        for (int i = 1; i < vertexList.Count; i++)
        {
            float y = vertexList[i].position.y;
            if (y > topY) topY = y;
            if (y < bottomY) bottomY = y;
        }

        float height = topY - bottomY;

        for (int i = 0; i < vertexList.Count; i++)
        {
            UIVertex v = vertexList[i];
            float t = height == 0 ? 0 : Mathf.Clamp01((v.position.y - bottomY) / height);
            v.color = Color.Lerp(bottomColor, topColor, t);
            vertexList[i] = v;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(vertexList);
    }
}
