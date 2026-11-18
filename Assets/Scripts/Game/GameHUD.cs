using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD do Chroma Blitz, montado por código (sem prefab nem imagens): basta colocar
/// este script em um GameObject vazio da cena do jogo (ex.: "HUD").
///
/// Layout (hierarquia):
/// - CENTRO, topo: painel principal com a COR-ALVO grande (protagonista) e, logo abaixo,
///   a barra de tempo com os segundos. No preparo vira "GET READY" + contagem.
/// - ESQUERDA, topo: painel pequeno com o número da rodada.
/// - DIREITA, topo: livre (reservado para pontuação).
/// - EMBAIXO: dica curta (regra + controles) ou, na derrota, a explicação do bloco certo.
///
/// Visual: painéis arredondados com sombra e brilho (sprites gerados por código),
/// textos com contorno. Funciona sem fonte extra; para o visual final, arraste uma
/// fonte grossa e arredondada (TMP Font Asset) no campo "Font".
/// </summary>
public class GameHUD : MonoBehaviour
{
    [Header("Textos (troque para português se quiser)")]
    public string textoRodada = "ROUND";
    public string textoPreparo = "GET READY";
    public string textoAlvo = "TARGET";
    public string textoRegra = "STAND ON THE TILE THAT MATCHES THE TARGET!";
    [Tooltip("Texto dos controles no teclado.")]
    public string textoControles = "MOVE: WASD / ARROWS";
    [Tooltip("Texto dos controles com controle (gamepad).")]
    public string textoControlesGamepad = "MOVE: LEFT STICK / D-PAD";
    [Tooltip("Texto dos controles no toque (celular).")]
    public string textoControlesToque = "MOVE: TOUCH PAD";
    public string textoDerrota = "TIME'S UP!";
    public string textoDerrotaExplicacao = "THE TILE THAT KEPT ITS COLOR WAS THE ANSWER";
    public string textoPontos = "SCORE";
    public string textoRecorde = "BEST";
    public string textoNovoRecorde = "NEW BEST!";
    public string textoCombo = "COMBO";

    [Header("Dica de início")]
    [Tooltip("A dica fica visível durante o preparo e nas primeiras N rodadas.")]
    public int rodadasComDica = 3;
    [Tooltip("Distância da dica até a borda de baixo da tela (pixels, base 1920x1080).")]
    public float margemDica = 30f;

    [Header("Entrada suave")]
    [Tooltip("Tempo do fade-in da cena (sai do preto) e do HUD, em segundos.")]
    public float duracaoFadeIn = 0.8f;

    [Header("Visual")]
    [Tooltip("Recomendado: fonte grossa e arredondada (TMP Font Asset). Vazio = fonte padrão do TMP em negrito.")]
    public TMP_FontAsset font;
    [Tooltip("Ordem do Canvas. Aumente se o HUD ficar atrás de outro painel.")]
    public int sortingOrder = 5;
    [Tooltip("Espessura do contorno dos textos (0 = sem contorno).")]
    [Range(0f, 0.5f)] public float larguraContorno = 0.10f;
    [Tooltip("O brilho em volta da cor-alvo pulsa de leve na batida da música.")]
    public bool pulsarNaBatida = true;

    [Header("Paleta")]
    public Color corBorda = new Color(0.43f, 0.25f, 1.00f);             // roxo do menu
    public Color corPainel = new Color(0.12f, 0.08f, 0.22f, 0.94f);     // miolo escuro
    public Color corTrilho = new Color(0.04f, 0.02f, 0.09f, 1f);
    public Color corContorno = new Color(0.05f, 0.02f, 0.12f, 1f);
    public Color corVerde = new Color(0.24f, 0.86f, 0.52f);             // highlight do menu
    public Color corPreparo = new Color(0.62f, 0.42f, 1.00f);           // barra do "get ready"
    public Color corPerigo = new Color(0.95f, 0.25f, 0.25f);            // vermelho do cubo/derrota
    public Color corTextoSuave = new Color(1f, 1f, 1f, 0.75f);
    public Color corDestaque = new Color(1.00f, 0.82f, 0.25f);          // dourado: combo, "+pontos", novo recorde

    [Header("Painel da cor-alvo")]
    [Tooltip("Cor do quadro enquanto ainda não existe cor-alvo (preparo).")]
    public Color corAlvoVazia = new Color(0.16f, 0.13f, 0.24f);
    [Tooltip("Moldura clara em volta da cor-alvo, para ela se destacar do painel escuro.")]
    public Color corMolduraAlvo = new Color(0.96f, 0.96f, 1.00f);

