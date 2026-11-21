using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// PLACAR TOP 10 do KROMOVOX, estilo arcade (iniciais de 3 letras), montado 100% por codigo.
///
/// COMO USAR: NADA. Ao entrar na cena do jogo ele se cria sozinho (como o MobileControls).
/// Se quiser mudar opcoes no Inspector, coloque o script em um GameObject vazio da cena do jogo.
///
/// O QUE FAZ:
///  - Painel na LATERAL ESQUERDA (o Game Over fica na direita), no mesmo estilo pixel.
///  - Aparece no Game Over e durante o preparo ("GET READY"), para quem esta assistindo ver o ranking.
///  - Se a pontuacao entra no Top 10, o jogador digita as iniciais:
///      TECLADO: digite as letras (A-Z), SETAS cima/baixo trocam a letra, esquerda/direita movem o cursor,
///               BACKSPACE volta, ENTER confirma.
///      CONTROLE: direcional (cima/baixo letra, esquerda/direita cursor) e botao A/X confirma.
///      TOQUE/MOUSE: toque numa letra para trocar, toque em OK para confirmar.
///  - Se clicar em TRY AGAIN no meio, as iniciais atuais sao salvas (nao perde a pontuacao).
///  - Salva em PlayerPrefs (no WebGL fica no navegador: use sempre o mesmo navegador/endereco).
///  - Comeca com algumas pontuacoes "de desafio" (semearRanking) para dar o que bater.
///
/// ATALHO PARA LIMPAR O RANKING (zerar os testes antes da apresentacao):
///  com o placar na tela, aperte CTRL + SHIFT + DELETE. (Tambem: botao direito no componente > Limpar ranking.)
///
/// Para remover o recurso: apague este arquivo. Nada mais no projeto depende dele.
/// </summary>
[DisallowMultipleComponent]
public class Leaderboard : MonoBehaviour
{
    const string ChaveRanking = "kromovox_ranking";
    const string ChaveUltimoNome = "kromovox_ultimo_nome";
    const int TamanhoNome = 3;
    const float AlturaLinha = 42f;
    const float TopoLinhas = 92f;

    [Header("Ranking")]
    [Range(3, 10)] public int maxEntradas = 10;
    [Tooltip("Na primeira vez (sem nada salvo), preenche algumas pontuacoes de desafio.")]
    public bool semearRanking = true;

    [Header("Quando mostrar")]
    [Tooltip("Mostra o placar tambem no preparo antes da rodada 1 (GET READY).")]
    public bool mostrarNoPreparo = true;
    [Tooltip("Espera extra depois da revelacao da derrota. 0.7 = junto com o painel de Game Over.")]
    public float extraAposDerrota = 0.7f;

    [Header("Estilo pixel (igual ao Game Over)")]
    [Tooltip("Vazio = pega a fonte do GameOverUI/HUD sozinho.")]
    public TMP_FontAsset fontePixel;
    public int sortingOrder = 21;
    public float pixel = 5f;
    public float larguraPainel = 420f;
    public float margemLateral = 30f;
    [Range(0f, 0.5f)] public float contornoTexto = 0.22f;

    [Header("Paleta")]
    public Color corBorda = new Color(0.43f, 0.25f, 1.00f);
    public Color corFundo = new Color(0.03f, 0.02f, 0.07f, 1f);
    public Color corTextoSuave = new Color(1f, 1f, 1f, 0.75f);
    public Color corDestaque = new Color(1.00f, 0.82f, 0.25f);
    public Color corBotaoBorda = new Color(0.43f, 0.25f, 1.00f);
    public Color corBotaoFundo = new Color(0.13f, 0.08f, 0.26f);
    public Color corBotaoBordaSel = new Color(0.24f, 0.86f, 0.52f);
    public Color corBotaoFundoSel = new Color(0.05f, 0.22f, 0.15f);

