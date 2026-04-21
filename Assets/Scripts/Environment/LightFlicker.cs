using UnityEngine;

public class LightFlicker : MonoBehaviour
{
    Light fireplace;

    private void Start()
    {
        fireplace = GetComponent<Light>();
    }

    void Update()
    {
        fireplace.intensity = Random.Range(45f, 55f);
    }
}
