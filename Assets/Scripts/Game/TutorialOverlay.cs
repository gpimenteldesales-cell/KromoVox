using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Mini tutorial de começo do KROMOVOX, montado por código e independente do resto
/// do jogo: coloque este script em um GameObject vazio da cena do jogo (ex.: "Tutorial").
///
/// Como funciona:
/// - Aparece só na primeira vez (salva "já visto" em PlayerPrefs). ESC (ou o botão SKIP no celular) pula.
/// - Congela o jogo (Time.timeScale = 0) enquanto os 3 cartões estão na tela e devolve
///   a escala ao terminar. Depois disso o preparo ("GET READY") começa normalmente.
/// - Cartão 1: achar o bloco da cor-alvo.
///   Cartão 2: controles. NO PC, as teclas acendem ao apertar. NO CELULAR, mostra o pad de toque
///   (com uma demonstração animada que também reage ao pad de verdade).
///   Cartão 3: o tempo acaba e cada rodada fica mais rápida.
/// - Avança com ESPAÇO, ENTER ou clique/toque. Tudo usa tempo não escalado, então funciona congelado.
///
/// NOVO (celular):
/// - Detecta sozinho o dispositivo (celular/tablet ou primeiro toque) e troca textos e ilustração.
///   Se o jogador usar o teclado depois, volta para o texto de teclado (só em PC).
/// - No cartão 2 do celular, tocar nos cantos de baixo (onde fica o pad) NÃO avança o cartão,
///   para o jogador poder testar o pad. Toque no meio da tela para continuar.
/// - Botão SKIP no canto superior direito do cartão (só no toque).
/// </summary>
public class TutorialOverlay : MonoBehaviour
{
    const string ChaveConcluido = "chroma_tutorial_done";

    [Header("Quando mostrar")]
    [Tooltip("Para testes: mostra o tutorial toda vez (ignora o 'já visto').")]
    public bool forcarMostrar = false;
    [Tooltip("Espera depois da cena abrir (o fade-in do HUD dura ~0.8 s), em segundos.")]
    public float atrasoInicial = 0.9f;

    [Header("Textos (teclado / PC)")]
    public string titulo1 = "FIND THE COLOR";
    public string texto1 = "Stand on the tile that matches the TARGET color at the top of the screen.";
    public string rotuloAlvo = "TARGET";
    public string rotuloIgual = "MATCH!";
    public string titulo2 = "MOVE YOUR CUBE";
    public string texto2 = "Use WASD or the ARROW KEYS. Try them now!";
    public string rotuloOu = "OR";
    public string titulo3 = "BEAT THE CLOCK";
    public string texto3 = "When the timer ends, you must be standing on the matching tile. Every round is faster!";
    public string rodapeProximo = "SPACE / CLICK: NEXT  |  ESC: SKIP";
    public string rodapeComecar = "SPACE / CLICK: START";

    [Header("Textos (celular / toque)")]
    public string texto2Toque = "Touch and drag the PAD in the bottom corner to move. You can go diagonally too. Try it now!";
    public string rotuloArrastar = "DRAG TO MOVE";
    public string rodapeProximoToque = "TAP: NEXT";
    public string rodapePadToque = "TAP THE MIDDLE OF THE SCREEN: NEXT";
    public string rodapeComecarToque = "TAP: START";
    public string rotuloPular = "SKIP";

    [Header("Visual")]
    [Tooltip("Opcional: fonte do jogo (TMP Font Asset). Vazio = fonte padrão do TMP.")]
    public TMP_FontAsset font;
    [Tooltip("Ordem do Canvas (acima do HUD, abaixo do fade preto).")]
    public int sortingOrder = 100;
    public Color corDim = new Color(0f, 0f, 0f, 0.62f);
    public Color corCard = new Color(0.10f, 0.08f, 0.18f, 0.97f);
    public Color corBorda = new Color(0.43f, 0.25f, 1.00f);
    public Color corDestaque = new Color(0.24f, 0.86f, 0.52f);
    public Color corPerigo = new Color(0.95f, 0.25f, 0.25f);
    public Color corTecla = new Color(0.22f, 0.19f, 0.34f);
    public Color corTrilho = new Color(0.04f, 0.02f, 0.09f, 1f);
    public Color corTextoSuave = new Color(1f, 1f, 1f, 0.82f);