    // --- runtime ---
    TextMeshProUGUI txtRodada;
    TextMeshProUGUI lblAlvo;
    TextMeshProUGUI txtTempo;
    TextMeshProUGUI txtInterrogacao;
    TextMeshProUGUI txtDica;
    TextMeshProUGUI txtControles;
    TextMeshProUGUI txtPontos;
    TextMeshProUGUI txtRecorde;
    TextMeshProUGUI txtCombo;
    TextMeshProUGUI txtGanho;
    CanvasGroup grupoCombo;
    RectTransform barraFill;
    Image barraFillImg;
    Image quadroAlvo;
    Image glowAlvo;
    RectTransform molduraAlvoRt;
    CanvasGroup grupoRaiz;
    CanvasGroup grupoDica;
    Image fadePreto;

    enum Dispositivo { Teclado, Gamepad, Toque }
    Dispositivo dispositivo = Dispositivo.Teclado;

    int ultimoUid = -1;
    float alphaDica;
    float tFadeIn;
    Color corAlvoAtual;
    Coroutine popRodadaCo;
    Coroutine popAlvoCo;
    Coroutine popPontosCo;
    Coroutine ganhoCo;
    float pontExibida;
    int ultimoGanhoUid = -1;

    static readonly Dictionary<int, Sprite> cacheSprites = new Dictionary<int, Sprite>();

    void Awake()
    {
        BuildUI();
        corAlvoAtual = corAlvoVazia;

        if (Application.isMobilePlatform)
            dispositivo = Dispositivo.Toque;
    }

    void Update()
    {
        AtualizarDispositivo();
        AtualizarFadeIn();

        var gm = GameManager.Instance;
        if (gm == null) return;

        // Rodada/preparo novo (detecta pelo Uid, que nunca reseta: funciona também após TryAgain).
        if (gm.RodadaUid != ultimoUid)
        {
            ultimoUid = gm.RodadaUid;
            SetText(txtRodada, gm.TemCorAlvo ? gm.Rodada.ToString("00") : "--");

            if (popRodadaCo != null) StopCoroutine(popRodadaCo);
            popRodadaCo = StartCoroutine(Pop(txtRodada.rectTransform, 0.12f));

            if (gm.TemCorAlvo)
            {
                if (popAlvoCo != null) StopCoroutine(popAlvoCo);
                popAlvoCo = StartCoroutine(Pop(molduraAlvoRt, 0.06f));
            }
        }

        AtualizarPainelTempo(gm);
        AtualizarPainelAlvo(gm);
        AtualizarMensagem(gm);
        AtualizarPontuacao(gm);
    }

    // ------------------------------------------------------------------
    // ATUALIZAÇÕES
    // ------------------------------------------------------------------
    /// <summary>
    /// Descobre qual foi o último dispositivo usado (teclado, controle ou toque),
    /// para a dica de controles mostrar o texto certo.
    /// </summary>
    void AtualizarDispositivo()
    {
#if ENABLE_INPUT_SYSTEM
        var toque = UnityEngine.InputSystem.Touchscreen.current;
        var pad = UnityEngine.InputSystem.Gamepad.current;
        var teclado = UnityEngine.InputSystem.Keyboard.current;

        if (toque != null && toque.primaryTouch.press.isPressed)
            dispositivo = Dispositivo.Toque;
        else if (pad != null && (pad.leftStick.ReadValue().sqrMagnitude > 0.09f
                                 || pad.dpad.ReadValue().sqrMagnitude > 0.01f
                                 || pad.buttonSouth.isPressed
                                 || pad.startButton.isPressed))
            dispositivo = Dispositivo.Gamepad;
        else if (teclado != null && teclado.anyKey.isPressed)
            dispositivo = Dispositivo.Teclado;
#else
        if (Input.touchCount > 0)
            dispositivo = Dispositivo.Toque;
        else if (Input.anyKey)
            dispositivo = Dispositivo.Teclado;
#endif
    }

    string TextoControlesAtual()
    {
        switch (dispositivo)
        {
            case Dispositivo.Gamepad: return textoControlesGamepad;
            case Dispositivo.Toque: return textoControlesToque;
            default: return textoControles;
        }
    }

    void AtualizarFadeIn()
    {
        if (tFadeIn >= duracaoFadeIn) return;

        tFadeIn += Time.unscaledDeltaTime;
        float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(tFadeIn / Mathf.Max(0.01f, duracaoFadeIn)));

