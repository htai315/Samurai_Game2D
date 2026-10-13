using UnityEngine;

public class CitadelBackdrop : MonoBehaviour
{
    public float horizontalParallax = .15f;
    private Vector3 origin;
    private Camera view;
    private void Start() { origin = transform.position; view = Camera.main; }
    private void LateUpdate()
    {
        if (!view) view = Camera.main;
        if (view) transform.position = origin + Vector3.right * view.transform.position.x * horizontalParallax;
    }
}