    // --- runtime ---
    GameObject raizCanvas;
    CanvasGroup grupoOverlay;
    CanvasGroup grupoConteudo;
    TextMeshProUGUI txtTitulo;
    TextMeshProUGUI txtCorpo;
    TextMeshProUGUI txtRodape;
    TextMeshProUGUI txtBarra;
    RectTransform barraFill;
    Image barraFillImg;
    RectTransform botaoPular;

    readonly RectTransform[] ilustracoes = new RectTransform[3];
    RectTransform ilustracaoPad;                 // versão do cartão 2 para celular
    readonly Image[] pontos = new Image[3];
    readonly Image[] teclas = new Image[8];      // W A S D ^ < v >
    readonly Image[] setasPad = new Image[4];    // cima, direita, baixo, esquerda

    static Sprite spCirculo;
    static Sprite spTriangulo;

    // Direções da demonstração do pad (celular).
    static readonly Vector2[] demoPad =
    {
        new Vector2(0f, 1f),
        new Vector2(1f, 0f),
        new Vector2(0f, -1f),
        new Vector2(-1f, 0f),
        new Vector2(1f, 1f),
        Vector2.zero
    };

    bool construido;
    bool ativo;
    bool pulou;
    bool toque;                 // true = mostra a versão de celular
    int passo = -1;
    float escalaAnterior = 1f;

    // ------------------------------------------------------------------
    // CICLO DE VIDA
    // ------------------------------------------------------------------
    void Awake()
    {
        if (!forcarMostrar && PlayerPrefs.GetInt(ChaveConcluido, 0) == 1)
        {
            enabled = false;
            return;
        }

        Mostrar();
    }

    /// <summary>Roda o tutorial. Também pode ser ligado a um botão "HOW TO PLAY".</summary>
    public void Mostrar()
    {
        if (ativo) return;

        if (!construido)
        {
            BuildUI();
            construido = true;
        }

        ativo = true;
        pulou = false;
        passo = -1;
        toque = Application.isMobilePlatform || MobileControls.ToqueDetectado;

        escalaAnterior = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;

        raizCanvas.SetActive(true);
        enabled = true;   // o Update (animações dos cartões) precisa rodar
        StartCoroutine(Rotina());
    }

    void OnDestroy()
    {
        // Segurança: nunca deixa o jogo congelado se o objeto for destruído no meio.
        if (ativo) Time.timeScale = escalaAnterior;
    }

    [ContextMenu("Resetar 'já visto' (para testes)")]
    void ResetarPreferencia()
    {
        PlayerPrefs.DeleteKey(ChaveConcluido);
        PlayerPrefs.Save();
    }

    void Update()
    {
        if (!ativo) return;

        DetectarDispositivo();

        if (passo == 1)
        {
            if (toque) AtualizarPadDemo();
            else AtualizarTeclas();
        }

        if (passo == 2) AtualizarBarra();
    }

    // ------------------------------------------------------------------
    // SEQUÊNCIA
    // ------------------------------------------------------------------
    IEnumerator Rotina()
    {
        yield return Esperar(atrasoInicial);
        yield return Fade(grupoOverlay, 0f, 1f, 0.35f);

        for (int i = 0; i < 3 && !pulou; i++)
        {
            MostrarPasso(i);
            yield return Fade(grupoConteudo, 0f, 1f, 0.25f);

            // Pequena trava para não pular o cartão sem querer.
            yield return Esperar(0.35f);

            while (!pulou && !ConfirmarPressionado())
            {
                if (SkipPressionado()) pulou = true;
                yield return null;
            }

            yield return Fade(grupoConteudo, grupoConteudo.alpha, 0f, 0.2f);
        }

        PlayerPrefs.SetInt(ChaveConcluido, 1);
        PlayerPrefs.Save();

        yield return Fade(grupoOverlay, grupoOverlay.alpha, 0f, 0.35f);

        ativo = false;
        Time.timeScale = escalaAnterior;
        raizCanvas.SetActive(false);
        enabled = false;
    }

