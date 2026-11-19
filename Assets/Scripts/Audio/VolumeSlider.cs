using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Liga um Slider ao volume geral do AudioManager.
///
/// OTIMIZACOES/CORRECOES: o listener e removido ao desativar (nada de lambda acumulando), o slider
/// e sincronizado toda vez que o painel de Opcoes abre e o valor inicial nao dispara o evento.
/// </summary>
[RequireComponent(typeof(Slider))]
public class VolumeSlider : MonoBehaviour
{
    Slider slider;

    void Awake()
    {
        slider = GetComponent<Slider>();
    }

    void OnEnable()
    {
        Sincronizar();
        slider.onValueChanged.AddListener(AoMudar);
    }

    void OnDisable()
    {
        slider.onValueChanged.RemoveListener(AoMudar);
    }

    void Sincronizar()
    {
        var am = AudioManager.Instance;
        if (am != null) slider.SetValueWithoutNotify(am.MasterVolume);
    }

    void AoMudar(float valor)
    {
        var am = AudioManager.Instance;
        if (am != null) am.SetMasterVolume(valor);
    }
}