using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tela de Game Over no ESTILO PIXEL DO MENU, montada por codigo (sem prefab nem imagens).
///
/// - Painel preto com borda roxa de cantos "escada" (pixel art), na LATERAL DIREITA:
///   o jogador continua enxergando qual era o bloco certo.
/// - Titulo em bloco roxo que "cai" com quique, borda que pisca vermelha no impacto.
/// - Faixa das 6 cores das notas que pisca no ritmo da musica.
/// - Pontuacao com contagem, recorde, e "NEW BEST!" piscando (com jingle).
/// - Botoes como os do menu: fundo roxo escuro e, quando selecionado (mouse, teclado, controle
///   ou toque), borda verde com seta pixel balancando. TRY AGAIN ja vem selecionado.
///
/// IMPORTANTE: arraste no campo "Fonte Pixel" a MESMA fonte (TMP Font Asset) dos botoes do menu.
///
/// Os nomes publicos continuam os mesmos (MostrarGameOver, TryAgain, IrParaMenu), entao as
/// ligacoes que voce ja tem no Inspector continuam funcionando.
/// </summary>
public class GameOverUI : MonoBehaviour
{
    [Header("Painel antigo (opcional)")]
    [Tooltip("O painel antigo da cena. Fica sempre escondido: o painel novo e montado por codigo.")]
    public GameObject painelGameOver;

    [Tooltip("Nome exato da cena do menu")]
    public string nomeCenaMenu = "Menu";

    [Header("Textos")]
    [Tooltip("Espacos viram quebra de linha no titulo (GAME OVER fica em duas linhas, como o menu).")]
    public string textoTitulo = "GAME OVER";
    public string textoPontos = "SCORE";
    public string textoRecorde = "BEST";
    public string textoNovoRecorde = "NEW BEST!";
    public string textoTentar = "TRY AGAIN";
    public string textoMenu = "MENU";

    [Header("Aparicao")]
    [Tooltip("Espera antes de mostrar o painel, para o jogador ver qual era o bloco certo (segundos).")]
    public float atraso = 0.6f;
    public float duracaoFade = 0.35f;

    [Header("Estilo pixel (igual ao menu)")]
    [Tooltip("Arraste a MESMA fonte pixel usada nos botoes do menu (TMP Font Asset). Vazio = usa a fonte do HUD.")]
    public TMP_FontAsset fontePixel;
    [Tooltip("Fonte alternativa. Vazio = usa a mesma fonte do HUD.")]
    public TMP_FontAsset font;
    public int sortingOrder = 20;
    [Tooltip("Tamanho de 1 'pixel' do visual, em px (base 1920x1080). Bordas e cantos sao multiplos disso.")]
    public float pixel = 5f;
    public float larguraPainel = 400f;
    public float alturaPainel = 660f;
    [Tooltip("Distancia do painel ate a borda direita da tela (deixe folga para a ponta do tabuleiro).")]
    public float margemLateral = 30f;
    [Range(0f, 0.5f)] public float contornoTexto = 0.22f;
    [Tooltip("O titulo e a faixa de cores batem no ritmo da musica.")]
    public bool pulsarNaBatida = true;
    [Tooltip("Toca o jingle quando bate o recorde.")]
    public bool jingleNovoRecorde = true;

    [Header("Paleta")]
    public Color corBordaPainel = new Color(0.43f, 0.25f, 1.00f);
    public Color corFundoPainel = new Color(0.03f, 0.02f, 0.07f, 1f);
    public Color corBlocoTitulo = new Color(0.48f, 0.27f, 1.00f);
    [Tooltip("Cor em que a borda pisca no impacto da derrota.")]
    public Color corImpacto = new Color(0.95f, 0.25f, 0.25f);
    public Color corContornoTexto = new Color(0f, 0f, 0f, 1f);
    public Color corTextoSuave = new Color(1f, 1f, 1f, 0.75f);
    public Color corDestaque = new Color(1.00f, 0.82f, 0.25f);
    public Color corBotaoBorda = new Color(0.43f, 0.25f, 1.00f);
    public Color corBotaoFundo = new Color(0.13f, 0.08f, 0.26f);
    public Color corBotaoBordaSel = new Color(0.24f, 0.86f, 0.52f);
    public Color corBotaoFundoSel = new Color(0.05f, 0.22f, 0.15f);