    void MostrarPasso(int i)
    {
        passo = i;
        AplicarPasso();
    }

    /// <summary>Aplica textos e ilustração do passo atual de acordo com o dispositivo (teclado ou toque).</summary>
    void AplicarPasso()
    {
        int i = passo;
        if (i < 0) return;

        txtTitulo.text = i == 0 ? titulo1 : (i == 1 ? titulo2 : titulo3);

        if (i == 0) txtCorpo.text = texto1;
        else if (i == 1) txtCorpo.text = toque ? texto2Toque : texto2;
        else txtCorpo.text = texto3;

        if (!toque)
            txtRodape.text = i == 2 ? rodapeComecar : rodapeProximo;
        else if (i == 2)
            txtRodape.text = rodapeComecarToque;
        else
            txtRodape.text = i == 1 ? rodapePadToque : rodapeProximoToque;

        ilustracoes[0].gameObject.SetActive(i == 0);
        ilustracoes[1].gameObject.SetActive(i == 1 && !toque);
        ilustracoes[2].gameObject.SetActive(i == 2);
        ilustracaoPad.gameObject.SetActive(i == 1 && toque);

        botaoPular.gameObject.SetActive(toque);

        for (int k = 0; k < pontos.Length; k++)
            pontos[k].color = k == i ? corDestaque : corTecla;
    }

    static IEnumerator Esperar(float segundos)
    {
        float t = 0f;
        while (t < segundos)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    static IEnumerator Fade(CanvasGroup grupo, float de, float ate, float duracao)
    {
        float t = 0f;
        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            grupo.alpha = Mathf.Lerp(de, ate, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracao)));
            yield return null;
        }
        grupo.alpha = ate;
    }

    // ------------------------------------------------------------------
    // DISPOSITIVO (teclado x toque)
    // ------------------------------------------------------------------
    /// <summary>
    /// Troca entre os textos de teclado e de toque conforme o que o jogador usou por último.
    /// Em celular de verdade (isMobilePlatform) nunca volta para o teclado.
    /// </summary>
    void DetectarDispositivo()
    {
        bool antes = toque;

#if ENABLE_INPUT_SYSTEM
        var t = Touchscreen.current;
        var k = Keyboard.current;

        if (t != null && t.primaryTouch.press.wasPressedThisFrame)
            toque = true;
        else if (!Application.isMobilePlatform && k != null && k.anyKey.wasPressedThisFrame)
            toque = false;
#else
        if (Input.touchCount > 0)
            toque = true;
        else if (!Application.isMobilePlatform && Input.anyKeyDown && !Input.GetMouseButtonDown(0))
            toque = false;
#endif

        if (toque != antes)
            AplicarPasso();
    }

    // ------------------------------------------------------------------
    // ANIMAÇÕES DOS CARTÕES
    // ------------------------------------------------------------------
    void AtualizarTeclas()
    {
        for (int i = 0; i < teclas.Length; i++)
            teclas[i].color = TeclaSegura(i) ? corDestaque : corTecla;
    }

    /// <summary>
    /// Celular: as setas do pad desenhado acendem em sequência (demonstração).
    /// Se o jogador mexer no pad de verdade, elas passam a seguir o dedo dele.
    /// </summary>
    void AtualizarPadDemo()
    {
        Vector2 d = MobileControls.Direcao;

        if (d.sqrMagnitude < 0.01f)
        {
            int idx = Mathf.FloorToInt(Time.unscaledTime / 0.7f) % demoPad.Length;
            d = demoPad[idx];
        }

        setasPad[0].color = d.y > 0.35f ? corDestaque : corTecla;
        setasPad[1].color = d.x > 0.35f ? corDestaque : corTecla;
        setasPad[2].color = d.y < -0.35f ? corDestaque : corTecla;
        setasPad[3].color = d.x < -0.35f ? corDestaque : corTecla;
    }

    void AtualizarBarra()
    {
        // Mostra a barra de tempo esvaziando em loop (3 s + uma pausinha).
        const float duracao = 3f;
        float t = Mathf.Repeat(Time.unscaledTime, duracao + 0.6f);
        float p = 1f - Mathf.Clamp01(t / duracao);

        barraFill.anchorMax = new Vector2(p, 1f);
        barraFillImg.enabled = p > 0.003f;
        barraFillImg.color = Color.Lerp(corPerigo, corDestaque, Mathf.InverseLerp(0.15f, 0.45f, p));
        txtBarra.text = (p * duracao).ToString("0.0", CultureInfo.InvariantCulture) + "s";
    }

    // ------------------------------------------------------------------
    // ENTRADA (funciona com o Input Manager antigo e com o novo Input System)
    // ------------------------------------------------------------------
    /// <summary>True quando o jogador confirma (teclado, clique ou toque). O botão SKIP vira "pular".</summary>
    bool ConfirmarPressionado()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        var m = Mouse.current;
        var t = Touchscreen.current;

        if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
            return true;

        if (t != null && t.primaryTouch.press.wasPressedThisFrame)
            return ToqueConfirma(t.primaryTouch.position.ReadValue());

        if (m != null && m.leftButton.wasPressedThisFrame)
            return ToqueConfirma(m.position.ReadValue());

        return false;
