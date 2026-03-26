using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class TrackWidthProfile : MonoBehaviour
{
    [System.Serializable]
    public class WidthKey
    {
        [Range(0f, 1f)] public float tf;
        public float leftWidth = 2f;
        public float rightWidth = 2f;
    }

    [SerializeField]
    private List<WidthKey> keys = new List<WidthKey>()
    {
        new WidthKey { tf = 0f, leftWidth = 2f, rightWidth = 2f },
        new WidthKey { tf = 1f, leftWidth = 2f, rightWidth = 2f }
    };

    public float GetLeftWidth(float tf)
    {
        Evaluate(tf, out float left, out _);
        return left;
    }

    public float GetRightWidth(float tf)
    {
        Evaluate(tf, out _, out float right);
        return right;
    }

    public float ClampOffset(float tf, float offset)
    {
        Evaluate(tf, out float left, out float right);
        return Mathf.Clamp(offset, -left, right);
    }

    private void Evaluate(float tf, out float left, out float right)
    {
        left = 2f;
        right = 2f;

        if (keys == null || keys.Count == 0)
            return;

        if (keys.Count == 1)
        {
            left = Mathf.Max(0f, keys[0].leftWidth);
            right = Mathf.Max(0f, keys[0].rightWidth);
            return;
        }

        tf = Mathf.Repeat(tf, 1f);
        keys.Sort((a, b) => a.tf.CompareTo(b.tf));

        WidthKey aKey = keys[0];
        WidthKey bKey = keys[keys.Count - 1];

        for (int i = 0; i < keys.Count - 1; i++)
        {
            if (tf >= keys[i].tf && tf <= keys[i + 1].tf)
            {
                aKey = keys[i];
                bKey = keys[i + 1];
                float t = Mathf.InverseLerp(aKey.tf, bKey.tf, tf);
                left = Mathf.Lerp(aKey.leftWidth, bKey.leftWidth, t);
                right = Mathf.Lerp(aKey.rightWidth, bKey.rightWidth, t);
                left = Mathf.Max(0f, left);
                right = Mathf.Max(0f, right);
                return;
            }
        }

        left = Mathf.Max(0f, keys[keys.Count - 1].leftWidth);
        right = Mathf.Max(0f, keys[keys.Count - 1].rightWidth);
    }
}