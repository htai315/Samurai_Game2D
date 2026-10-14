using TMPro;
using UnityEngine;

public class FloatingDamageText : MonoBehaviour
{
    public TMP_Text text;
    public float moveUpSpeed = 1.5f;
    public float lifetime = 0.8f;
    private float timer;

    public void Init(float damage)
    {
        if (text) text.text = "-" + damage.ToString();
    }

    void Update()
    {
        transform.position += Vector3.up * moveUpSpeed * Time.deltaTime;
        timer += Time.deltaTime;
        if (timer >= lifetime) Destroy(gameObject);
    }
}
