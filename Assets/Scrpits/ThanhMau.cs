using UnityEngine;
using UnityEngine.UI;

public class ThanhMau : MonoBehaviour
{
    public Image _thanhmau;

    public void capNhatMau(float luongMauHienTai, float luongMauToiDA)
    {
        _thanhmau.fillAmount = luongMauHienTai / luongMauToiDA;
    }

}
