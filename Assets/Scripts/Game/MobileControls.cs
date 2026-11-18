using UnityEngine;
using UnityEngine.EventSystems;

public class MobileControls : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Configurações do Joystick Virtual")]
    [Tooltip("Raio máximo que o botão do joystick pode se mover na tela.")]
    public float raioMaximo = 100f;

    /// <label>Direção normalizada do toque (-1 a 1 para X e Y).</label>
    public static Vector2 Direcao { get; private set; } = Vector2.zero;

    private RectTransform fundoJoystick;
    private RectTransform caboJoystick;
    private Vector2 posicaoOriginal;

    void Start()
    {
        // Tenta encontrar ou criar referências visuais básicas se necessário, 
        // ou você pode associar via Inspector se preferir.
        RectTransform[] pedacos = GetComponentsInChildren<RectTransform>();
        foreach (var p in pedacos)
        {
            if (p.name == "Handle" || p.name == "Cabo") caboJoystick = p;
            if (p.name == "Background" || p.name == "Fundo") fundoJoystick = p;
        }

        if (caboJoystick != null)
            posicaoOriginal = caboJoystick.anchoredPosition;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (fundoJoystick == null)
        {
            // Fallback simples baseado na tela caso não tenha UI desenhada ainda
            Vector2 posicaoTela = eventData.position;
            Vector2 centroTela = new Vector2(Screen.width / 2f, Screen.height / 2f);
            Vector2 offset = (posicaoTela - centroTela) / raioMaximo;
            Direcao = Vector2.ClampMagnitude(offset, 1f);
            return;
        }

        // Lógica padrão de joystick na UI (Canvas)
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            fundoJoystick,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 posicaoLocal
        );

        Vector2 direcaoMovimento = Vector2.ClampMagnitude(posicaoLocal, raioMaximo);

        if (caboJoystick != null)
            caboJoystick.anchoredPosition = posicaoOriginal + direcaoMovimento;

        // Normaliza a saída de 0 a 1 para o PlayerMove consumir
        Direcao = direcaoMovimento / raioMaximo;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Direcao = Vector2.zero;

        if (caboJoystick != null)
            caboJoystick.anchoredPosition = posicaoOriginal;
    }

    // Mantém compatibilidade caso outro script queira forçar a direção manualmente
    public static void DefinirDirecao(Vector2 novaDirecao)
    {
        Direcao = novaDirecao;
    }
}