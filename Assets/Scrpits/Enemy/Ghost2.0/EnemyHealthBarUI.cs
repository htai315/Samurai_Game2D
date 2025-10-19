using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBarUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 1.2f, 0);
    [SerializeField] private float smooth = 10f;

    private Transform target;
    private RectTransform rect;
    private Canvas canvas;
    private Camera cam;
    private float targetValue;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        cam = Camera.main;
        if (!slider) slider = GetComponentInChildren<Slider>();
    }

    public void Bind(Transform followTarget, int max, int current)
    {
        target = followTarget;
        slider.maxValue = max;
        slider.value = current;
        targetValue = current;
        UpdatePositionImmediate();
    }

    public void UpdateHealth(int current, int max)
    {
        if (slider.maxValue != max)
            slider.maxValue = max;
        targetValue = Mathf.Clamp(current, 0, max);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!target) return;

        // Cập nhật vị trí
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
        {
            rect.position = cam.WorldToScreenPoint(target.position + worldOffset);
        }
        else
        {
            transform.position = target.position + worldOffset;
            transform.rotation = Quaternion.identity;
        }

        // Làm mượt thanh máu
        if (slider)
            slider.value = Mathf.Lerp(slider.value, targetValue, Time.deltaTime * smooth);
    }

    public void UpdatePositionImmediate()
    {
        if (!target) return;
        if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            rect.position = cam.WorldToScreenPoint(target.position + worldOffset);
        else
            transform.position = target.position + worldOffset;
    }
}
