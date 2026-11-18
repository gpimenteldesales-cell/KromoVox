using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Botoes de toque para celular/tablet (e WebGL no navegador do celular).
/// Monta tudo por codigo: um pad com 4 setas (cima, baixo, esquerda, direita) no canto da tela.
///
/// Como funciona:
///  - Encoste o dedo no pad e arraste: ele le a direcao em 8 sentidos (inclusive diagonais)
///    com um dedo so, e acende as setas apertadas.
///  - A direcao fica em MobileControls.Direcao (x = direita, y = frente), que o PlayerMove le.
///  - Aparece sozinho em celular/tablet, ou no primeiro toque (modo Auto).
///  - Some no Game Over e fica sem pegar clique quando esta escondido.
///
/// COMO USAR: coloque este script em qualquer objeto da cena do jogo (ex.: ControlsScripts).
/// Precisa de um EventSystem na cena (o seu ja tem).
/// Para testar no Editor, ponha Modo = SempreMostrar (o mouse faz o papel do dedo).
/// </summary>
public class MobileControls : MonoBehaviour
{
    public enum Modo { Auto, SempreMostrar, Nunca }

    /// <summary>Direcao pedida pelo toque: x = direita, y = frente. (0,0) = solto.</summary>
    public static Vector2 Direcao { get; private set; }

    /// <summary>True depois que algum toque foi detectado nesta sessao.</summary>
    public static bool ToqueDetectado { get; private set; }

    [Header("Quando mostrar")]
    [Tooltip("Auto = celular/tablet, ou ao primeiro toque. SempreMostrar serve para testar no PC.")]
    public Modo modo = Modo.Auto;
    [Tooltip("Esconde o pad quando o jogo termina (tela de Game Over).")]
    public bool esconderNoGameOver = true;

    [Header("Layout (base 1920x1080)")]
    public bool naDireita = false;
    public float margem = 70f;
    public float tamanhoPad = 420f;
    public int sortingOrder = 20;

    [Header("Leitura do dedo")]
    [Range(0.05f, 0.5f)] public float zonaMorta = 0.18f;
    [Tooltip("LIGADO: 8 direcoes certinhas (recomendado). DESLIGADO: direcao livre.")]
    public bool oitoDirecoes = true;

    [Header("Cores")]
    public Color corBorda = new Color(0.43f, 0.25f, 1.00f, 0.90f);
    public Color corFundo = new Color(0.09f, 0.06f, 0.18f, 0.55f);
    public Color corSeta = new Color(1f, 1f, 1f, 0.40f);
    public Color corAtiva = new Color(0.24f, 0.86f, 0.52f, 1f);

    // --- runtime ---
    Canvas canvas;
    CanvasGroup grupo;
    RectTransform padRt;
    readonly Image[] setas = new Image[4];   // 0 cima, 1 direita, 2 baixo, 3 esquerda
    bool visivel;
    bool padAtivo;
    int padPointer = int.MinValue;

    static Sprite spCirculo;
    static Sprite spTriangulo;

    // ------------------------------------------------------------------
    void Awake()
    {
        Direcao = Vector2.zero;
        Build();
    }

    void Start()
    {
        if (EventSystem.current == null)
            Debug.LogWarning("MobileControls: nao ha EventSystem na cena, os botoes nao vao receber toque.", this);

        visivel = DecidirVisibilidadeInicial();
        grupo.alpha = visivel ? 1f : 0f;
        AtualizarEstadoDoCanvas();
    }

    void OnDisable()
    {
        SoltarPad();
    }

    void OnDestroy()
    {
        Direcao = Vector2.zero;
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus) SoltarPad();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;

        // Modo Auto: aparece no primeiro toque (celular em que o navegador nao se identifica como mobile).
        if (modo == Modo.Auto && !visivel && ToqueNesteFrame())
        {
            ToqueDetectado = true;
            visivel = true;
        }

        if (modo == Modo.SempreMostrar) visivel = true;
        if (modo == Modo.Nunca) visivel = false;

        bool fimDeJogo = esconderNoGameOver
                         && GameManager.Instance != null
                         && GameManager.Instance.FimDeJogo;