#else
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            return true;

        if (Input.GetMouseButtonDown(0))
            return ToqueConfirma(Input.mousePosition);

        return false;
#endif
    }

    /// <summary>
    /// Decide se um toque/clique avança o cartão.
    /// - Toque no botão SKIP: pula o tutorial (não avança).
    /// - Cartão 2 no celular: toques nos cantos de baixo (área do pad) são ignorados,
    ///   assim o jogador consegue testar o pad sem passar o cartão sem querer.
    /// </summary>
    bool ToqueConfirma(Vector2 pos)
    {
        if (toque && botaoPular != null &&
            RectTransformUtility.RectangleContainsScreenPoint(botaoPular, pos, null))
        {
            pulou = true;
            return false;
        }

        if (toque && passo == 1 && EmZonaDoPad(pos))
            return false;

        return true;
    }

    static bool EmZonaDoPad(Vector2 pos)
    {
        bool embaixo = pos.y < Screen.height * 0.5f;
        bool lado = pos.x < Screen.width * 0.35f || pos.x > Screen.width * 0.65f;
        return embaixo && lado;
    }

    static bool SkipPressionado()
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        return k != null && k.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    /// <summary>0 W, 1 A, 2 S, 3 D, 4 seta cima, 5 esquerda, 6 baixo, 7 direita.</summary>
    static bool TeclaSegura(int indice)
    {
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k == null) return false;

        switch (indice)
        {
            case 0: return k.wKey.isPressed;
            case 1: return k.aKey.isPressed;
            case 2: return k.sKey.isPressed;
            case 3: return k.dKey.isPressed;
            case 4: return k.upArrowKey.isPressed;
            case 5: return k.leftArrowKey.isPressed;
            case 6: return k.downArrowKey.isPressed;
            default: return k.rightArrowKey.isPressed;
        }
#else
        switch (indice)
        {
            case 0: return Input.GetKey(KeyCode.W);
            case 1: return Input.GetKey(KeyCode.A);
            case 2: return Input.GetKey(KeyCode.S);
            case 3: return Input.GetKey(KeyCode.D);
            case 4: return Input.GetKey(KeyCode.UpArrow);
            case 5: return Input.GetKey(KeyCode.LeftArrow);
            case 6: return Input.GetKey(KeyCode.DownArrow);
            default: return Input.GetKey(KeyCode.RightArrow);
        }
