using UnityEngine;
using System.Collections.Generic;

public static class FactionColorUtil
{
    private static readonly Dictionary<string, Color> _cache = new();
    public static Color ColorFromString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return Color.white;

        if (_cache.TryGetValue(input, out var cached))
            return cached;

        if (string.IsNullOrEmpty(input))
            return Color.white;

        // 1. Simple deterministic hash (don’t use GetHashCode!)
        int hash = 17;
        unchecked
        {
            for (int i = 0; i < input.Length; i++)
            {
                hash = hash * 31 + input[i];
            }
        }

        // 2. Map hash → [0,1] range for Hue
        // & 0xFFFFFF to keep it positive-ish and bounded
        float hue = (hash & 0xFFFFFF) / (float)0xFFFFFF;

        // 3. Fix saturation & value so it’s not too dark/washed out
        float saturation = 0.7f;
        float value = 0.9f;

        // 4. Convert HSV → RGB
        Color rgb = Color.HSVToRGB(hue, saturation, value);
        rgb.a = 1f;
        _cache[input] = rgb;
        return rgb;
    }
}
