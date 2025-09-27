using UnityEngine;

public class DustEvents : MonoBehaviour
{
    
    public void OnDustEnd()
    {
        Destroy(gameObject);
    }
}
