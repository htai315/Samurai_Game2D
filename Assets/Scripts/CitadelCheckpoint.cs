using UnityEngine;

public class CitadelCheckpoint : MonoBehaviour
{
    public CitadelLevel level;
    public int index;
    public Transform respawnPoint;
    public SpriteRenderer beacon;
    public string SaveId => gameObject.scene.name + ":beacon:" + index;
    public void SetLit() { if (beacon) beacon.color = new Color(.35f, 1f, .95f); }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var player = other.GetComponentInParent<PlayerController1>();
        if (!player || !player.CanAct || !level) return;
        level.Activate(this, true);
    }
    private void OnTriggerStay2D(Collider2D other) => OnTriggerEnter2D(other);
}