    // --- runtime ---
    class BotaoPixel
    {
        public Button botao;
        public RectTransform raiz;
        public List<Image> borda = new List<Image>();
        public List<Image> fundo = new List<Image>();
        public RectTransform seta;
        public float sel;   // 0..1 (suavizado)
    }

    CanvasGroup grupo;
    TextMeshProUGUI txtTitulo;
    TextMeshProUGUI txtPontos;
    TextMeshProUGUI txtRecorde;
    RectTransform blocoTitulo;
    Vector2 baseTitulo;
    readonly List<Image> bordaPainel = new List<Image>();
    readonly Image[] faixa = new Image[6];
    readonly float[] faixaEscala = { 1f, 1f, 1f, 1f, 1f, 1f };
    BotaoPixel btnTentar;
    BotaoPixel btnMenu;
    Coroutine rotina;
    bool mostrandoNovoRecorde;

    static readonly Color[] CoresFaixa =
    {
        new Color(1.00f, 0.20f, 0.25f),   // vermelho
        new Color(1.00f, 0.85f, 0.20f),   // amarelo
        new Color(0.20f, 1.00f, 0.45f),   // verde
        new Color(0.20f, 0.95f, 1.00f),   // ciano
        new Color(0.25f, 0.40f, 1.00f),   // azul
        new Color(0.70f, 0.30f, 1.00f)    // roxo
    };

    void Awake()
    {
        if (painelGameOver != null)
            painelGameOver.SetActive(false);

        if (font == null)
        {
            GameHUD hud = FindFirstObjectByType<GameHUD>();
            if (hud != null) font = hud.font;
        }

        BuildUI();
        Esconder();
    }

    // ------------------------------------------------------------------
    // API PUBLICA (mesmos nomes de antes)
    // ------------------------------------------------------------------
    public void MostrarGameOver()
    {
        if (painelGameOver != null)
            painelGameOver.SetActive(false);   // o painel antigo nunca aparece

        // Idempotente: se o painel ja esta entrando (ou ja esta na tela), ignora chamadas repetidas.
        // Assim, mesmo com dois eventos ligados aqui no Inspector, a animacao nao reinicia nem pisca.
        if (rotina != null || (grupo != null && grupo.alpha > 0.01f))
            return;

        rotina = StartCoroutine(Aparecer());
    }

    public void TryAgain()
    {
        Esconder();

        if (GameManager.Instance != null)
            GameManager.Instance.ReiniciarJogo();
    }

    public void IrParaMenu()
    {
        SceneManager.LoadScene(nomeCenaMenu);
    }

