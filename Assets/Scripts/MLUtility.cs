// TransformSearchUtils.cs
// Drop this anywhere in your project. Provides recursive search utilities.

using System;
using UnityEngine;

public static class MLUtility
{
    /// <summary>
    /// Recursively searches the hierarchy under <paramref name="root"/> for a Transform whose
    /// name matches <paramref name="targetName"/>. Returns the first match (depth-first).
    /// </summary>
    public static Transform FindInChildrenRecursive(Transform root, string targetName, StringComparison comparison = StringComparison.Ordinal)
    {
        if (root == null || string.IsNullOrEmpty(targetName))
            return null;

        // Check the current node
        if (string.Equals(root.name, targetName, comparison))
            return root;

        // Recurse through children
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            // Depth-first: search this child subtree
            Transform match = FindInChildrenRecursive(child, targetName, comparison);
            if (match != null)
                return match;
        }

        // No match in this branch
        return null;
    }

    /// <summary>
    /// Convenience wrapper that returns the GameObject instead of Transform.
    /// </summary>
    public static GameObject FindGameObjectInChildrenRecursive(Transform root, string targetName, StringComparison comparison = StringComparison.Ordinal)
    {
        var t = FindInChildrenRecursive(root, targetName, comparison);
        return t != null ? t.gameObject : null;
    }

    /// <summary>
    /// Recursively logs every object in the hierarchy under <paramref name="root"/>.
    /// </summary>
    public static void DebugHierarchyRecursive(Transform root, int depth = 0)
    {
        if (root == null)
        {
            Debug.LogWarning($"[{nameof(DebugHierarchyRecursive)}] root is null.");
            return;
        }

        // Indent by depth to visualize hierarchy (safe for all environments)
        string indent = "".PadLeft(depth * 2, '-');
        Debug.Log($"{indent}{root.name}");

        // Recurse through children
        for (int i = 0; i < root.childCount; i++)
        {
            DebugHierarchyRecursive(root.GetChild(i), depth + 1);
        }
    }
    /// <summary>
    /// Recursively searches the hierarchy under <paramref name="root"/> for a Transform 
    /// whose name matches <paramref name="targetName"/>, ignoring "(Clone)" suffix and whitespace.
    /// </summary>
    public static Transform FindIgnoringClone(Transform root, string targetName, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
    {
        if (root == null || string.IsNullOrWhiteSpace(targetName))
            return null;

        // Clean both names: remove "(Clone)" and trim whitespace
        string CleanName(string name) => name.Replace("(Clone)", "").Trim();

        string cleanRootName = CleanName(root.name);
        string cleanTargetName = CleanName(targetName);

        if (string.Equals(cleanRootName, cleanTargetName, comparison))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = FindIgnoringClone(root.GetChild(i), targetName, comparison);
            if (child != null)
                return child;
        }

        return null;
    }

}
