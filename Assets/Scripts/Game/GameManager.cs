using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuração")]
    [Tooltip("Objeto pai que contém todos os blocos da fase")]
    public Transform blocksParent;

    [Header("Cores (legibilidade)")]
    [Tooltip("Saturação mínima das cores do tabuleiro. Mais alto = cores mais vivas, nada acinzentado.")]
    [Range(0f, 1f)]
    public float saturacaoMinima = 0.65f;

    [Tooltip("Brilho mínimo das cores do tabuleiro.")]
    [Range(0f, 1f)]
    public float brilhoMinimo = 0.85f;

    [Tooltip("Distância mínima entre a cor-alvo e os blocos ERRADOS na 1ª rodada (0 a 1). Mais alto = mais fácil.")]
    [Range(0f, 1f)]
    public float distanciaMinInicial = 0.30f;

    [Tooltip("Distância mínima nas rodadas avançadas. Deixe igual à inicial para a dificuldade vir só do tempo.")]
    [Range(0f, 1f)]
    public float distanciaMinFinal = 0.12f;

    [Tooltip("Em quantas rodadas a distância vai da inicial até a final.")]
    public int rodadasAteDistanciaFinal = 12;

    [Header("Detecção do player")]
    [Tooltip("Player usado para descobrir em que bloco ele está. Se vazio, procura pela tag Player.")]
    public Transform playerTransform;

    [Tooltip("Altura (acima do centro do player) de onde o raio parte para baixo.")]
    public float alturaRaio = 1f;

    [Header("Dificuldade")]
    public float tempoInicial = 3f;
    public float tempoMinimo = 0.6f;

    [Range(0.5f, 0.99f)]
    public float reducaoPorRodada = 0.95f;

    [Tooltip("Velocidade da música na dificuldade máxima (1 = normal). O AudioManager limita em 1.25.")]
    [Range(1f, 1.25f)]
    public float velocidadeMusicaMax = 1.2f;

    [Header("Entrada calma (sem susto)")]
    [Tooltip("Preparo antes da primeira rodada, em segundos. O jogador já pode andar.")]
    public float tempoPreparo = 3f;

    [Tooltip("Preparo depois do TryAgain, em segundos.")]
    public float tempoPreparoRetry = 1.5f;

    [Tooltip("Cor do fundo do jogo (neutra e escura). A cor-alvo NÃO usa mais o fundo: ela aparece no HUD.")]
    public Color corFundo = new Color(0.10f, 0.10f, 0.12f);

    [Tooltip("Tempo para o fundo mudar de cor ao entrar no preparo, em segundos.")]
    public float duracaoTransicaoFundo = 0.25f;

    [Header("Feedback visual")]
    [Tooltip("Cor dos blocos entre as rodadas e no começo da onda. Um roxo escuro combina com o HUD e evita o clarão do branco puro.")]
    public Color corNeutraBlocos = new Color(0.13f, 0.09f, 0.24f);

    public Color corVitoria = new Color(0.24f, 0.86f, 0.52f);

    [Tooltip("Duração do pulso suave de vitória, em segundos.")]
    public float duracaoPulsoVitoria = 0.5f;

    [Range(0f, 1f)]
    [Tooltip("Quanto o pulso chega perto da cor de vitória (0 = nada, 1 = cor cheia).")]
    public float forcaPulsoVitoria = 0.55f;

    [Tooltip("Pausa depois da vitória antes da próxima rodada")]
    public float pausaAposVitoria = 0.5f;

    [Header("Revelação da derrota")]
    [Tooltip("Cor para onde os blocos errados vão 'apagando'.")]
    public Color corBlocosApagados = new Color(0.25f, 0.25f, 0.30f);

    [Tooltip("Tempo do apagar suave dos blocos errados, em segundos.")]
    public float duracaoRevelacao = 0.6f;

    [Tooltip("Espera antes de mostrar o painel de Game Over, para o jogador ver o bloco certo.")]
    public float atrasoGameOver = 0.9f;

    [Header("Pontuação")]
    [Tooltip("Pontos por rodada vencida.")]
    public int pontosBase = 100;

    [Tooltip("Bônus máximo por chegar cedo ao bloco certo e ficar nele (proporcional ao tempo que sobrava).")]
    public int bonusMaximoVelocidade = 100;

    [Tooltip("Chegar ao bloco certo com pelo menos esta fração do tempo sobrando mantém o combo (0 a 1).")]
    [Range(0f, 1f)]
    public float limiarCombo = 0.5f;

    [Tooltip("Quanto cada rodada de combo soma ao multiplicador (0,25 = x1.25, x1.5, x1.75...).")]
    public float passoMultiplicador = 0.25f;

    [Tooltip("Multiplicador máximo de pontos.")]
    public float multiplicadorMaximo = 3f;

    [Header("Vida no tabuleiro")]
    [Tooltip("Duração máxima da onda que acende os tiles no começo de cada rodada. 0 = desliga (as cores aparecem de uma vez).")]
    public float duracaoOndaMax = 0.40f;

    [Tooltip("Limite da onda como fração da rodada, para ela nunca roubar muito tempo em rodadas curtas.")]
    [Range(0f, 0.5f)]
    public float fracaoOndaDaRodada = 0.15f;

    [Tooltip("Tique discreto, na batida da música, nos últimos segundos da rodada.")]
    public bool tiqueTaque = true;

    [Tooltip("Quantos segundos finais da rodada têm o tique.")]
    public float segundosDeTique = 3f;

    [Header("Eventos")]
    public UnityEvent onRoundWon;
    public UnityEvent onRoundLost;
    public UnityEvent onGameOver;

    private readonly List<GridBlock> blocks = new List<GridBlock>();

    // Blocos atualmente tocados pelo Player.
    // Atualizado pelos triggers SEMPRE (inclusive entre rodadas): o trigger só
    // dispara ao ENTRAR, então quem ficou parado num bloco não pode ser "esquecido".
    private readonly List<GridBlock> currentBlocks = new List<GridBlock>();

    private GridBlock blocoCorreto;

    private Color targetColor;
    private float tempoAtual;

    private Coroutine preparoCoroutine;
    private Coroutine timerCoroutine;
    private Coroutine derrotaCoroutine;
    private Coroutine fundoCoroutine;
    private Coroutine ondaCoroutine;

    private Vector3 centroTabuleiro;

    private bool roundActive;
    private int rodada;

    private float inicioRodada;
    private int rodadaUid;
    private bool fimDeJogo;

    // --- pontuação ---
    private const string ChaveRecorde = "chromablitz_recorde";
    private int pontuacao;
    private int recordeSalvo;
    private int combo;
    private float multiplicador = 1f;
    private int ultimoGanho;
    private int ganhoUid;
    private bool novoRecorde;
    private float chegada01;   // fração do tempo que sobrava quando o player pisou no bloco certo (e ficou)

    private bool emPreparo;
    private float preparoFim;
    private float preparoTotal;

    // =========================================================
    // LEITURA PARA O HUD (somente leitura)
    // =========================================================

    /// <summary>Número da rodada atual (0 durante o preparo, depois 1, 2, 3...).</summary>
    public int Rodada => rodada;

    // --- pontuação (lida pelo HUD) ---
    public int Pontuacao => pontuacao;
    public int Recorde => recordeSalvo;
    public int Combo => combo;
    public float Multiplicador => multiplicador;
    public int UltimoGanho => ultimoGanho;
    public int GanhoUid => ganhoUid;
    public bool NovoRecorde => novoRecorde;

    /// <summary>Contador que sobe a cada rodada/preparo e nunca reseta (detecta "algo novo").</summary>
    public int RodadaUid => rodadaUid;

    public bool RodadaAtiva => roundActive;

    /// <summary>Cor que o jogador precisa encontrar (mostrada no HUD, não no fundo).</summary>
    public Color CorAlvo => targetColor;

    /// <summary>False no preparo antes da 1ª rodada (ainda não existe cor-alvo).</summary>
    public bool TemCorAlvo => rodada >= 1;

    /// <summary>True depois que o jogador errou, até o TryAgain.</summary>
    public bool FimDeJogo => fimDeJogo;

    /// <summary>True enquanto o jogo "respira" antes de valer (contagem inicial).</summary>
    public bool EmPreparo => emPreparo;

    public float PreparoRestante =>
        emPreparo ? Mathf.Max(0f, preparoFim - Time.time) : 0f;

    /// <summary>1 = preparo acabou de começar, 0 = prestes a valer.</summary>
    public float Preparo01 =>
        preparoTotal > 0f ? Mathf.Clamp01(PreparoRestante / preparoTotal) : 0f;

    /// <summary>Duração total da rodada atual, em segundos.</summary>
    public float TempoDaRodada => tempoAtual;

    public float TempoRestante =>
        roundActive
            ? Mathf.Max(0f, tempoAtual - (Time.time - inicioRodada))
            : 0f;

    /// <summary>1 = rodada acabou de começar, 0 = tempo esgotado.</summary>
    public float Progresso01 =>
        tempoAtual > 0f
            ? Mathf.Clamp01(TempoRestante / tempoAtual)
            : 0f;

    // =========================================================
    // UNITY
    // =========================================================

    private void Awake()
    {
        // Evita dois GameManagers na mesma cena.
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "GameManager: já existe outro GameManager (" + Instance.gameObject.name + "). " +
                "Este (" + gameObject.name + ") será removido. Deixe só um na cena.",
                this
            );

            // Desliga antes de destruir: assim o Start deste duplicado não chega a rodar.
            enabled = false;

            // Se o duplicado é só outro componente no MESMO objeto, destrói só o componente
            // (destruir o objeto levaria o GameManager verdadeiro junto).
            if (Instance.gameObject == gameObject)
                Destroy(this);
            else
                Destroy(gameObject);

            return;
        }

        Instance = this;

        if (blocksParent == null)
        {
            Debug.LogError(
                "GameManager: blocksParent não foi definido no Inspector."
            );

            enabled = false;
            return;
        }

        blocks.AddRange(
            blocksParent.GetComponentsInChildren<GridBlock>()
        );

        if (blocks.Count == 0)
        {
            Debug.LogError(
                "GameManager: nenhum GridBlock foi encontrado em blocksParent."
            );

            enabled = false;
            return;
        }

        tempoAtual = tempoInicial;

        // Centro do tabuleiro: de onde a onda de revelação parte.
        Vector3 soma = Vector3.zero;
        foreach (GridBlock b in blocks)
            soma += b.transform.position;
        centroTabuleiro = soma / blocks.Count;
    }

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject p = GameObject.FindWithTag("Player");

            if (p != null)
            {
                playerTransform = p.transform;
            }
            else
            {
                Debug.LogWarning(
                    "GameManager: Player não encontrado (tag Player). " +
                    "Usando apenas os triggers para detectar o bloco."
                );
            }
        }

        // Fundo neutro e fixo durante todo o jogo (a cor-alvo vive no HUD).
        if (Camera.main == null)
        {
            Debug.LogError(
                "GameManager: nenhuma câmera com a tag MainCamera foi encontrada."
            );
        }
        else
        {
            MudarFundo(corFundo, 0f);
        }

        // Em vez de valer já no primeiro frame, o jogo começa com um preparo calmo.
        recordeSalvo = PlayerPrefs.GetInt(ChaveRecorde, 0);

        // Tique na batida da música (o AudioManager vive entre cenas: desinscreve no OnDestroy).
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnBeat += AoBater;

        IniciarPreparo(tempoPreparo);
    }

    private void OnDestroy()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.OnBeat -= AoBater;
    }

    // =========================================================
    // PREPARO (contagem antes de valer)
    // =========================================================

    private void IniciarPreparo(float duracao)
    {
        PararOnda();

        if (preparoCoroutine != null)
        {
            StopCoroutine(preparoCoroutine);
        }

        preparoCoroutine = StartCoroutine(PreparoRoutine(duracao));
    }

    private IEnumerator PreparoRoutine(float duracao)
    {
        emPreparo = true;
        fimDeJogo = false;
        roundActive = false;
        rodadaUid++;

        preparoTotal = Mathf.Max(0.01f, duracao);
        preparoFim = Time.time + preparoTotal;

        // Tabuleiro neutro e fundo calmo: nenhuma cor aleatória até valer.
        foreach (GridBlock block in blocks)
        {
            block.SetColor(corNeutraBlocos);
        }

        MudarFundo(corFundo, duracaoTransicaoFundo);

        yield return new WaitForSeconds(preparoTotal);

        emPreparo = false;
        NovaRodada();
    }

    // =========================================================
    // RODADA
    // =========================================================

    // Privado de propósito: só o fluxo do próprio GameManager (preparo e vitória) começa rodadas.
    // Se algo de fora (um UnityEvent no Inspector, por exemplo) chamasse isto, a rodada contava em dobro.
    private void NovaRodada()
    {
        // Guarda: nunca começa uma rodada por cima de outra que ainda está valendo.
        if (roundActive)
        {
            Debug.LogWarning(
                $"GameManager: NovaRodada chamada com a rodada {rodada} ainda ativa. Chamada ignorada. " +
                "Clique neste aviso para ver no stack trace quem chamou.",
                this
            );
            return;
        }

        rodada++;
        rodadaUid++;
        fimDeJogo = false;
        emPreparo = false;
        roundActive = true;

        // Sorteia a cor que será procurada (sempre viva e saturada).
        targetColor =
            ColorUtils.RandomVibrant(saturacaoMinima, brilhoMinimo);

        // A cor-alvo aparece no HUD (CorAlvo); o fundo permanece neutro.

        // Sorteia as cores dos blocos errados: vivas e sempre distintas da cor-alvo,
        // para ninguém perder por causa de dois tons quase iguais.
        float distanciaMin = DistanciaMinimaDaRodada();

        foreach (GridBlock block in blocks)
        {
            block.SetColor(
                ColorUtils.RandomVibrantAwayFrom(
                    targetColor,
                    distanciaMin,
                    saturacaoMinima,
                    brilhoMinimo
                )
            );
        }

        // Garante que exista pelo menos um bloco correto.
        int indiceCorreto = Random.Range(0, blocks.Count);

        blocoCorreto = blocks[indiceCorreto];
        blocoCorreto.SetColor(targetColor);

        // Se o player já está em cima do bloco certo, conta como chegada no tempo máximo.
        chegada01 = currentBlocks.Contains(blocoCorreto) ? 1f : 0f;

        // Em vez de as cores "estourarem" de uma vez, elas acendem em onda a partir do centro.
        IniciarOnda();

        Debug.Log(
            $"Rodada {rodada} iniciada | " +
            $"Tempo: {tempoAtual:F2}s | " +
            $"Distância mínima de cor: {distanciaMin:F2} | " +
            $"Bloco correto: {blocoCorreto.name}"
        );

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
        }

        inicioRodada = Time.time;

        timerCoroutine = StartCoroutine(TimerRodada());
    }

    /// <summary>
    /// Quão diferente os blocos errados precisam ser da cor-alvo nesta rodada.
    /// Começa fácil (cores bem distintas) e vai apertando até distanciaMinFinal.
    /// </summary>
    private float DistanciaMinimaDaRodada()
    {
        float t = Mathf.Clamp01(
            (rodada - 1) / (float)Mathf.Max(1, rodadasAteDistanciaFinal)
        );

        return Mathf.Lerp(distanciaMinInicial, distanciaMinFinal, t);
    }

    // =========================================================
    // ONDA DE REVELAÇÃO (só visual: não mexe em colisores nem em BlockColor)
    // =========================================================

    private void IniciarOnda()
    {
        PararOnda();

        float duracao = Mathf.Min(duracaoOndaMax, tempoAtual * fracaoOndaDaRodada);

        if (duracao <= 0.02f)
            return;

        ondaCoroutine = StartCoroutine(OndaRevelacao(duracao));
    }

    private void PararOnda()
    {
        if (ondaCoroutine != null)
        {
            StopCoroutine(ondaCoroutine);
            ondaCoroutine = null;
        }
    }

    private IEnumerator OndaRevelacao(float duracao)
    {
        int n = blocks.Count;
        float[] atraso = new float[n];
        float maisLonge = 0.0001f;

        for (int i = 0; i < n; i++)
        {
            Vector3 d = blocks[i].transform.position - centroTabuleiro;
            d.y = 0f;
            atraso[i] = d.magnitude;
            maisLonge = Mathf.Max(maisLonge, atraso[i]);
        }

        float fade = duracao * 0.45f;          // tempo que cada tile leva para acender
        float espalhar = duracao - fade;       // tempo para a onda chegar até a borda

        for (int i = 0; i < n; i++)
        {
            atraso[i] = atraso[i] / maisLonge * espalhar;
            blocks[i].SetVisualColor(corNeutraBlocos);   // começa neutro, no mesmo frame da troca de cores
        }

        float t = 0f;

        while (t < duracao)
        {
            t += Time.deltaTime;

            for (int i = 0; i < n; i++)
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - atraso[i]) / fade));

                blocks[i].SetVisualColor(
                    Color.Lerp(corNeutraBlocos, blocks[i].BlockColor, k)
                );
            }

            yield return null;
        }

        for (int i = 0; i < n; i++)
            blocks[i].SetVisualColor(blocks[i].BlockColor);

        ondaCoroutine = null;
    }

    // =========================================================
    // TIQUE NOS ÚLTIMOS SEGUNDOS (na batida da música, nada de susto)
    // =========================================================

    private void AoBater(int beat)
    {
        if (!tiqueTaque || !roundActive || AudioManager.Instance == null)
            return;

        float restante = TempoRestante;

        // Só nos últimos segundos, nunca logo no começo da rodada nem no instante final.
        if (restante > segundosDeTique || restante < 0.15f)
            return;

        if (tempoAtual - restante < 0.4f)
            return;

        AudioManager.Instance.PlayTick();
    }

    /// <summary>
    /// A música acelera junto com a dificuldade (o tempo da rodada encurtando),
    /// com o efeito de "fita acelerando" do AudioManager.
    /// </summary>
    private void AtualizarVelocidadeMusica()
    {
        AudioManager am = AudioManager.Instance;
        if (am == null) return;

        float faixa = Mathf.Max(0.01f, tempoInicial - tempoMinimo);
        float dificuldade01 = Mathf.Clamp01((tempoInicial - tempoAtual) / faixa);

        am.SetMusicSpeed(1f + (velocidadeMusicaMax - 1f) * dificuldade01, 1.5f);
    }

    private IEnumerator TimerRodada()
    {
        yield return new WaitForSeconds(tempoAtual);

        if (!roundActive)
            yield break;

        // Verifica o bloco sob o player somente quando o tempo acaba.
        GridBlock blocoDoPlayer = BlocoSobPlayer();

        bool acertou =
            blocoDoPlayer != null &&
            ColorUtils.CoresIguais(
                blocoDoPlayer.BlockColor,
                targetColor
            );

        Debug.Log(
            $"Fim da rodada {rodada} | " +
            $"Bloco do player: {(blocoDoPlayer != null ? blocoDoPlayer.name : "nenhum")} | " +
            $"Acertou: {acertou}"
        );

        if (acertou)
        {
            // Revelação sonora somente após o fim da rodada.
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayCorrect();

                // A nota correspondente à cor pode ser tocada aqui
                // quando tivermos a associação entre cor e índice.
            }

            yield return StartCoroutine(VitoriaRoutine());
        }
        else
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayError();
            }

            yield return StartCoroutine(DerrotaRoutine());
        }
    }

    // =========================================================
    // FUNDO (transição suave)
    // =========================================================

    private void MudarFundo(Color alvo, float duracao)
    {
        if (fundoCoroutine != null)
        {
            StopCoroutine(fundoCoroutine);
            fundoCoroutine = null;
        }

        Camera cam = Camera.main;

        if (cam == null)
            return;

        if (duracao <= 0.001f)
        {
            cam.backgroundColor = alvo;
            return;
        }

        fundoCoroutine = StartCoroutine(TransicaoFundo(cam, alvo, duracao));
    }

    private IEnumerator TransicaoFundo(Camera cam, Color alvo, float duracao)
    {
        Color origem = cam.backgroundColor;
        float t = 0f;

        while (t < duracao)
        {
            t += Time.deltaTime;

            cam.backgroundColor = Color.Lerp(
                origem,
                alvo,
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracao))
            );

            yield return null;
        }

        cam.backgroundColor = alvo;
    }

    // =========================================================
    // QUAL BLOCO ESTÁ SOB O PLAYER?
    // =========================================================

    /// <summary>
    /// Descobre o bloco sob o CENTRO do player.
    /// 1) Raio para baixo a partir do player (não depende de ter "entrado" no bloco).
    /// 2) Se o raio não achar nada, usa os blocos registrados pelos triggers
    ///    (o mais próximo do player; ou o último registrado se não houver referência).
    /// </summary>
    private GridBlock BlocoSobPlayer()
    {
        if (playerTransform != null)
        {
            Vector3 origem =
                playerTransform.position + Vector3.up * alturaRaio;

            RaycastHit[] hits = Physics.RaycastAll(
                origem,
                Vector3.down,
                alturaRaio + 5f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide
            );

            GridBlock melhor = null;
            float menorDistancia = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                GridBlock gb =
                    hits[i].collider.GetComponentInParent<GridBlock>();

                if (gb != null && hits[i].distance < menorDistancia)
                {
                    menorDistancia = hits[i].distance;
                    melhor = gb;
                }
            }

            if (melhor != null)
                return melhor;
        }

        // Fallback: blocos registrados pelos triggers.
        if (currentBlocks.Count == 0)
            return null;

        if (playerTransform == null)
            return currentBlocks[currentBlocks.Count - 1];

        GridBlock maisPerto = null;
        float menorSqr = float.MaxValue;

        foreach (GridBlock b in currentBlocks)
        {
            Vector3 d = b.transform.position - playerTransform.position;
            d.y = 0f;

            if (d.sqrMagnitude < menorSqr)
            {
                menorSqr = d.sqrMagnitude;
                maisPerto = b;
            }
        }

        return maisPerto;
    }

    // =========================================================
    // PLAYER ENTROU
    // =========================================================

    public void OnPlayerEnteredBlock(GridBlock block)
    {
        if (block == null)
            return;

        // Registra SEMPRE, mesmo fora de rodada (preparo, pausa pós-vitória, game over...).
        // Quem avalia o resultado é o fim do timer, não esta função.
        if (!currentBlocks.Contains(block))
        {
            currentBlocks.Add(block);
        }

        // Pisou no bloco certo durante a rodada: guarda quanto tempo ainda sobrava (bônus de velocidade).
        if (roundActive && block == blocoCorreto)
            chegada01 = Mathf.Clamp01(TempoRestante / Mathf.Max(0.01f, tempoAtual));

        Debug.Log(
            $"Entrou em {block.name} | " +
            $"Cor: {block.BlockColor}"
        );

        // IMPORTANTE:
        // Nenhum áudio é reproduzido aqui.
        // O jogador precisa descobrir a cor visualmente.
    }

    // =========================================================
    // PLAYER SAIU
    // =========================================================

    public void OnPlayerExitedBlock(GridBlock block)
    {
        if (block == null)
            return;

        currentBlocks.Remove(block);

        // Saiu do bloco certo: perde o bônus de chegada (vale a última vez que ele pisou e ficou).
        if (block == blocoCorreto)
            chegada01 = 0f;
    }

    // =========================================================
    // VITÓRIA
    // =========================================================

    /// <summary>
    /// Pontos da rodada = (base + bônus de velocidade) x multiplicador de combo.
    /// Combo sobe quando o player chega cedo ao bloco certo; chegar tarde zera o combo
    /// (mas nunca encerra o jogo).
    /// </summary>
    private void RegistrarPontos()
    {
        float rapidez = Mathf.Clamp01(chegada01);
        int bonus = Mathf.RoundToInt(bonusMaximoVelocidade * rapidez);

        if (rapidez >= limiarCombo) combo++;
        else combo = 0;

        multiplicador = Mathf.Min(1f + combo * passoMultiplicador, multiplicadorMaximo);

        ultimoGanho = Mathf.RoundToInt((pontosBase + bonus) * multiplicador);
        pontuacao += ultimoGanho;
        ganhoUid++;

        if (pontuacao > recordeSalvo)
        {
            // "Novo recorde" só aparece se já existia um recorde para bater.
            if (recordeSalvo > 0) novoRecorde = true;

            recordeSalvo = pontuacao;
            PlayerPrefs.SetInt(ChaveRecorde, recordeSalvo);
        }

        Debug.Log(
            $"Pontos: +{ultimoGanho} (base {pontosBase} + velocidade {bonus}) x{multiplicador:0.##} | " +
            $"Total: {pontuacao} | Recorde: {recordeSalvo}"
        );
    }

    private IEnumerator VitoriaRoutine()
    {
        roundActive = false;

        RegistrarPontos();

        // Um único pulso suave (sobe e desce), em vez de piscadas secas.
        yield return StartCoroutine(
            PulsoBlocos(corVitoria, duracaoPulsoVitoria, forcaPulsoVitoria)
        );

        foreach (GridBlock block in blocks)
        {
            block.SetColor(corNeutraBlocos);
        }

        onRoundWon?.Invoke();

        // Aumenta a dificuldade.
        tempoAtual = Mathf.Max(
            tempoMinimo,
            tempoAtual * reducaoPorRodada
        );

        AtualizarVelocidadeMusica();

        yield return new WaitForSeconds(pausaAposVitoria);

        NovaRodada();
    }

    /// <summary>
    /// Mistura os blocos com uma cor e volta, em curva de seno (0 -> 1 -> 0).
    /// Nunca chega na cor cheia (limitado por "forca") e nunca corta de uma vez.
    /// </summary>
    private IEnumerator PulsoBlocos(Color cor, float duracao, float forca)
    {
        if (duracao > 0.001f)
        {
            float t = 0f;

            while (t < duracao)
            {
                t += Time.deltaTime;

                float sino = Mathf.Sin(Mathf.Clamp01(t / duracao) * Mathf.PI);

                foreach (GridBlock block in blocks)
                {
                    block.SetVisualColor(
                        Color.Lerp(block.BlockColor, cor, forca * sino)
                    );
                }

                yield return null;
            }
        }

        foreach (GridBlock block in blocks)
        {
            block.SetVisualColor(block.BlockColor);
        }
    }

    // =========================================================
    // DERROTA
    // =========================================================

    private IEnumerator DerrotaRoutine()
    {
        roundActive = false;
        fimDeJogo = true;

        // Grava o recorde no fim da partida (no WebGL vai para o armazenamento do navegador).
        PlayerPrefs.Save();

        // A música abafa (estado GameOver) e volta à velocidade normal.
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetState(AudioManager.MusicState.GameOver, 1f);
            AudioManager.Instance.SetMusicSpeed(1f, 1.5f);
        }

        onRoundLost?.Invoke();

        // Sem piscar: a tela "apaga" e mostra qual era o bloco certo.
        derrotaCoroutine = StartCoroutine(RevelarDerrota());

        yield break;
    }

    private IEnumerator RevelarDerrota()
    {
        float duracao = Mathf.Max(0.01f, duracaoRevelacao);
        float t = 0f;

        while (t < duracao)
        {
            t += Time.deltaTime;

            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duracao));

            foreach (GridBlock block in blocks)
            {
                // O bloco certo mantém a cor verdadeira (igual à cor-alvo do HUD);
                // todos os outros apagam suavemente.
                if (block == blocoCorreto)
                    continue;

                block.SetVisualColor(
                    Color.Lerp(block.BlockColor, corBlocosApagados, 0.75f * k)
                );
            }

            yield return null;
        }

        // Dá um tempo para o jogador entender o que aconteceu antes do painel.
        yield return new WaitForSeconds(atrasoGameOver);

        onGameOver?.Invoke();
    }

    // =========================================================
    // REINICIAR
    // =========================================================

    public void ReiniciarJogo()
    {
        if (derrotaCoroutine != null)
        {
            StopCoroutine(derrotaCoroutine);
            derrotaCoroutine = null;
        }

        if (timerCoroutine != null)
        {
            StopCoroutine(timerCoroutine);
            timerCoroutine = null;
        }

        // currentBlocks NÃO é limpo de propósito: ele reflete a posição física do
        // player (triggers). Limpar faria o jogo esquecer quem continua parado
        // em cima de um bloco, pois o trigger não dispara de novo.

        tempoAtual = tempoInicial;
        rodada = 0;
        roundActive = false;
        blocoCorreto = null;

        pontuacao = 0;
        combo = 0;
        multiplicador = 1f;
        ultimoGanho = 0;
        novoRecorde = false;
        chegada01 = 0f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetState(AudioManager.MusicState.Gameplay, 0.8f);
            AudioManager.Instance.SetMusicSpeed(1f, 0.8f);
        }

        IniciarPreparo(tempoPreparoRetry);
    }
}