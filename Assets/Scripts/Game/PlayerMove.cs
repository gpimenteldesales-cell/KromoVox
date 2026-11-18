using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Movimento do cubo. Aceita, ao mesmo tempo:
///  - TECLADO: WASD / setas;
///  - CONTROLE: analogico esquerdo e direcional (PS4/DualShock, Xbox, etc.);
///  - TOQUE: o pad de botoes do MobileControls (celular / tablet).
///
/// Funciona em Windows/Mac/Linux e em WebGL.
///  - Com o Input System novo (Active Input Handling = Both ou Input System Package):
///    teclado + controle. E o unico jeito do controle funcionar no WebGL.
///  - Com so o Input Manager antigo: teclado (e o controle que o Input Manager enxergar).
///  - O toque funciona nos dois casos.
///
/// O que o jogo "pede" fica em InputAtual (x = direita, y = frente), para outros scripts
/// (como o PlayerJuice) usarem exatamente a mesma leitura.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMove : MonoBehaviour
{
    [Header("Movimento")]
    public float velocidade = 6f;

    [Header("Câmera isométrica")]
    [Tooltip("Câmera usada como referência de direção. Se vazio, usa Camera.main")]
    public Transform referenciaCamera;

    [Header("Entradas aceitas")]
    public bool usarTeclado = true;
    public bool usarControle = true;
    public bool usarToque = true;

    [Header("Sensação")]
    [Tooltip("LIGADO: o cubo para na hora ao soltar (preciso, recomendado). " +
             "DESLIGADO: acelera e freia suave (pode escorregar para o tile ao lado).")]
    public bool entradaCrua = true;

    [Tooltip("Velocidade da suavização (só vale com Entrada Crua desligada).")]
    public float suavizacao = 14f;

    [Tooltip("Zona morta do analógico do controle (0 a 1).")]
    [Range(0f, 0.9f)] public float zonaMorta = 0.2f;

    /// <summary>Direção pedida pelo jogador agora: x = direita, y = frente (tamanho 0 a 1).</summary>
    public Vector2 InputAtual { get; private set; }

    public bool Movendo => InputAtual.sqrMagnitude > 0.01f;

    private Rigidbody rb;
    private Vector3 direcaoMovimento;
    private Vector2 suave;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // Trava todas as rotações (X, Y e Z) — o player nunca gira, nem por física nem por script
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        // Move em FixedUpdate mas desenha a cada frame: sem isso o cubo "treme" em telas de 120/144 Hz.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        if (referenciaCamera == null && Camera.main != null)
            referenciaCamera = Camera.main.transform;
    }

    void Update()
    {
        // Lê o input em Update (mais responsivo), aplica o movimento em FixedUpdate
        Vector2 alvo = Vector2.zero;

        if (usarTeclado) alvo = MaiorMagnitude(alvo, LerTeclado());
        if (usarControle) alvo = MaiorMagnitude(alvo, LerControle());
        if (usarToque) alvo = MaiorMagnitude(alvo, MobileControls.Direcao);

        alvo = Vector2.ClampMagnitude(alvo, 1f);

        if (entradaCrua)
            suave = alvo;
        else
            suave = Vector2.MoveTowards(suave, alvo, suavizacao * Time.unscaledDeltaTime);

        InputAtual = suave;

        Vector3 inputBruto = new Vector3(suave.x, 0f, suave.y);
        if (inputBruto.sqrMagnitude > 1f)
            inputBruto.Normalize();

        if (referenciaCamera != null)
        {
            // Pega a direção "pra frente" e "pra direita" da câmera, achatadas no chão (ignora inclinação)
            Vector3 camForward = referenciaCamera.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = referenciaCamera.right;
            camRight.y = 0f;
            camRight.Normalize();

            // W = "pra frente" na tela, D = "pra direita" na tela, mesmo com a câmera em diagonal
            direcaoMovimento = camForward * inputBruto.z + camRight * inputBruto.x;
        }
        else
        {
            // Fallback: eixos do mundo, caso não tenha câmera de referência
            direcaoMovimento = inputBruto;
        }
    }

    void FixedUpdate()
    {
        // Move o Rigidbody de forma segura com a física (colisores e triggers continuam funcionando)
        Vector3 novaPosicao = rb.position + direcaoMovimento * velocidade * Time.fixedDeltaTime;
        rb.MovePosition(novaPosicao);
    }

    void OnDisable()
    {
        suave = Vector2.zero;
        InputAtual = Vector2.zero;
        direcaoMovimento = Vector3.zero;
    }

    // ------------------------------------------------------------------
    // LEITURA DOS DISPOSITIVOS
    // ------------------------------------------------------------------
    static Vector2 MaiorMagnitude(Vector2 a, Vector2 b)
    {
        return b.sqrMagnitude > a.sqrMagnitude ? b : a;
    }

    /// <summary>WASD / setas. Valores "crus" (-1, 0 ou 1), sem deslizar.</summary>
    static Vector2 LerTeclado()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;

        float x = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f)
                - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f);

        float y = ((kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1f : 0f)
                - ((kb.sKey.isPressed || kb.downArrowKey.isPressed) ? 1f : 0f);

        return new Vector2(x, y);
#elif ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    /// <summary>Analógico esquerdo (com zona morta) ou direcional do controle.</summary>
    Vector2 LerControle()
    {
#if ENABLE_INPUT_SYSTEM
        var gp = Gamepad.current;
        if (gp == null) return Vector2.zero;

        Vector2 stick = AplicarZonaMorta(gp.leftStick.ReadValue());
        Vector2 dpad = Vector2.ClampMagnitude(gp.dpad.ReadValue(), 1f);

        return dpad.sqrMagnitude > stick.sqrMagnitude ? dpad : stick;
#else
        return Vector2.zero;
#endif
    }

    /// <summary>Zona morta radial: ignora o tremor do analógico parado e reescala o resto para 0..1.</summary>
    Vector2 AplicarZonaMorta(Vector2 v)
    {
        float m = v.magnitude;
        if (m < zonaMorta) return Vector2.zero;

        float k = Mathf.Clamp01((m - zonaMorta) / Mathf.Max(0.01f, 1f - zonaMorta));
        return v / m * k;
    }
}