        bool mostrarAgora = visivel && !fimDeJogo;

        if (!mostrarAgora && padAtivo) SoltarPad();

        grupo.alpha = Mathf.MoveTowards(grupo.alpha, mostrarAgora ? 1f : 0f, dt * 4f);
        grupo.blocksRaycasts = mostrarAgora;
        AtualizarEstadoDoCanvas();

        AtualizarSetas(dt);
    }

    // ------------------------------------------------------------------
    // VISIBILIDADE
    // ------------------------------------------------------------------
    bool DecidirVisibilidadeInicial()
    {
        switch (modo)
        {
            case Modo.SempreMostrar: return true;
            case Modo.Nunca: return false;
            default: return Application.isMobilePlatform || ToqueDetectado;
        }
    }

    static bool ToqueNesteFrame()
    {
#if ENABLE_INPUT_SYSTEM
        var ts = Touchscreen.current;
        if (ts != null && ts.primaryTouch.press.wasPressedThisFrame) return true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.touchCount > 0) return true;
#endif
        return false;
    }

    void AtualizarEstadoDoCanvas()
    {
        canvas.enabled = grupo.alpha > 0.01f;
    }

    // ------------------------------------------------------------------
    // TOQUE
    // ------------------------------------------------------------------
    void AoTocar(PointerEventData d)
    {
        if (padAtivo) return;           // um dedo so controla o pad

        padAtivo = true;
        padPointer = d.pointerId;
        Calcular(d);
    }

    void AoArrastar(PointerEventData d)
    {
        if (!padAtivo || d.pointerId != padPointer) return;
        Calcular(d);
    }

    void AoSoltar(PointerEventData d)
    {
        if (!padAtivo || d.pointerId != padPointer) return;
        SoltarPad();
    }

    void SoltarPad()
    {
        padAtivo = false;
        padPointer = int.MinValue;
        Direcao = Vector2.zero;
    }

    void Calcular(PointerEventData d)
    {
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(padRt, d.position, d.pressEventCamera, out local))
            return;

        float meio = Mathf.Max(1f, padRt.rect.width * 0.5f);
        // O pad tem pivot no canto da tela: mede a partir do CENTRO do retangulo.
        Vector2 v = (local - padRt.rect.center) / meio;
        float m = v.magnitude;

        if (m < zonaMorta)
        {
            Direcao = Vector2.zero;
            return;
        }

        Vector2 dir = v / m;

        if (oitoDirecoes)
        {
            float passo = Mathf.PI * 0.25f;
            float ang = Mathf.Round(Mathf.Atan2(dir.y, dir.x) / passo) * passo;
            Direcao = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
        }
        else
        {
            float forca = Mathf.Clamp01((m - zonaMorta) / Mathf.Max(0.01f, 1f - zonaMorta));
            Direcao = dir * forca;
        }
    }

    // ------------------------------------------------------------------
    // VISUAL
    // ------------------------------------------------------------------
    void AtualizarSetas(float dt)
    {
        Vector2 d = Direcao;
        bool[] ativa =
        {
            d.y > 0.35f,    // cima
            d.x > 0.35f,    // direita
            d.y < -0.35f,   // baixo
            d.x < -0.35f    // esquerda
        };

        float k = 1f - Mathf.Exp(-30f * dt);

        for (int i = 0; i < setas.Length; i++)
        {
            Image img = setas[i];
            img.color = Color.Lerp(img.color, ativa[i] ? corAtiva : corSeta, k);

            float alvo = ativa[i] ? 1.18f : 1f;
            float s = Mathf.Lerp(img.rectTransform.localScale.x, alvo, k);
            img.rectTransform.localScale = new Vector3(s, s, 1f);
        }
    }

    void Build()
    {
        // Canvas proprio (nao depende do Canvas do menu/HUD).
        var cgo = new GameObject("MobileControlsCanvas", typeof(RectTransform));
        cgo.transform.SetParent(transform, false);

        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        cgo.AddComponent<GraphicRaycaster>();
        grupo = cgo.AddComponent<CanvasGroup>();

        var root = (RectTransform)cgo.transform;

        // Pad (ancorado no canto inferior).
        float ax = naDireita ? 1f : 0f;
        padRt = NovoRect("Pad", root);
        padRt.anchorMin = padRt.anchorMax = new Vector2(ax, 0f);
        padRt.pivot = new Vector2(ax, 0f);
        padRt.sizeDelta = new Vector2(tamanhoPad, tamanhoPad);
        padRt.anchoredPosition = new Vector2(naDireita ? -margem : margem, margem);

        // Borda + fundo (circulos).
        var borda = NovoImagem("Borda", padRt, SpriteCirculo(), corBorda, tamanhoPad + 10f);
        borda.raycastTarget = false;
        borda.rectTransform.anchoredPosition = new Vector2(0f, 0f);

        var fundo = NovoImagem("Fundo", padRt, SpriteCirculo(), corFundo, tamanhoPad);
        fundo.raycastTarget = true;     // area que recebe o toque

        LigarEventos(fundo.gameObject);

        // Setas.
        float tamSeta = tamanhoPad * 0.24f;
        float dist = tamanhoPad * 0.30f;

        Vector2[] pos =
        {
            new Vector2(0f, dist),      // cima
            new Vector2(dist, 0f),      // direita
            new Vector2(0f, -dist),     // baixo
            new Vector2(-dist, 0f)      // esquerda
        };
        float[] rot = { 0f, -90f, 180f, 90f };

        for (int i = 0; i < 4; i++)
        {
            var seta = NovoImagem("Seta" + i, padRt, SpriteTriangulo(), corSeta, tamSeta);
            seta.raycastTarget = false;
            seta.rectTransform.anchoredPosition = pos[i];
            seta.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot[i]);
            setas[i] = seta;
        }
    }

    void LigarEventos(GameObject go)
    {
        var et = go.AddComponent<EventTrigger>();
        Adicionar(et, EventTriggerType.PointerDown, e => AoTocar((PointerEventData)e));
        Adicionar(et, EventTriggerType.Drag, e => AoArrastar((PointerEventData)e));
        Adicionar(et, EventTriggerType.PointerUp, e => AoSoltar((PointerEventData)e));
    }

    static void Adicionar(EventTrigger et, EventTriggerType tipo, UnityEngine.Events.UnityAction<BaseEventData> acao)
    {
        var entrada = new EventTrigger.Entry { eventID = tipo };
        entrada.callback.AddListener(acao);
        et.triggers.Add(entrada);
    }

    // ------------------------------------------------------------------
    // HELPERS DE UI / SPRITES
    // ------------------------------------------------------------------
    static RectTransform NovoRect(string nome, RectTransform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    /// <summary>Cria uma imagem centralizada no pai (o pad tem pivot no canto, entao centralizamos pelo meio).</summary>
    static Image NovoImagem(string nome, RectTransform pai, Sprite sprite, Color cor, float tamanho)
    {
        RectTransform rt = NovoRect(nome, pai);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(tamanho, tamanho);

        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = cor;
        return img;
    }

    static Sprite SpriteCirculo()
    {
        if (spCirculo != null) return spCirculo;

        const int s = 128;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[s * s];
        float r = s * 0.5f;
        var centro = new Vector2(r, r);

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centro);
                float a = Mathf.Clamp01(r - d + 0.5f);
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        spCirculo = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return spCirculo;
    }

    /// <summary>Triangulo apontando para cima (as outras setas sao esta, girada).</summary>
    static Sprite SpriteTriangulo()
    {
        if (spTriangulo != null) return spTriangulo;

        const int s = 64;
        var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[s * s];
        const float yBase = 12f;
        const float yTopo = 52f;
        const float meiaBase = 22f;

        for (int y = 0; y < s; y++)
        {
            for (int x = 0; x < s; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;

                float t = Mathf.Clamp01((fy - yBase) / (yTopo - yBase));
                float meia = Mathf.Lerp(meiaBase, 0f, t);
                float dx = Mathf.Abs(fx - 32f);

                float a = Mathf.Clamp01(meia - dx + 0.5f)
                          * Mathf.Clamp01(fy - yBase + 0.5f)
                          * Mathf.Clamp01(yTopo + 0.5f - fy);

                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        spTriangulo = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        return spTriangulo;
    }
}
