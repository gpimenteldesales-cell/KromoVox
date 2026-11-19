using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Coloque em cada botao do menu. Toca hover (mouse e navegacao por teclado/controle) e clique
/// (ou "voltar"). Opcionalmente ja muda o estado da musica ao clicar.
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class ButtonSfx : MonoBehaviour, IPointerEnterHandler, ISelectHandler
{
    public enum ClickKind { Click, Back, None }

    public ClickKind clickSound = ClickKind.Click;

    [Header("Música (opcional)")]
    public bool changeMusicState = false;
    public AudioManager.MusicState musicState = AudioManager.MusicState.Menu;
    public float fade = 0.8f;

    Button _button;
    float _lastHoverTime = -1f;

    void Awake()
    {
        _button = GetComponent<Button>();
    }

    void OnEnable()
    {
        _button.onClick.AddListener(OnClick);
    }

    void OnDisable()
    {
        _button.onClick.RemoveListener(OnClick);
    }

    public void OnPointerEnter(PointerEventData eventData) => Hover();
    public void OnSelect(BaseEventData eventData) => Hover();

    void Hover()
    {
        var am = AudioManager.Instance;
        if (am == null || !_button.interactable) return;

        // evita hover duplo (mouse + selecao no mesmo instante)
        float agora = Time.unscaledTime;
        if (agora - _lastHoverTime < 0.06f) return;

        _lastHoverTime = agora;
        am.PlayHover();
    }

    void OnClick()
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        switch (clickSound)
        {
            case ClickKind.Click: am.PlayClick(); break;
            case ClickKind.Back: am.PlayBack(); break;
        }

        if (changeMusicState) am.SetState(musicState, fade);
    }
}