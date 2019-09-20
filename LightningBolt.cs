using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LightningBolt : MonoBehaviour {

    [Tooltip("The start offset where the lightning will end at.")]
    public Vector3 startOffset;

    [Tooltip("The end position where the lightning will end at.")]
    public Vector3 endOffset;

    [Range(0, 8)]
    [Tooltip("How many generations? Higher numbers create more line segments.")]
    public int boltSegments = 6;

    [Range(0.0f, 1.0f)]
    [Tooltip("How chaotic should the lightning be? (0-1)")]
    public float chaosFactor = 0.15f;

    public bool chaosDistanceBased = true;

    LineRenderer lineRenderer;
    float startTimeIncrement;
    float stopTimeIncrement;
    int startIndex;

    public void Activate(Gradient elementColor) {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.positionCount = 0;
        if (elementColor != null) {
            lineRenderer.colorGradient = elementColor;
        }
    }

    public void Trigger(Vector3 position, Quaternion rotation) {
        Vector3 start = position + rotation * startOffset;
        Vector3 end = position + rotation * endOffset;
        
        UpdateLineRenderer(Utility.GenerateSegmentedArc(start, end, boltSegments, (chaosDistanceBased ? (end - start).magnitude : 8) * chaosFactor, out startIndex));
    }

    void UpdateLineRenderer(List<KeyValuePair<Vector3, Vector3>> segments) {
        int segmentCount = (segments.Count - startIndex) + 1;
        lineRenderer.positionCount = segmentCount;

        if (segmentCount < 1) {
            return;
        }

        int index = 0;
        lineRenderer.SetPosition(index++, segments[startIndex].Key);

        for (int i = startIndex; i < segments.Count; i++) {
            lineRenderer.SetPosition(index++, segments[i].Value);
        }

        ListPool<KeyValuePair<Vector3, Vector3>>.Add(segments);
    }
}
