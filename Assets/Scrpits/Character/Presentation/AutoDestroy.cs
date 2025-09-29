using UnityEngine;

public sealed class AutoDestroy : MonoBehaviour
{
    [SerializeField] private float lifetime = 1f;
    void OnEnable() => Destroy(gameObject, Mathf.Max(0.01f, lifetime));
}
