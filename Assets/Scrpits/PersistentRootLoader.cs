// PersistentRootLoader.cs
using UnityEngine;

public static class PersistentRootLoader
{
    public static void Ensure(GameObject prefabRef)
    {
        if (Object.FindFirstObjectByType<PersistentRoot>() != null) return;

        if (prefabRef != null)
        {
            Object.Instantiate(prefabRef);
            Debug.Log("✅ PersistentRoot created from Inspector reference.");
            return;
        }

        // fallback: Resources (tuỳ bạn giữ hay bỏ)
        var res = Resources.Load<GameObject>("PersistentRoot");
        if (res != null)
        {
            Object.Instantiate(res);
            Debug.Log("✅ PersistentRoot created from Resources.");
        }
        else
        {
            Debug.LogError("❌ PersistentRoot prefab missing (Inspector ref null & Resources not found).");
        }
    }
}