    // ------------------------------------------------------------------ dados
    struct Entrada
    {
        public string nome;
        public int pontos;
    }

    class Linha
    {
        public Image fundo;
        public TextMeshProUGUI rank, nome, pontos;
    }

    enum Estado { Ocioso, Editando, Liberando }

    readonly List<Entrada> lista = new List<Entrada>();
    Linha[] linhas;

    Canvas canvas;
    CanvasGroup grupo;
    RectTransform areaEntrada;
    TextMeshProUGUI txtOcioso;
    readonly Image[] celBorda = new Image[TamanhoNome];
    readonly Image[] celFundo = new Image[TamanhoNome];
    readonly TextMeshProUGUI[] celTxt = new TextMeshProUGUI[TamanhoNome];

    Estado estado = Estado.Ocioso;
    char[] letras = { 'A', 'A', 'A' };
    int cursor;
    int posEdicao;
    int pontosEdicao;
    int ultimoIndice = -1;
    float liberarEm;

    bool emDerrota;
    bool avaliado;
    float inicioDerrota;
    int pontosFinais;

    bool sujo = true;
    bool acesoAnterior;
    string corDestaqueHex;

    // controle de entrada durante a digitacao
    GameObject selAntes;
    PlayerMove pm;
    bool pmTec, pmCon, pmTou;

    // ------------------------------------------------------------------ criacao automatica
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded -= AoCarregarCena;
        SceneManager.sceneLoaded += AoCarregarCena;
        Garantir();
    }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        Garantir();
    }

    static void Garantir()
    {
        if (FindFirstObjectByType<Leaderboard>() != null) return;
        if (FindFirstObjectByType<GameManager>() == null) return;   // so na cena do jogo

        new GameObject("Leaderboard (auto)").AddComponent<Leaderboard>();
    }

    // ------------------------------------------------------------------ ciclo de vida
    void Start()
    {
        corDestaqueHex = ColorUtility.ToHtmlStringRGB(corDestaque);
        Carregar();
        AcharFonte();
        Build();
        sujo = true;
    }

    void OnDestroy()
    {
        // Saiu da cena no meio da digitacao (ex.: botao MENU): salva o que tem e solta os controles.
        if (estado == Estado.Editando) Confirmar();
        if (estado != Estado.Ocioso) Liberar();
    }

    void Update()
    {
        if (grupo == null) return;

        GameManager gm = GameManager.Instance;
        if (gm == null) return;

        float dt = Time.unscaledDeltaTime;
        float agora = Time.unscaledTime;

        // Detecta o inicio e o fim de uma derrota.
        if (gm.FimDeJogo && !emDerrota)
        {
            emDerrota = true;
            avaliado = false;
            inicioDerrota = agora;
            pontosFinais = gm.Pontuacao;
        }
        else if (!gm.FimDeJogo && emDerrota)
        {
            emDerrota = false;

            if (estado == Estado.Editando)
                Confirmar();                              // TRY AGAIN no meio: salva as iniciais atuais
            else if (!avaliado)
                RegistrarDireto(pontosFinais);            // clicou antes do placar aparecer
        }

        // Nova rodada comecou: tira o destaque da ultima entrada.
        if (gm.RodadaAtiva && ultimoIndice >= 0)
        {
            ultimoIndice = -1;
            sujo = true;
        }

        bool derrotaPronta = emDerrota && agora - inicioDerrota >= AtrasoAposDerrota(gm);

        if (derrotaPronta && !avaliado)
        {
            avaliado = true;
            IniciarEdicao(pontosFinais);
        }

        if (estado == Estado.Editando)
        {
            ManterSemSelecao();
            LerEntrada();
        }
        else if (estado == Estado.Liberando && agora >= liberarEm)
        {
            Liberar();
        }

        // Pisca do cursor.
        bool aceso = Mathf.Repeat(agora, 0.6f) < 0.36f;
        if (aceso != acesoAnterior)
        {
            acesoAnterior = aceso;
            if (estado == Estado.Editando) sujo = true;
        }

        // Atalho: CTRL + SHIFT + DELETE limpa o ranking (com o placar visivel).
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null && estado == Estado.Ocioso && grupo.alpha > 0.5f &&
            kb.ctrlKey.isPressed && kb.shiftKey.isPressed && kb.deleteKey.wasPressedThisFrame)
        {
            LimparRanking();
        }
