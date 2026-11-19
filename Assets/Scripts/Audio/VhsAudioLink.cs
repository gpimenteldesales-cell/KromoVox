using UnityEngine;

/// <summary>
/// Liga a intensidade do efeito VHS (visual) ao chiado de fita da musica.
/// Opcao A: arraste o Material do shader e o nome da propriedade float (ex.: _VhsIntensity).
/// Opcao B: deixe sem material e altere "manual" (por Animator, script, Volume, etc.).
///
/// OTIMIZACOES: o ID da propriedade e resolvido uma vez (nada de strings por frame) e o
/// AudioManager so e avisado quando o valor realmente muda.
/// </summary>
public class VhsAudioLink : MonoBehaviour
{
    public Material material;
    public string property = "_VhsIntensity";
    public float min = 0f;
    public float max = 1f;

    [Range(0f, 1f)] public float manual = 1f;

    int propId;
    bool temPropriedade;
    Material materialEmCache;
    string propriedadeEmCache;
    float ultimo = -1f;

    void Start()
    {
        Recachear();
    }

    void OnValidate()
    {
        Recachear();
    }

    void Recachear()
    {
        materialEmCache = material;
        propriedadeEmCache = property;
        propId = Shader.PropertyToID(property);
        temPropriedade = material != null && !string.IsNullOrEmpty(property) && material.HasProperty(propId);
    }

    void Update()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        // Material/propriedade trocados em tempo de execucao.
        if (material != materialEmCache || property != propriedadeEmCache)
            Recachear();

        float v = manual;
        if (temPropriedade)
            v = Mathf.InverseLerp(min, max, material.GetFloat(propId));

        if (Mathf.Abs(v - ultimo) < 0.002f) return;

        ultimo = v;
        am.SetVhsIntensity(v);
    }
}