    // ------------------------------------------------------------------
    // ANIMACAO EM LOOP
    // ------------------------------------------------------------------
    void Update()
    {
        if (grupo == null || grupo.alpha <= 0.01f) return;

        float dt = Time.unscaledDeltaTime;

        GameObject selecionado = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        AtualizarBotao(btnTentar, selecionado, dt);
        AtualizarBotao(btnMenu, selecionado, dt);

        // Ritmo da musica (ou relogio interno se nao houver audio).
        AudioManager am = AudioManager.Instance;
        bool temMusica = am != null && am.IsPlaying;
        float beatLen = am != null ? Mathf.Max(0.05f, am.BeatLength) : 0.8f;
        float relogio = temMusica ? am.MusicTime : Time.unscaledTime;

        float pulso = 0f;
        if (pulsarNaBatida)
        {
            float k = 1f - Mathf.Repeat(relogio / beatLen, 1f);
            pulso = 0.03f * k * k * k;
        }
        blocoTitulo.localScale = Vector3.one * (1f + pulso);

        // Faixa das 6 cores: o quadradinho da vez cresce a cada batida.
        int vez = pulsarNaBatida ? Mathf.FloorToInt(relogio / beatLen) % faixa.Length : -1;
        if (vez < 0) vez += faixa.Length;

        float suave = 1f - Mathf.Exp(-18f * dt);
        for (int i = 0; i < faixa.Length; i++)
        {
            float alvo = (pulsarNaBatida && i == vez) ? 1.5f : 1f;
            faixaEscala[i] = Mathf.Lerp(faixaEscala[i], alvo, suave);
            faixa[i].rectTransform.localScale = new Vector3(faixaEscala[i], faixaEscala[i], 1f);
        }

        // NEW BEST! pisca.
        if (mostrandoNovoRecorde)
        {
            bool aceso = Mathf.Repeat(Time.unscaledTime, 0.7f) < 0.45f;
            txtRecorde.color = aceso ? corDestaque : Color.Lerp(corDestaque, corFundoPainel, 0.65f);
        }
    }

    void AtualizarBotao(BotaoPixel b, GameObject selecionado, float dt)
    {
        bool ativo = selecionado == b.botao.gameObject;
        b.sel = Mathf.MoveTowards(b.sel, ativo ? 1f : 0f, dt * 10f);

        Color cBorda = Color.Lerp(corBotaoBorda, corBotaoBordaSel, b.sel);
        Color cFundo = Color.Lerp(corBotaoFundo, corBotaoFundoSel, b.sel);

        for (int i = 0; i < b.borda.Count; i++) b.borda[i].color = cBorda;
        for (int i = 0; i < b.fundo.Count; i++) b.fundo[i].color = cFundo;

        // Seta pixel: aparece e balanca de leve quando selecionado.
        b.seta.localScale = new Vector3(b.sel, b.sel, 1f);
        b.seta.anchoredPosition = new Vector2(pixel * 3.6f + Mathf.Sin(Time.unscaledTime * 9f) * pixel * 0.6f * b.sel, 0f);
    }

