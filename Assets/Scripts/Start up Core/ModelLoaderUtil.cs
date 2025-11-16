using UnityEngine;
using System.IO;
#if BARRACUDA_PRESENT
using Unity.Barracuda;
#else
using NNModel = System.Object;
#endif

public static class ModelLoaderUtil
{
#if BARRACUDA_PRESENT
    // relativePath should be relative to StreamingAssets
    public static NNModel LoadModelFromStreamingAssets(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath)) return null;
        string fullPath = Path.Combine(UnityEngine.Application.streamingAssetsPath, relativePath);
        if (!File.Exists(fullPath))
        {
            UnityEngine.Debug.LogWarning($"[ModelLoader] ONNX not found at {fullPath}");
            return null;
        }

        // Try to load by asset name (without extension) if imported as NNModel
        string assetName = Path.GetFileNameWithoutExtension(relativePath);
        var nnAsset = Resources.Load<NNModel>(assetName);
        if (nnAsset != null) return nnAsset;

        UnityEngine.Debug.LogWarning($"[ModelLoader] Could not load NNModel asset for {relativePath}. Ensure it is imported as NNModel (Barracuda).");
        return null;
    }
#else
    // Barracuda not present; stub out
    public static NNModel LoadModelFromStreamingAssets(string relativePath) => null;
#endif
}
