using UnityEngine;

public class PlayerMask : MonoBehaviour
{
    [Header("Mask State")]
    public bool HasMaskOn = false;

    public void SetMaskState(bool on)
    {
        HasMaskOn = on;
    }

    public float GetMaskEffect()
    {
        return HasMaskOn ? 0f : 1f;
    }
}