#endif
    }

    // ------------------------------------------------------------------
    // CONSTRUÇÃO DA UI (tudo por código)
    // ------------------------------------------------------------------
    void BuildUI()
    {
        raizCanvas = new GameObject("TutorialCanvas", typeof(RectTransform));
        raizCanvas.transform.SetParent(transform, false);

        var canvas = raizCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = raizCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        grupoOverlay = raizCanvas.AddComponent<CanvasGroup>();
        grupoOverlay.alpha = 0f;
        grupoOverlay.interactable = false;
        grupoOverlay.blocksRaycasts = false;

        var root = (RectTransform)raizCanvas.transform;
        Vector2 centro = new Vector2(0.5f, 0.5f);

        // Fundo escurecido: foca a atenção no cartão.
        Image dim = MakeImage(root, "Dim", corDim);
        Inset(dim.rectTransform, 0f);

        // Cartão (borda + miolo).
        Image bordaCard = MakeImage(root, "Card", corBorda);
        PlaceFixo(bordaCard.rectTransform, centro, Vector2.zero, new Vector2(1180f, 640f));

        Image fundoCard = MakeImage(bordaCard.rectTransform, "Fundo", corCard);
        Inset(fundoCard.rectTransform, 4f);
        RectTransform fundoRt = fundoCard.rectTransform;

        // Conteúdo que troca a cada passo (com fade entre passos).
        var conteudoGo = new GameObject("Conteudo", typeof(RectTransform));
        var conteudo = (RectTransform)conteudoGo.transform;
        conteudo.SetParent(fundoRt, false);
        Inset(conteudo, 0f);

        grupoConteudo = conteudoGo.AddComponent<CanvasGroup>();
        grupoConteudo.alpha = 0f;
        grupoConteudo.interactable = false;
        grupoConteudo.blocksRaycasts = false;

        txtTitulo = MakeText(conteudo, "Titulo", titulo1, 68f, Color.white, TextAlignmentOptions.Center);
        Place(txtTitulo.rectTransform,
            new Vector2(0f, 0.80f), new Vector2(1f, 0.97f),
            new Vector2(40f, 0f), new Vector2(-40f, 0f));

        txtCorpo = MakeText(conteudo, "Corpo", texto1, 38f, corTextoSuave, TextAlignmentOptions.Center);
        Place(txtCorpo.rectTransform,
            new Vector2(0f, 0.22f), new Vector2(1f, 0.40f),
            new Vector2(70f, 0f), new Vector2(-70f, 0f));

        txtRodape = MakeText(conteudo, "Rodape", rodapeProximo, 28f, corDestaque, TextAlignmentOptions.Center);
        Place(txtRodape.rectTransform,
            new Vector2(0f, 0.03f), new Vector2(1f, 0.12f),
            new Vector2(20f, 0f), new Vector2(-20f, 0f));

        // Ilustrações (uma por passo; só a do passo atual fica ativa).
        ilustracoes[0] = CriarIlustracaoObjetivo(conteudo);
        ilustracoes[1] = CriarIlustracaoControles(conteudo);
        ilustracoes[2] = CriarIlustracaoTempo(conteudo);
        ilustracaoPad = CriarIlustracaoPad(conteudo);

        // Indicador de passos (fora do conteúdo, para não piscar na troca).
        for (int i = 0; i < pontos.Length; i++)
        {
            Image ponto = MakeImage(fundoRt, "Ponto" + i, corTecla);
            PlaceFixo(ponto.rectTransform, new Vector2(0.5f, 0.16f),
                new Vector2((i - 1) * 34f, 0f), new Vector2(16f, 16f));
            pontos[i] = ponto;
        }

        // Botão SKIP (só no toque). Fica fora do "Conteudo" para não sumir no fade entre cartões.
        Image skip = MakeImage(fundoRt, "BotaoPular", corTecla);
        PlaceFixo(skip.rectTransform, new Vector2(1f, 1f), new Vector2(-95f, -45f), new Vector2(160f, 56f));
        TextMeshProUGUI skipTxt = MakeText(skip.rectTransform, "Texto", rotuloPular, 30f, corTextoSuave, TextAlignmentOptions.Center);
        Inset(skipTxt.rectTransform, 4f);
        botaoPular = skip.rectTransform;
        botaoPular.gameObject.SetActive(false);

        raizCanvas.SetActive(false);
    }

    RectTransform NovoContainer(RectTransform parent, string nome)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        PlaceFixo(rt, new Vector2(0.5f, 0.58f), Vector2.zero, new Vector2(900f, 210f));
        return rt;
    }

    /// <summary>Cartão 1: a cor-alvo à esquerda e quatro blocos; só um combina.</summary>
    RectTransform CriarIlustracaoObjetivo(RectTransform parent)
    {
        RectTransform raiz = NovoContainer(parent, "IlustObjetivo");
        Vector2 c = new Vector2(0.5f, 0.5f);

        // Cores de exemplo fixas e vivas (não dependem do jogo).
        Color alvo = new Color(1.00f, 0.35f, 0.65f);
        Color[] tiles =
        {
            new Color(0.25f, 0.55f, 1.00f),
            new Color(0.30f, 0.90f, 0.45f),
            alvo,
            new Color(1.00f, 0.80f, 0.25f)
        };

        TextMeshProUGUI rotulo = MakeText(raiz, "RotuloAlvo", rotuloAlvo, 28f, corTextoSuave, TextAlignmentOptions.Center);
        PlaceFixo(rotulo.rectTransform, c, new Vector2(-330f, 82f), new Vector2(240f, 40f));

        Image moldura = MakeImage(raiz, "MolduraAlvo", new Color(0.92f, 0.92f, 0.96f));
        PlaceFixo(moldura.rectTransform, c, new Vector2(-330f, 0f), new Vector2(210f, 130f));
        Image chip = MakeImage(moldura.rectTransform, "Cor", alvo);
        Inset(chip.rectTransform, 5f);

        TextMeshProUGUI igual = MakeText(raiz, "Igual", "=", 80f, Color.white, TextAlignmentOptions.Center);
        PlaceFixo(igual.rectTransform, c, new Vector2(-175f, 0f), new Vector2(80f, 90f));

        for (int i = 0; i < tiles.Length; i++)
        {
            float x = -35f + i * 130f;

            if (i == 2)
            {
                Image halo = MakeImage(raiz, "Halo", corDestaque);
                PlaceFixo(halo.rectTransform, c, new Vector2(x, 8f), new Vector2(128f, 128f));
            }

            Image tile = MakeImage(raiz, "Tile" + i, tiles[i]);
            PlaceFixo(tile.rectTransform, c, new Vector2(x, 8f), new Vector2(108f, 108f));
        }

        TextMeshProUGUI ok = MakeText(raiz, "Match", rotuloIgual, 32f, corDestaque, TextAlignmentOptions.Center);
        PlaceFixo(ok.rectTransform, c, new Vector2(-35f + 2 * 130f, -82f), new Vector2(200f, 40f));

        return raiz;
    }

    /// <summary>Cartão 2 (PC): WASD e setas; as teclas acendem quando o jogador aperta.</summary>
    RectTransform CriarIlustracaoControles(RectTransform parent)
    {
        RectTransform raiz = NovoContainer(parent, "IlustControles");
        Vector2 c = new Vector2(0.5f, 0.5f);

        string[] letras = { "W", "A", "S", "D", "^", "<", "v", ">" };
        Vector2[] rel =
        {
            new Vector2(0f, 52f),
            new Vector2(-100f, -52f),
            new Vector2(0f, -52f),
            new Vector2(100f, -52f)
        };

        for (int grupo = 0; grupo < 2; grupo++)
        {
            float cx = grupo == 0 ? -300f : 300f;

            for (int k = 0; k < 4; k++)
            {
                int idx = grupo * 4 + k;

                Image tecla = MakeImage(raiz, "Tecla" + letras[idx], corTecla);
                PlaceFixo(tecla.rectTransform, c, new Vector2(cx + rel[k].x, rel[k].y), new Vector2(88f, 88f));

                TextMeshProUGUI letra = MakeText(tecla.rectTransform, "Letra", letras[idx], 48f, Color.white, TextAlignmentOptions.Center);
                Inset(letra.rectTransform, 0f);

                teclas[idx] = tecla;
            }
        }

        TextMeshProUGUI ou = MakeText(raiz, "Ou", rotuloOu, 44f, corTextoSuave, TextAlignmentOptions.Center);
        PlaceFixo(ou.rectTransform, c, Vector2.zero, new Vector2(140f, 70f));

        return raiz;
    }

    /// <summary>
    /// Cartão 2 (celular): um pad redondo com 4 setas, igual ao do jogo, que acende em sequência.
    /// Fica à esquerda, com a legenda "DRAG TO MOVE" ao lado.
    /// </summary>
    RectTransform CriarIlustracaoPad(RectTransform parent)
    {
        RectTransform raiz = NovoContainer(parent, "IlustPad");
        Vector2 c = new Vector2(0.5f, 0.5f);

        const float tam = 200f;

        Image borda = MakeImage(raiz, "Borda", corBorda);
        borda.sprite = SpriteCirculo();
        PlaceFixo(borda.rectTransform, c, new Vector2(-170f, 0f), new Vector2(tam + 10f, tam + 10f));

        Image fundo = MakeImage(borda.rectTransform, "Fundo", corTrilho);
        fundo.sprite = SpriteCirculo();
        Inset(fundo.rectTransform, 5f);

        Vector2[] pos =
        {
            new Vector2(0f, 62f),     // cima
            new Vector2(62f, 0f),     // direita
            new Vector2(0f, -62f),    // baixo
            new Vector2(-62f, 0f)     // esquerda
        };
        float[] rot = { 0f, -90f, 180f, 90f };

        for (int i = 0; i < 4; i++)
        {
            Image seta = MakeImage(fundo.rectTransform, "Seta" + i, corTecla);
            seta.sprite = SpriteTriangulo();
            PlaceFixo(seta.rectTransform, c, pos[i], new Vector2(52f, 52f));
            seta.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot[i]);
            setasPad[i] = seta;
        }

        TextMeshProUGUI legenda = MakeText(raiz, "Legenda", rotuloArrastar, 44f, Color.white, TextAlignmentOptions.Center);
        PlaceFixo(legenda.rectTransform, c, new Vector2(150f, 0f), new Vector2(520f, 90f));

        return raiz;
    }

    /// <summary>Cartão 3: uma barra de tempo esvaziando em loop.</summary>
    RectTransform CriarIlustracaoTempo(RectTransform parent)
    {
        RectTransform raiz = NovoContainer(parent, "IlustTempo");
        Vector2 c = new Vector2(0.5f, 0.5f);

        Image moldura = MakeImage(raiz, "Moldura", corBorda);
        PlaceFixo(moldura.rectTransform, c, new Vector2(0f, 10f), new Vector2(760f, 70f));

        Image trilho = MakeImage(moldura.rectTransform, "Trilho", corTrilho);
        Inset(trilho.rectTransform, 4f);

        Image fill = MakeImage(trilho.rectTransform, "Fill", corDestaque);
        Place(fill.rectTransform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        barraFill = fill.rectTransform;
        barraFillImg = fill;

        txtBarra = MakeText(raiz, "Segundos", "3.0s", 44f, Color.white, TextAlignmentOptions.Center);
        PlaceFixo(txtBarra.rectTransform, c, new Vector2(0f, -62f), new Vector2(300f, 60f));

        return raiz;
    }

    // ------------------------------------------------------------------
    // SPRITES GERADOS POR CÓDIGO (mesmo visual do pad do MobileControls)
    // ------------------------------------------------------------------
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

    /// <summary>Triângulo apontando para cima (as outras setas são este, girado).</summary>
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

    // ------------------------------------------------------------------
    // HELPERS DE UI
    // ------------------------------------------------------------------
    Image MakeImage(RectTransform parent, string nome, Color cor)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    TextMeshProUGUI MakeText(RectTransform parent, string nome, string texto,
        float tamanho, Color cor, TextAlignmentOptions alinhamento)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);

        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = texto;
        t.color = cor;
        t.alignment = alinhamento;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;

        // Auto-ajuste: o texto sempre cabe na caixa, mesmo com fontes largas.
        t.enableAutoSizing = true;
        t.fontSizeMax = tamanho;
        t.fontSizeMin = tamanho * 0.4f;
        t.fontSize = tamanho;

        if (font) t.font = font;
        else t.fontStyle = FontStyles.Bold;

        return t;
    }

    static void PlaceFixo(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    static void Inset(RectTransform rt, float px)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(px, px);
        rt.offsetMax = new Vector2(-px, -px);
    }
}