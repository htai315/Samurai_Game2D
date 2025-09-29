using UnityEngine;

[CreateAssetMenu(menuName = "Game2D/CharacterConfig", fileName = "CharacterConfig")]
public class CharacterConfig : ScriptableObject
{
    [Header("Health")]
    public float luongMauToiDa = 10f;
    public float hurtStunDuration = 0.3f;

    [Header("Movement")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;
    public int maxJumps = 2;

    [Header("Dash")]
    public float dashDistance = 3f;
    public float dashDuration = 0.12f;
    public float dashSkin = 0.02f;
}
