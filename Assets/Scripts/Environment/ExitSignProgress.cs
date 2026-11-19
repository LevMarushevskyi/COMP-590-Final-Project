using UnityEngine;

public class ExitSignProgress : MonoBehaviour
{
    [Tooltip("Lit in this order, one per solved puzzle.")]
    [SerializeField] Renderer[] signs;

    [Header("Look")]
    [SerializeField] Color onGlow = Color.white;
    [SerializeField] float onGlowIntensity = 1f;
    [Tooltip("Tint while unlit, so the sign reads as switched off.")]
    [SerializeField] Color offTint = new Color(0.4f, 0.4f, 0.4f);

    static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    MaterialPropertyBlock block;
    int litCount = -1;

    void OnEnable()
    {
        block ??= new MaterialPropertyBlock();
        litCount = -1;
        Refresh();
    }

    void Update() => Refresh();

    void Refresh()
    {
        int solved = GameManager.Instance != null ? GameManager.Instance.SolvedCount : 0;
        if (solved == litCount) return;
        litCount = solved;
        for (int i = 0; i < signs.Length; i++) Set(signs[i], i < solved);
    }

    void Set(Renderer r, bool on)
    {
        if (r == null) return;
        r.GetPropertyBlock(block);
        block.SetColor(EmissionId, on ? onGlow * onGlowIntensity : Color.black);
        block.SetColor(BaseColorId, on ? Color.white : offTint);
        r.SetPropertyBlock(block);
    }
}
