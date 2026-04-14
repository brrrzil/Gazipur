using UnityEngine;

[CreateAssetMenu(fileName = "NewColor",menuName = "MyGame/System/Color",order =0)]
public class ColorCode : ScriptableObject
{
    [SerializeField] private Color _color;
    [SerializeField] private string _id;
    public string colorCode => ColorUtility.ToHtmlStringRGB(_color);
    public string id => _id;
}