        grupoRaiz.alpha = k;
        fadePreto.color = new Color(0f, 0f, 0f, 1f - k);

        if (tFadeIn >= duracaoFadeIn)
        {
            grupoRaiz.alpha = 1f;
            fadePreto.gameObject.SetActive(false);
        }
    }

    void AtualizarPainelTempo(GameManager gm)
    {
        if (gm.EmPreparo)
        {
            // Contagem calma: barra roxa que esvazia e número grande.
            float pp = Mathf.Clamp01(gm.Preparo01);
            barraFill.anchorMax = new Vector2(pp, 1f);
            barraFillImg.enabled = pp > 0.003f;
            barraFillImg.color = corPreparo;

            SetText(lblAlvo, textoPreparo);
            SetText(txtTempo, Mathf.CeilToInt(gm.PreparoRestante).ToString());
            return;
        }

        // Rodada valendo: barra verde que vai ficando vermelha perto do fim.
        float p = Mathf.Clamp01(gm.Progresso01);
        barraFill.anchorMax = new Vector2(p, 1f);
        barraFillImg.enabled = p > 0.003f;
        barraFillImg.color = Color.Lerp(corPerigo, corVerde, Mathf.InverseLerp(0.15f, 0.45f, p));

        SetText(lblAlvo, textoAlvo);
        SetText(txtTempo, gm.TempoRestante.ToString("0.0", CultureInfo.InvariantCulture) + "s");
    }

    void AtualizarPainelAlvo(GameManager gm)
    {
        // Mostra a cor-alvo exata (a última continua visível na pausa e na derrota).
        // Um deslizar bem curto evita o "estouro" de cor, sem atrasar a leitura.
        Color alvo = gm.TemCorAlvo ? gm.CorAlvo : corAlvoVazia;
        alvo.a = 1f;

        float suavizar = 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime);
        corAlvoAtual = Color.Lerp(corAlvoAtual, alvo, suavizar);
        quadroAlvo.color = corAlvoAtual;

        txtInterrogacao.gameObject.SetActive(!gm.TemCorAlvo);

        // Brilho em volta da cor-alvo; pulsa de leve na batida (nada de piscar forte).
        float pulso = 0f;
        var am = AudioManager.Instance;
        if (pulsarNaBatida && gm.TemCorAlvo && am != null && am.IsPlaying)
            pulso = 1f - am.BeatProgress;

        Color g = corAlvoAtual;
        g.a = gm.TemCorAlvo ? 0.30f + 0.25f * pulso : 0f;
        glowAlvo.color = g;
    }

    void AtualizarMensagem(GameManager gm)
    {
        bool derrota = gm.FimDeJogo;
        bool dicaInicio = !derrota && (gm.EmPreparo || (gm.Rodada >= 1 && gm.Rodada <= rodadasComDica));
        bool mostrar = dicaInicio || derrota;

        string t1 = derrota ? textoDerrota : textoRegra;
        string t2 = derrota ? textoDerrotaExplicacao : TextoControlesAtual();
        Color c2 = derrota ? Color.white : corVerde;

        bool textoIgual = txtDica.text == t1;

        // Se o texto precisa mudar, primeiro some, depois troca, depois volta (sem corte seco).
        float alvo = (mostrar && textoIgual) ? 1f : 0f;
        alphaDica = Mathf.MoveTowards(alphaDica, alvo, Time.unscaledDeltaTime * 2.5f);

        if (mostrar && !textoIgual && alphaDica <= 0.01f)
        {
            txtDica.text = t1;
            txtControles.text = t2;
            txtControles.color = c2;
        }

        // Troca de dispositivo com a dica visível: atualiza só a linha de controles.
        if (mostrar && !derrota && textoIgual)
            SetText(txtControles, t2);

        grupoDica.alpha = alphaDica;
    }

    void AtualizarPontuacao(GameManager gm)
    {
        float dt = Time.unscaledDeltaTime;

        // O número sobe aos poucos (contagem). Se zerou (reinício), vai direto.
        float alvo = gm.Pontuacao;
        if (alvo < pontExibida) pontExibida = alvo;
        else pontExibida = Mathf.Lerp(pontExibida, alvo, 1f - Mathf.Exp(-10f * dt));
        if (alvo - pontExibida < 0.5f) pontExibida = alvo;

        SetText(txtPontos, Mathf.RoundToInt(pontExibida).ToString(CultureInfo.InvariantCulture));

        // Recorde: vira "NEW BEST!" dourado quando o player passa o recorde antigo.
        if (gm.NovoRecorde)
        {
            SetText(txtRecorde, textoNovoRecorde);
            txtRecorde.color = corDestaque;
        }
        else
        {
            SetText(txtRecorde, textoRecorde + " " + gm.Recorde.ToString(CultureInfo.InvariantCulture));
            txtRecorde.color = corTextoSuave;
        }

        // Combo: aparece abaixo da pontuação enquanto houver multiplicador.
        bool comboAtivo = gm.Combo >= 1 && !gm.FimDeJogo;
        grupoCombo.alpha = Mathf.MoveTowards(grupoCombo.alpha, comboAtivo ? 1f : 0f, dt * 4f);
        if (comboAtivo)
            SetText(txtCombo, textoCombo + " x" + gm.Multiplicador.ToString("0.##", CultureInfo.InvariantCulture));

        // Rodada vencida: número dá um "pop" e o "+pontos" sobe e some.
        if (ultimoGanhoUid < 0)
        {
            ultimoGanhoUid = gm.GanhoUid;   // primeira leitura: só sincroniza, sem animar
        }
        else if (gm.GanhoUid != ultimoGanhoUid)
        {
            ultimoGanhoUid = gm.GanhoUid;

            if (popPontosCo != null) StopCoroutine(popPontosCo);
            popPontosCo = StartCoroutine(Pop(txtPontos.rectTransform, 0.15f));

            if (ganhoCo != null) StopCoroutine(ganhoCo);
            ganhoCo = StartCoroutine(MostrarGanho(gm.UltimoGanho));
        }
    }

    IEnumerator MostrarGanho(int pontos)
    {
        SetText(txtGanho, "+" + pontos.ToString(CultureInfo.InvariantCulture));

        RectTransform rt = txtGanho.rectTransform;
        Vector2 baseP = new Vector2(-400f, -92f);

        const float duracao = 0.9f;
        float t = 0f;

        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duracao);
            float sobe = 1f - (1f - k) * (1f - k);          // sobe rápido e desacelera

            rt.anchoredPosition = baseP + Vector2.up * (36f * sobe);
            txtGanho.alpha = k < 0.6f ? 1f : 1f - (k - 0.6f) / 0.4f;
            yield return null;
        }

        txtGanho.alpha = 0f;
        rt.anchoredPosition = baseP;
    }

    static void SetText(TMP_Text t, string s)
    {
        if (t.text != s) t.text = s;
    }

    IEnumerator Pop(RectTransform rt, float forca)
    {
        const float duracao = 0.25f;
        float t = 0f;

        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            float k = 1f - Mathf.Clamp01(t / duracao);
            rt.localScale = Vector3.one * (1f + forca * k * k);   // leve, só para chamar atenção sem assustar
            yield return null;
        }

        rt.localScale = Vector3.one;
    }

    // ------------------------------------------------------------------
    // CONSTRUÇÃO DA UI (tudo por código)
    // ------------------------------------------------------------------
    void BuildUI()
    {
        // ---------- Canvas do HUD
        var canvasGo = new GameObject("HUDCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Começa invisível; o fade-in liga o HUD aos poucos.
        grupoRaiz = canvasGo.AddComponent<CanvasGroup>();
        grupoRaiz.alpha = 0f;
        grupoRaiz.interactable = false;
        grupoRaiz.blocksRaycasts = false;

        // Sem GraphicRaycaster de propósito: o HUD nunca bloqueia cliques.
        var root = (RectTransform)canvasGo.transform;
        RectTransform conteudo;

        // ---------- RODADA (topo esquerdo): pequeno, só informação
        MakePanel(root, "PainelRodada",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(40f, -40f), new Vector2(250f, 104f), 28,
            out conteudo);

        var lblRodada = MakeText(conteudo, "Label", textoRodada, 24f, corTextoSuave, TextAlignmentOptions.MidlineLeft);
        Place(lblRodada.rectTransform,
            new Vector2(0f, 0.62f), Vector2.one,
            new Vector2(24f, 0f), new Vector2(-16f, -6f));

        txtRodada = MakeText(conteudo, "Valor", "--", 56f, Color.white, TextAlignmentOptions.MidlineLeft);
        Place(txtRodada.rectTransform,
            Vector2.zero, new Vector2(1f, 0.66f),
            new Vector2(24f, 6f), new Vector2(-16f, 0f),
            new Vector2(0f, 0.5f));   // pivô à esquerda: o "pop" cresce para a direita

        // ---------- PONTUAÇÃO (topo direito): total, recorde e combo
        MakePanel(root, "PainelPontos",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-40f, -40f), new Vector2(340f, 104f), 28,
            out conteudo);

        var lblPontos = MakeText(conteudo, "Label", textoPontos, 24f, corTextoSuave, TextAlignmentOptions.MidlineLeft);
        Place(lblPontos.rectTransform,
            new Vector2(0f, 0.62f), new Vector2(0.4f, 1f),
            new Vector2(24f, 0f), new Vector2(0f, -6f));

        txtRecorde = MakeText(conteudo, "Recorde", textoRecorde + " 0", 24f, corTextoSuave, TextAlignmentOptions.MidlineRight);
        Place(txtRecorde.rectTransform,
            new Vector2(0.35f, 0.62f), Vector2.one,
            new Vector2(0f, 0f), new Vector2(-24f, -6f));

        txtPontos = MakeText(conteudo, "Valor", "0", 56f, Color.white, TextAlignmentOptions.MidlineLeft);
        Place(txtPontos.rectTransform,
            Vector2.zero, new Vector2(1f, 0.66f),
            new Vector2(24f, 6f), new Vector2(-16f, 0f),
            new Vector2(0f, 0.5f));   // pivô à esquerda: o "pop" cresce para a direita

        RectTransform raizCombo = MakePanel(root, "PainelCombo",
            new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-40f, -158f), new Vector2(260f, 60f), 22,
            out conteudo);

        grupoCombo = raizCombo.gameObject.AddComponent<CanvasGroup>();
        grupoCombo.alpha = 0f;
        grupoCombo.interactable = false;
        grupoCombo.blocksRaycasts = false;

        txtCombo = MakeText(conteudo, "Combo", textoCombo + " x1", 30f, corDestaque, TextAlignmentOptions.Center);
        Inset(txtCombo.rectTransform, 0f);

        // "+pontos" que sobe ao lado do painel de pontuação quando o player acerta.
        txtGanho = MakeText(root, "Ganho", "+0", 52f, corDestaque, TextAlignmentOptions.MidlineRight);
        RectTransform ganhoRt = txtGanho.rectTransform;
        ganhoRt.anchorMin = new Vector2(1f, 1f);
        ganhoRt.anchorMax = new Vector2(1f, 1f);
        ganhoRt.pivot = new Vector2(1f, 0.5f);
        ganhoRt.sizeDelta = new Vector2(260f, 70f);
        ganhoRt.anchoredPosition = new Vector2(-400f, -92f);
        txtGanho.alpha = 0f;

        // ---------- ALVO + TEMPO (topo centro): o protagonista
        MakePanel(root, "PainelAlvo",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -30f), new Vector2(540f, 206f), 36,
            out conteudo);

        // Brilho atrás da cor-alvo (criado primeiro para ficar por baixo de tudo).
        glowAlvo = MakeRounded(conteudo, "Glow", new Color(1f, 1f, 1f, 0f), 30);
        Place(glowAlvo.rectTransform,
            new Vector2(0f, 0.30f), new Vector2(1f, 0.76f),
            new Vector2(18f, -6f), new Vector2(-18f, 6f));

        Image moldura = MakeRounded(conteudo, "Moldura", corMolduraAlvo, 24);
        Place(moldura.rectTransform,
            new Vector2(0f, 0.30f), new Vector2(1f, 0.76f),
            new Vector2(24f, 0f), new Vector2(-24f, 0f));
        molduraAlvoRt = moldura.rectTransform;

        quadroAlvo = MakeRounded(moldura.rectTransform, "CorAlvo", corAlvoVazia, 20);
        Inset(quadroAlvo.rectTransform, 5f);

        txtInterrogacao = MakeText(moldura.rectTransform, "Interrogacao", "?", 64f, corTextoSuave, TextAlignmentOptions.Center);
        Inset(txtInterrogacao.rectTransform, 0f);

        lblAlvo = MakeText(conteudo, "Label", textoAlvo, 28f, corTextoSuave, TextAlignmentOptions.Center);
        Place(lblAlvo.rectTransform,
            new Vector2(0f, 0.78f), Vector2.one,
            new Vector2(20f, 0f), new Vector2(-20f, -6f));

        Image trilho = MakeRounded(conteudo, "Trilho", corTrilho, 16);
        Place(trilho.rectTransform,
            new Vector2(0f, 0.06f), new Vector2(1f, 0.26f),
            new Vector2(24f, 0f), new Vector2(-24f, 0f));

        Image fill = MakeRounded(trilho.rectTransform, "Fill", corVerde, 13);
        Place(fill.rectTransform,
            Vector2.zero, Vector2.one,
            new Vector2(3f, 3f), new Vector2(-3f, -3f));

        barraFill = fill.rectTransform;
        barraFillImg = fill;

        txtTempo = MakeText(trilho.rectTransform, "Tempo", "0.0s", 28f, Color.white, TextAlignmentOptions.Center);
        Inset(txtTempo.rectTransform, 0f);

        // ---------- DICA (embaixo, centro): regra curta ou explicação da derrota
        RectTransform raizDica = MakePanel(root, "PainelDica",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, margemDica), new Vector2(1000f, 96f), 28,
            out conteudo);

        grupoDica = raizDica.gameObject.AddComponent<CanvasGroup>();
        grupoDica.alpha = 0f;
        grupoDica.interactable = false;
        grupoDica.blocksRaycasts = false;

        txtDica = MakeText(conteudo, "Dica", textoRegra, 34f, Color.white, TextAlignmentOptions.Center);
        Place(txtDica.rectTransform,
            new Vector2(0f, 0.46f), Vector2.one,
            new Vector2(24f, 0f), new Vector2(-24f, -6f));

        txtControles = MakeText(conteudo, "Controles", textoControles, 26f, corVerde, TextAlignmentOptions.Center);
        Place(txtControles.rectTransform,
            Vector2.zero, new Vector2(1f, 0.46f),
            new Vector2(24f, 8f), new Vector2(-24f, 0f));

        // ---------- Fade preto de entrada (Canvas separado, por cima de tudo)
        var fadeGo = new GameObject("HUDFade", typeof(RectTransform));
        fadeGo.transform.SetParent(transform, false);

        var fadeCanvas = fadeGo.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 500;

        fadePreto = MakeImage((RectTransform)fadeGo.transform, "Preto", Color.black);
        Inset(fadePreto.rectTransform, 0f);
    }

    /// <summary>
    /// Painel arredondado: sombra, borda e miolo escuro.
    /// Retorna a raiz; "conteudo" é o miolo, onde vão os filhos.
    /// </summary>
    RectTransform MakePanel(RectTransform parent, string nome,
        Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int raio,
        out RectTransform conteudo)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        // Sombra projetada (um pouco para baixo)
        Image sombra = MakeRounded(rt, "Sombra", new Color(0f, 0f, 0f, 0.38f), raio);
        Place(sombra.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(4f, -10f), new Vector2(-4f, -10f));

        Image borda = MakeRounded(rt, "Borda", corBorda, raio);
        Inset(borda.rectTransform, 0f);

        Image fundo = MakeRounded(borda.rectTransform, "Fundo", corPainel, Mathf.Max(4, raio - 4));
        Inset(fundo.rectTransform, 4f);

        conteudo = fundo.rectTransform;
        return rt;
    }

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

    Image MakeRounded(RectTransform parent, string nome, Color cor, int raio)
    {
        Image img = MakeImage(parent, nome, cor);
        img.sprite = RoundedSprite(raio);
        img.type = Image.Type.Sliced;
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

        // Contorno escuro: dá leitura sobre qualquer fundo e o aspecto "de jogo".
        if (larguraContorno > 0f)
        {
            t.outlineWidth = larguraContorno;
            t.outlineColor = corContorno;
        }

        return t;
    }

    /// <summary>
    /// Retângulo arredondado branco (para tingir com Image.color), em 9-slice,
    /// gerado por código. Fica em cache por raio.
    /// </summary>
    public static Sprite RoundedSprite(int raio)
    {
        if (cacheSprites.TryGetValue(raio, out Sprite existente) && existente != null)
            return existente;

        int size = raio * 2 + 2;   // sobra uma faixa central que estica no 9-slice
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x + 0.5f;
                float fy = y + 0.5f;
                float cx = Mathf.Clamp(fx, raio, size - raio);
                float cy = Mathf.Clamp(fy, raio, size - raio);
                float d = Vector2.Distance(new Vector2(fx, fy), new Vector2(cx, cy));
                float a = Mathf.Clamp01(raio - d + 0.5f);   // borda suavizada
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        tex.SetPixels32(px);
        tex.Apply();

        Sprite sp = Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(raio, raio, raio, raio));

        cacheSprites[raio] = sp;
        return sp;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Vector2? pivot = null)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
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