    // ------------------------------------------------------------------
    // APARICAO
    // ------------------------------------------------------------------
    IEnumerator Aparecer()
    {
        // Deixa o jogador ver qual era o bloco certo antes do painel entrar.
        float t = 0f;
        while (t < atraso)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        GameManager gm = GameManager.Instance;
        int pontos = gm != null ? gm.Pontuacao : 0;
        int recorde = gm != null ? gm.Recorde : 0;
        bool novoRecorde = gm != null && gm.NovoRecorde;

        txtPontos.text = "0";
        mostrandoNovoRecorde = false;

        if (novoRecorde)
        {
            txtRecorde.text = textoNovoRecorde;
            txtRecorde.color = corDestaque;
        }
        else
        {
            txtRecorde.text = textoRecorde + " " + recorde.ToString(CultureInfo.InvariantCulture);
            txtRecorde.color = corTextoSuave;
        }

        grupo.alpha = 0f;
        grupo.interactable = true;
        grupo.blocksRaycasts = true;

        // Entrada: fade + titulo cai com quique + borda pisca vermelha e assenta no roxo.
        float alturaQueda = pixel * 30f;
        float dur = Mathf.Max(Mathf.Max(0.01f, duracaoFade), 0.6f);
        t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;

            grupo.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.01f, duracaoFade)));

            float queda = Mathf.Clamp01(t / 0.55f);
            blocoTitulo.anchoredPosition = baseTitulo + Vector2.up * (alturaQueda * (1f - EaseOutBounce(queda)));

            Color cb = Color.Lerp(corImpacto, corBordaPainel, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.6f)));
            PintarBordaPainel(cb);

            yield return null;
        }

        grupo.alpha = 1f;
        blocoTitulo.anchoredPosition = baseTitulo;
        PintarBordaPainel(corBordaPainel);

        // TRY AGAIN ja selecionado: da para jogar de novo so com o controle ou o teclado.
        Selecionar(btnTentar.botao.gameObject);

        // Contagem dos pontos.
        const float contagem = 0.7f;
        t = 0f;
        while (t < contagem)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / contagem);
            float suave = 1f - (1f - k) * (1f - k);
            txtPontos.text = Mathf.RoundToInt(Mathf.Lerp(0f, pontos, suave)).ToString(CultureInfo.InvariantCulture);
            yield return null;
        }

        txtPontos.text = pontos.ToString(CultureInfo.InvariantCulture);

        if (novoRecorde)
        {
            mostrandoNovoRecorde = true;
            if (jingleNovoRecorde && AudioManager.Instance != null)
                AudioManager.Instance.PlayJingle();
        }

        rotina = null;
    }

    void Esconder()
    {
        if (rotina != null)
        {
            StopCoroutine(rotina);
            rotina = null;
        }

        mostrandoNovoRecorde = false;

        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;

        blocoTitulo.anchoredPosition = baseTitulo;
        PintarBordaPainel(corBordaPainel);

        // Nao deixa um botao escondido como "selecionado".
        if (EventSystem.current != null)
        {
            GameObject atual = EventSystem.current.currentSelectedGameObject;
            if (atual != null && (atual == btnTentar.botao.gameObject || atual == btnMenu.botao.gameObject))
                EventSystem.current.SetSelectedGameObject(null);
        }
    }

    void PintarBordaPainel(Color cor)
    {
        for (int i = 0; i < bordaPainel.Count; i++)
            bordaPainel[i].color = cor;
    }

    static void Selecionar(GameObject alvo)
    {
        if (alvo != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(alvo);
    }

    // ------------------------------------------------------------------
    // CONSTRUCAO DA UI (tudo por codigo)
    // ------------------------------------------------------------------
    void BuildUI()
    {
        var canvasGo = new GameObject("GameOverCanvas", typeof(RectTransform));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();
        grupo = canvasGo.AddComponent<CanvasGroup>();

        var root = (RectTransform)canvasGo.transform;

        // Sombra projetada (bloco preto deslocado), criada antes para ficar por tras.
        RectTransform sombra = NovoRect("Sombra", root);
        PosicionarPainel(sombra);
        sombra.anchoredPosition += new Vector2(pixel * 1.5f, -pixel * 1.5f);
        Banda3(sombra, "S", new Color(0f, 0f, 0f, 0.5f), pixel, null);

        // Painel: borda roxa grossa + miolo preto, cantos em "escada".
        RectTransform miolo;
        RectTransform painel = CaixaPixel(root, "Painel", corBordaPainel, corFundoPainel,
            pixel * 2f, pixel, bordaPainel, null, out miolo);
        PosicionarPainel(painel);

        // Layout em pixels, de cima para baixo (com respiro entre os blocos).
        // Titulo: bloco roxo com texto branco pixel (como no menu).
        blocoTitulo = NovoRect("BlocoTitulo", miolo);
        PlaceTop(blocoTitulo, 24f, 130f, 24f);
        Banda3(blocoTitulo, "T", corBlocoTitulo, pixel, null);

        txtTitulo = MakeText(blocoTitulo, "Titulo", textoTitulo.Replace(' ', '\n'), 54f, Color.white, TextAlignmentOptions.Center);
        Inset(txtTitulo.rectTransform, pixel * 3f);
        baseTitulo = blocoTitulo.anchoredPosition;

        // Faixa das 6 cores das notas (pisca no ritmo).
        RectTransform faixaRt = NovoRect("Faixa", miolo);
        faixaRt.anchorMin = faixaRt.anchorMax = new Vector2(0.5f, 1f);
        faixaRt.anchoredPosition = new Vector2(0f, -190f);
        float lado = pixel * 4f;
        float passo = lado + pixel * 2f;
        for (int i = 0; i < faixa.Length; i++)
        {
            Image q = NovaImagem(faixaRt, "Cor" + i, CoresFaixa[i]);
            q.rectTransform.sizeDelta = new Vector2(lado, lado);
            q.rectTransform.anchoredPosition = new Vector2((i - 2.5f) * passo, 0f);
            faixa[i] = q;
        }

        var lblPontos = MakeText(miolo, "LabelPontos", textoPontos, 32f, corTextoSuave, TextAlignmentOptions.Center);
        PlaceTop(lblPontos.rectTransform, 214f, 36f, 16f);

        txtPontos = MakeText(miolo, "Pontos", "0", 96f, Color.white, TextAlignmentOptions.Center);
        PlaceTop(txtPontos.rectTransform, 250f, 112f, 16f);

        txtRecorde = MakeText(miolo, "Recorde", textoRecorde + " 0", 34f, corTextoSuave, TextAlignmentOptions.Center);
        PlaceTop(txtRecorde.rectTransform, 368f, 46f, 16f);

        btnTentar = CriarBotao(miolo, "BotaoTentar", textoTentar, 442f, 78f, TryAgain);
        btnMenu = CriarBotao(miolo, "BotaoMenu", textoMenu, 536f, 78f, IrParaMenu);
    }

    void PosicionarPainel(RectTransform rt)
    {
        rt.anchorMin = new Vector2(1f, 0.5f);
        rt.anchorMax = new Vector2(1f, 0.5f);
        rt.pivot = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-margemLateral, -10f);
        rt.sizeDelta = new Vector2(larguraPainel, alturaPainel);
    }

    BotaoPixel CriarBotao(RectTransform pai, string nome, string texto, float top, float altura, UnityAction aoClicar)
    {
        var b = new BotaoPixel();

        RectTransform miolo;
        b.raiz = CaixaPixel(pai, nome, corBotaoBorda, corBotaoFundo, pixel, pixel, b.borda, b.fundo, out miolo);
        PlaceTop(b.raiz, top, altura, pixel * 3f);

        // Toda a caixa recebe toque/mouse.
        foreach (Image img in b.borda) img.raycastTarget = true;
        foreach (Image img in b.fundo) img.raycastTarget = true;

        var botao = b.raiz.gameObject.AddComponent<Button>();
        botao.transition = Selectable.Transition.None;   // as cores sao controladas por AtualizarBotao
        botao.targetGraphic = b.fundo[1];
        botao.onClick.AddListener(aoClicar);
        b.botao = botao;

        // Passar o mouse seleciona (assim so um botao fica aceso, como no menu).
        var et = b.raiz.gameObject.AddComponent<EventTrigger>();
        var entrada = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        GameObject alvo = b.raiz.gameObject;
        entrada.callback.AddListener(_ => Selecionar(alvo));
        et.triggers.Add(entrada);

        // Texto.
        // Texto centralizado, com folga dos dois lados (a seta fica na folga da esquerda, sem cobrir letras).
        TextMeshProUGUI t = MakeText(miolo, "Texto", texto, 32f, Color.white, TextAlignmentOptions.Center);
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = new Vector2(pixel * 9f, pixel * 1.5f);
        t.rectTransform.offsetMax = new Vector2(-pixel * 9f, -pixel * 1.5f);

        // Seta pixel (triangulo de 7 linhas) a esquerda do texto.
        b.seta = CriarSeta(miolo);

        b.raiz.gameObject.AddComponent<ButtonSfx>();   // sons de hover e clique, como no menu
        return b;
    }

    /// <summary>Triangulo "▶" feito de quadradinhos: 7 linhas com larguras 1,2,3,4,3,2,1.</summary>
    RectTransform CriarSeta(RectTransform pai)
    {
        float u = pixel * 0.8f;
        int[] larguras = { 1, 2, 3, 4, 3, 2, 1 };

        RectTransform seta = NovoRect("Seta", pai);
        seta.anchorMin = seta.anchorMax = new Vector2(0f, 0.5f);
        seta.pivot = new Vector2(0f, 0.5f);
        seta.sizeDelta = new Vector2(4f * u, 7f * u);
        seta.anchoredPosition = new Vector2(pixel * 3.6f, 0f);

        for (int i = 0; i < larguras.Length; i++)
        {
            Image linha = NovaImagem(seta, "L" + i, corBotaoBordaSel);
            RectTransform rt = linha.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(larguras[i] * u, u);
            rt.anchoredPosition = new Vector2(0f, (3 - i) * u);
        }

        seta.localScale = Vector3.zero;
        return seta;
    }

    // ------------------------------------------------------------------
    // PECAS DE PIXEL ART
    // ------------------------------------------------------------------
    /// <summary>
    /// Caixa pixel: borda + miolo, ambos com os cantos cortados em "escada".
    /// Cada camada e feita de 3 faixas que nao se sobrepoem.
    /// </summary>
    RectTransform CaixaPixel(RectTransform pai, string nome, Color corBorda, Color corFundo,
        float borda, float quina, List<Image> imgsBorda, List<Image> imgsFundo, out RectTransform miolo)
    {
        RectTransform raiz = NovoRect(nome, pai);
        Banda3(raiz, "Borda", corBorda, quina, imgsBorda);

        RectTransform dentro = NovoRect("Miolo", raiz);
        Inset(dentro, borda);
        Banda3(dentro, "Fundo", corFundo, quina, imgsFundo);

        miolo = dentro;
        return raiz;
    }

    /// <summary>Retangulo com os 4 cantos cortados por "quina", em 3 faixas: topo, meio e base.</summary>
    void Banda3(RectTransform pai, string nome, Color cor, float quina, List<Image> lista)
    {
        Image topo = NovaImagem(pai, nome + "T", cor);
        Place(topo.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f),
            new Vector2(quina, -quina), new Vector2(-quina, 0f));

        Image meio = NovaImagem(pai, nome + "M", cor);
        Place(meio.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(0f, quina), new Vector2(0f, -quina));

        Image baixo = NovaImagem(pai, nome + "B", cor);
        Place(baixo.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f),
            new Vector2(quina, 0f), new Vector2(-quina, quina));

        if (lista != null)
        {
            lista.Add(topo);
            lista.Add(meio);   // indice 1 = faixa do meio (usada como targetGraphic)
            lista.Add(baixo);
        }
    }

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

    static Image NovaImagem(RectTransform pai, string nome, Color cor)
    {
        RectTransform rt = NovoRect(nome, pai);
        var img = rt.gameObject.AddComponent<Image>();
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

        t.enableAutoSizing = true;
        t.fontSizeMax = tamanho;
        t.fontSizeMin = tamanho * 0.4f;
        t.fontSize = tamanho;

        TMP_FontAsset f = fontePixel != null ? fontePixel : font;
        if (f != null) t.font = f;
        else t.fontStyle = FontStyles.Bold;

        if (contornoTexto > 0f)
        {
            t.outlineWidth = contornoTexto;
            t.outlineColor = corContornoTexto;
        }

        return t;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    /// <summary>Posiciona de cima para baixo: "top" px de distancia do topo, "altura" px de altura, margens laterais iguais.</summary>
    static void PlaceTop(RectTransform rt, float top, float altura, float margemX)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(margemX, -(top + altura));
        rt.offsetMax = new Vector2(-margemX, -top);
    }

    static void Inset(RectTransform rt, float px)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(px, px);
        rt.offsetMax = new Vector2(-px, -px);
    }

    static float EaseOutBounce(float x)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (x < 1f / d1) return n1 * x * x;

        if (x < 2f / d1)
        {
            x -= 1.5f / d1;
            return n1 * x * x + 0.75f;
        }

        if (x < 2.5f / d1)
        {
            x -= 2.25f / d1;
            return n1 * x * x + 0.9375f;
        }

        x -= 2.625f / d1;
        return n1 * x * x + 0.984375f;
    }
}