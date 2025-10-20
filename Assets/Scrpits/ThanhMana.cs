using UnityEngine;
using UnityEngine.UI;

public class ThanhMana : MonoBehaviour
{
    public Image _thanhmana;

    // Cập nhật lượng mana hiện tại trên thanh (0–1)
    public void capNhatMana(float luongManaHienTai, float luongManaToiDa)
    {
        _thanhmana.fillAmount = luongManaHienTai / luongManaToiDa;
    }
}