#endif

        if (sujo) Redesenhar();

        bool mostrar = derrotaPronta
                       || (mostrarNoPreparo && gm.EmPreparo)
                       || estado != Estado.Ocioso;

        grupo.alpha = Mathf.MoveTowards(grupo.alpha, mostrar ? 1f : 0f, dt * 4f);
        canvas.enabled = grupo.alpha > 0.01f;

        bool editando = estado == Estado.Editando;
        grupo.interactable = editando;
        grupo.blocksRaycasts = editando;
    }

    float AtrasoAposDerrota(GameManager gm)
    {
        return gm.duracaoRevelacao + gm.atrasoGameOver + extraAposDerrota;
    }

    // ------------------------------------------------------------------ edicao das iniciais
    void IniciarEdicao(int pontos)
    {
        int pos = Posicao(pontos);
        if (pos < 0) return;

        string ultimo = NomeValido(PlayerPrefs.GetString(ChaveUltimoNome, "AAA"));
        letras = ultimo.ToCharArray();
        cursor = 0;
        posEdicao = pos;
        pontosEdicao = pontos;

        // Entra na lista so para aparecer (so e salvo quando confirmar).
        lista.Insert(pos, new Entrada { nome = ultimo, pontos = pontos });
        if (lista.Count > maxEntradas) lista.RemoveAt(lista.Count - 1);

        Travar();
        estado = Estado.Editando;
        sujo = true;

        AudioManager am = AudioManager.Instance;
        if (am != null) am.PlayCorrect();
    }

    void Confirmar()
    {
        if (estado != Estado.Editando) return;

        string nome = new string(letras);

        if (posEdicao >= 0 && posEdicao < lista.Count)
            lista[posEdicao] = new Entrada { nome = nome, pontos = pontosEdicao };

        PlayerPrefs.SetString(ChaveUltimoNome, nome);
        Salvar();

        ultimoIndice = posEdicao;
        estado = Estado.Liberando;
        liberarEm = Time.unscaledTime + 0.35f;   // espera para o ENTER nao cair em outro botao
        sujo = true;

        AudioManager am = AudioManager.Instance;
        if (am != null) am.PlayClick();
    }

    void RegistrarDireto(int pontos)
    {
        int pos = Posicao(pontos);
        if (pos < 0) return;

        lista.Insert(pos, new Entrada { nome = NomeValido(PlayerPrefs.GetString(ChaveUltimoNome, "AAA")), pontos = pontos });
        if (lista.Count > maxEntradas) lista.RemoveAt(lista.Count - 1);

        Salvar();
        ultimoIndice = pos;
        sujo = true;
    }

    /// <summary>Durante a digitacao: o cubo nao anda e ENTER/A nao "clicam" em TRY AGAIN.</summary>
    void Travar()
    {
        EventSystem es = EventSystem.current;
        if (es != null)
        {
            selAntes = es.currentSelectedGameObject;
            es.sendNavigationEvents = false;
            es.SetSelectedGameObject(null);
        }

        pm = FindFirstObjectByType<PlayerMove>();
        if (pm != null)
        {
            pmTec = pm.usarTeclado;
            pmCon = pm.usarControle;
            pmTou = pm.usarToque;
            pm.usarTeclado = false;
            pm.usarControle = false;
            pm.usarToque = false;
        }
    }

    void Liberar()
    {
        estado = Estado.Ocioso;

        if (pm != null)
        {
            pm.usarTeclado = pmTec;
            pm.usarControle = pmCon;
            pm.usarToque = pmTou;
        }
        pm = null;

        EventSystem es = EventSystem.current;
        if (es != null)
        {
            es.sendNavigationEvents = true;

            // So devolve a selecao se ainda estamos no Game Over (senao o botao esta escondido).
            GameManager gm = GameManager.Instance;
            if (selAntes != null && gm != null && gm.FimDeJogo)
                es.SetSelectedGameObject(selAntes);
        }

        selAntes = null;
        sujo = true;
    }

    void ManterSemSelecao()
    {
        EventSystem es = EventSystem.current;
        if (es != null && es.currentSelectedGameObject != null)
            es.SetSelectedGameObject(null);
    }

    void LerEntrada()
    {
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        if (kb != null)
        {
            for (int i = 0; i < 26; i++)
            {
                if (kb[(Key)((int)Key.A + i)].wasPressedThisFrame)
                {
                    DefinirLetra((char)('A' + i));
                    if (cursor < TamanhoNome - 1) cursor++;
                }
            }

            if (kb.upArrowKey.wasPressedThisFrame) CiclarLetra(1);
            if (kb.downArrowKey.wasPressedThisFrame) CiclarLetra(-1);
            if (kb.leftArrowKey.wasPressedThisFrame) MoverCursor(-1);
            if (kb.rightArrowKey.wasPressedThisFrame) MoverCursor(1);
            if (kb.backspaceKey.wasPressedThisFrame) MoverCursor(-1);

            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                Confirmar();
                return;
            }
        }

        if (estado != Estado.Editando) return;

        var gp = Gamepad.current;
        if (gp != null)
        {
            if (gp.dpad.up.wasPressedThisFrame) CiclarLetra(1);
            if (gp.dpad.down.wasPressedThisFrame) CiclarLetra(-1);
            if (gp.dpad.left.wasPressedThisFrame) MoverCursor(-1);
            if (gp.dpad.right.wasPressedThisFrame) MoverCursor(1);
            if (gp.buttonSouth.wasPressedThisFrame) Confirmar();
        }
#endif
    }

    void DefinirLetra(char c)
    {
        letras[cursor] = c;
        sujo = true;
        SomLetra();
    }

    void CiclarLetra(int direcao)
    {
        int n = letras[cursor] - 'A' + direcao;
        n = ((n % 26) + 26) % 26;
        letras[cursor] = (char)('A' + n);
        sujo = true;
        SomLetra();
    }

    void MoverCursor(int direcao)
    {
        cursor = Mathf.Clamp(cursor + direcao, 0, TamanhoNome - 1);
        sujo = true;
    }

    void SomLetra()
    {
        AudioManager am = AudioManager.Instance;
        if (am != null) am.PlayHover();
    }

    // ------------------------------------------------------------------ ranking (dados)
    int Posicao(int pontos)
    {
        if (pontos <= 0) return -1;

        for (int i = 0; i < lista.Count; i++)
            if (pontos > lista[i].pontos) return i;

        return lista.Count < maxEntradas ? lista.Count : -1;
    }

    void Carregar()
    {
        lista.Clear();

        if (PlayerPrefs.HasKey(ChaveRanking))
        {
            string s = PlayerPrefs.GetString(ChaveRanking, "");

            foreach (string item in s.Split('|'))
            {
                int v = item.IndexOf(',');
                if (v <= 0) continue;

                int pts;
                if (!int.TryParse(item.Substring(v + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out pts))
                    continue;

                lista.Add(new Entrada { nome = NomeValido(item.Substring(0, v)), pontos = pts });
            }
        }
        else if (semearRanking)
        {
            Semear();
            Salvar();
        }

        lista.Sort((a, b) => b.pontos.CompareTo(a.pontos));
        if (lista.Count > maxEntradas) lista.RemoveRange(maxEntradas, lista.Count - maxEntradas);
    }

    void Semear()
    {
        lista.Add(new Entrada { nome = "NVS", pontos = 2500 });
        lista.Add(new Entrada { nome = "KVX", pontos = 1800 });
        lista.Add(new Entrada { nome = "CPU", pontos = 1200 });
        lista.Add(new Entrada { nome = "BOT", pontos = 800 });
        lista.Add(new Entrada { nome = "DEV", pontos = 400 });
    }

    void Salvar()
    {
        var sb = new StringBuilder();

        for (int i = 0; i < lista.Count; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(lista[i].nome).Append(',').Append(lista[i].pontos.ToString(CultureInfo.InvariantCulture));
        }

        PlayerPrefs.SetString(ChaveRanking, sb.ToString());
        PlayerPrefs.Save();
    }

    void LimparRanking()
    {
        PlayerPrefs.DeleteKey(ChaveRanking);
        PlayerPrefs.Save();
        Carregar();
        ultimoIndice = -1;
        sujo = true;
    }

    [ContextMenu("Limpar ranking")]
    void LimparRankingMenu()
    {
        if (Application.isPlaying) LimparRanking();
        else
        {
            PlayerPrefs.DeleteKey(ChaveRanking);
            PlayerPrefs.Save();
        }
    }

    static string NomeValido(string s)
    {
        var sb = new StringBuilder();

        if (s != null)
        {
            foreach (char c in s.ToUpperInvariant())
                if (c >= 'A' && c <= 'Z' && sb.Length < TamanhoNome) sb.Append(c);
        }

        while (sb.Length < TamanhoNome) sb.Append('A');
        return sb.ToString();
    }

    // ------------------------------------------------------------------ desenho
    void Redesenhar()
    {
        sujo = false;

        bool editando = estado == Estado.Editando;
        bool aceso = acesoAnterior;

        for (int i = 0; i < linhas.Length; i++)
        {
            Linha l = linhas[i];
            Color cor = CorDoRank(i);

            l.rank.text = (i + 1).ToString(CultureInfo.InvariantCulture) + ".";
            l.rank.color = cor;

            if (i < lista.Count)
            {
                bool essa = editando && i == posEdicao;

                l.nome.text = essa ? NomeEditando(aceso) : lista[i].nome;
                l.nome.color = essa ? Color.white : cor;

                l.pontos.text = lista[i].pontos.ToString(CultureInfo.InvariantCulture);
                l.pontos.color = cor;

                if (essa)
                    l.fundo.color = new Color(corBotaoBordaSel.r, corBotaoBordaSel.g, corBotaoBordaSel.b, aceso ? 0.30f : 0.14f);
                else if (i == ultimoIndice)
                    l.fundo.color = new Color(corBotaoBordaSel.r, corBotaoBordaSel.g, corBotaoBordaSel.b, 0.22f);
                else
                    l.fundo.color = Color.clear;
            }
            else
            {
                l.nome.text = "---";
                l.nome.color = new Color(1f, 1f, 1f, 0.25f);
                l.pontos.text = "";
                l.fundo.color = Color.clear;
            }
        }

        if (areaEntrada.gameObject.activeSelf != editando)
            areaEntrada.gameObject.SetActive(editando);

        txtOcioso.gameObject.SetActive(!editando);

        for (int i = 0; i < TamanhoNome; i++)
        {
            bool sel = editando && i == cursor;
            celBorda[i].color = sel ? corBotaoBordaSel : corBotaoBorda;
            celFundo[i].color = sel ? corBotaoFundoSel : corBotaoFundo;
            celTxt[i].text = letras[i].ToString();
            celTxt[i].color = sel ? corDestaque : Color.white;
        }
    }

    string NomeEditando(bool aceso)
    {
        var sb = new StringBuilder();

        for (int k = 0; k < TamanhoNome; k++)
        {
            if (k == cursor && aceso)
                sb.Append("<color=#").Append(corDestaqueHex).Append('>').Append(letras[k]).Append("</color>");
            else if (k == cursor)
                sb.Append("<color=#").Append(corDestaqueHex).Append("66>").Append(letras[k]).Append("</color>");
            else
                sb.Append(letras[k]);
        }

        return sb.ToString();
    }

    Color CorDoRank(int i)
    {
        if (i == 0) return corDestaque;                              // ouro
        if (i == 1) return new Color(0.82f, 0.85f, 0.92f);           // prata
        if (i == 2) return new Color(0.88f, 0.58f, 0.32f);           // bronze
        return corTextoSuave;
    }

    // ------------------------------------------------------------------ construcao da UI
    void AcharFonte()
    {
        if (fontePixel != null) return;

        GameOverUI go = FindFirstObjectByType<GameOverUI>();
        if (go != null) fontePixel = go.fontePixel != null ? go.fontePixel : go.font;

        if (fontePixel == null)
        {
            GameHUD hud = FindFirstObjectByType<GameHUD>();
            if (hud != null) fontePixel = hud.font;
        }
    }

    void Build()
    {
        var cgo = new GameObject("LeaderboardCanvas", typeof(RectTransform));
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
        grupo.alpha = 0f;
        grupo.interactable = false;
        grupo.blocksRaycasts = false;
        canvas.enabled = false;

        var root = (RectTransform)cgo.transform;

        float areaTop = TopoLinhas + maxEntradas * AlturaLinha + 10f;
        float altura = areaTop + 190f;

        // Painel na lateral esquerda: borda roxa + miolo escuro.
        RectTransform painel = NovoRect("Painel", root);
        painel.anchorMin = painel.anchorMax = new Vector2(0f, 0.5f);
        painel.pivot = new Vector2(0f, 0.5f);
        painel.anchoredPosition = new Vector2(margemLateral, -10f);
        painel.sizeDelta = new Vector2(larguraPainel, altura);

        Image borda = NovaImagem(painel, "Borda", corBorda);
        Inset(borda.rectTransform, 0f);

        Image fundo = NovaImagem(painel, "Fundo", corFundo);
        Inset(fundo.rectTransform, pixel * 2f);
        RectTransform miolo = fundo.rectTransform;

        // Titulo.
        TextMeshProUGUI titulo = MakeText(miolo, "Titulo", "TOP " + maxEntradas.ToString(CultureInfo.InvariantCulture),
            54f, Color.white, TextAlignmentOptions.Center, true);
        PlaceTop(titulo.rectTransform, 16f, 64f, 16f);

        // Linhas do ranking.
        linhas = new Linha[maxEntradas];

        for (int i = 0; i < maxEntradas; i++)
        {
            RectTransform r = NovoRect("Linha" + i, miolo);
            PlaceTop(r, TopoLinhas + i * AlturaLinha, AlturaLinha - 4f, 14f);

            var l = new Linha();

            l.fundo = NovaImagem(r, "Fundo", Color.clear);
            Inset(l.fundo.rectTransform, 0f);

            l.rank = MakeText(r, "Rank", "", 30f, corTextoSuave, TextAlignmentOptions.MidlineLeft, false);
            Place(l.rank.rectTransform, new Vector2(0.03f, 0f), new Vector2(0.22f, 1f), Vector2.zero, Vector2.zero);

            l.nome = MakeText(r, "Nome", "", 30f, corTextoSuave, TextAlignmentOptions.MidlineLeft, false);
            Place(l.nome.rectTransform, new Vector2(0.24f, 0f), new Vector2(0.58f, 1f), Vector2.zero, Vector2.zero);

            l.pontos = MakeText(r, "Pontos", "", 30f, corTextoSuave, TextAlignmentOptions.MidlineRight, false);
            Place(l.pontos.rectTransform, new Vector2(0.58f, 0f), new Vector2(0.97f, 1f), Vector2.zero, Vector2.zero);

            linhas[i] = l;
        }

        // Area de digitacao das iniciais (so aparece quando o jogador entrou no Top).
        areaEntrada = NovoRect("Entrada", miolo);
        PlaceTop(areaEntrada, areaTop, 160f, 14f);

        TextMeshProUGUI rotulo = MakeText(areaEntrada, "Rotulo", "SUAS INICIAIS", 26f, corTextoSuave,
            TextAlignmentOptions.Center, false);
        PlaceTop(rotulo.rectTransform, 0f, 34f, 0f);

        for (int i = 0; i < TamanhoNome; i++)
        {
            int idx = i;
            NovoBotao(areaEntrada, "Letra" + i, -144f + 84f * i, 40f, 70f, 70f, "A", 48f,
                () => { cursor = idx; CiclarLetra(1); },
                out celBorda[i], out celFundo[i], out celTxt[i], false);
        }

        Image okBorda, okFundo;
        TextMeshProUGUI okTxt;
        NovoBotao(areaEntrada, "Ok", 129f, 40f, 100f, 70f, "OK", 36f, Confirmar,
            out okBorda, out okFundo, out okTxt, true);
        okBorda.color = corBotaoBordaSel;
        okFundo.color = corBotaoFundoSel;
        okTxt.color = corDestaque;

        TextMeshProUGUI dica = MakeText(areaEntrada, "Dica", "DIGITE / SETAS  -  ENTER = OK", 22f, corTextoSuave,
            TextAlignmentOptions.Center, false);
        PlaceTop(dica.rectTransform, 124f, 34f, 0f);

        areaEntrada.gameObject.SetActive(false);

        // Texto de rodape quando nao esta digitando.
        txtOcioso = MakeText(miolo, "Ocioso", "SUPERE O TOP " + maxEntradas.ToString(CultureInfo.InvariantCulture) + "!", 30f,
            corDestaque, TextAlignmentOptions.Center, false);
        PlaceTop(txtOcioso.rectTransform, areaTop + 30f, 60f, 16f);
    }

    void NovoBotao(RectTransform pai, string nome, float x, float top, float w, float h, string texto, float tamanho,
        UnityAction aoClicar, out Image borda, out Image fundo, out TextMeshProUGUI txt, bool comSom)
    {
        RectTransform rt = NovoRect(nome, pai);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(x, -top);

        borda = rt.gameObject.AddComponent<Image>();
        borda.color = corBotaoBorda;
        borda.raycastTarget = true;

        fundo = NovaImagem(rt, "Fundo", corBotaoFundo);
        Inset(fundo.rectTransform, pixel);

        txt = MakeText(fundo.rectTransform, "Texto", texto, tamanho, Color.white, TextAlignmentOptions.Center, false);
        Inset(txt.rectTransform, 2f);

        var botao = rt.gameObject.AddComponent<Button>();
        botao.transition = Selectable.Transition.None;
        botao.targetGraphic = borda;
        botao.onClick.AddListener(aoClicar);

        if (comSom) rt.gameObject.AddComponent<ButtonSfx>();
    }

    // ------------------------------------------------------------------ helpers de UI
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

    TextMeshProUGUI MakeText(RectTransform parent, string nome, string texto, float tamanho, Color cor,
        TextAlignmentOptions alinhamento, bool comContorno)
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
        t.richText = true;

        t.enableAutoSizing = true;
        t.fontSizeMax = tamanho;
        t.fontSizeMin = tamanho * 0.4f;
        t.fontSize = tamanho;

        if (fontePixel != null) t.font = fontePixel;
        else t.fontStyle = FontStyles.Bold;

        // Contorno so no titulo (cada texto com contorno cria um material proprio).
        if (comContorno && contornoTexto > 0f)
        {
            t.outlineWidth = contornoTexto;
            t.outlineColor = Color.black;
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

    /// <summary>De cima para baixo: "top" px do topo, "altura" px de altura, margens laterais iguais.</summary>
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
}