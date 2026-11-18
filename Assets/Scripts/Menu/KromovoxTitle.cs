using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Titulo animado do KROMOVOX, montado 100% por codigo (sem sprite, sem fonte, sem prefab).
///
/// O logo e desenhado com "voxels" de pixel (como no logo do GDD: letras brancas, contorno
/// escuro e sombra). Animacao:
///  1) INTRO: os voxels caem do alto, letra por letra, quicando ao pousar (com som opcional).
///  2) IMPACTO: ao terminar, glitch forte + tremida no titulo.
///  3) IDLE (em loop):
///     - pulso de escala na batida da musica (AudioManager.BeatProgress / MusicTime);
///     - uma "onda" sobe pelas letras uma vez por compasso;
///     - balanco suave das letras;
///     - brilho arco-iris que varre o logo de tempos em tempos;
///     - glitch RGB ocasional (linhas deslocadas, tons ciano/magenta).
///  4) SUBTITULO digitado embaixo (opcional).
///
/// COMO USAR:
///  - Dentro do Canvas do menu: crie um GameObject vazio "Title" (RectTransform, pivot no centro),
///    posicione onde estava o texto "CHROMA BLITZ!" e adicione este script. Desative o texto antigo.
///  - IMPORTANTE: coloque o "Title" DENTRO do Main Panel (junto dos botoes), assim ele some
///    sozinho ao abrir as Opcoes. Na hierarquia ele deve ficar ANTES (acima) do painel de
///    fade preto, para o fade do Play cobrir o titulo.
///  - O logo fica centrado no ponto do objeto. Ajuste "Tamanho Voxel" para aumentar/diminuir.
///  - Sem AudioManager (ou com audio travado no WebGL), usa um relogio interno de 0,8 s por batida.
///  - Letras disponiveis: K R O M V X (para outras, adicione padroes na tabela Glyphs).
///  - Dica: no OnClick do botao JOGAR, arraste este objeto e escolha KromovoxTitle > Glitch().
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class KromovoxTitle : MonoBehaviour
{
    // ------------------------------------------------------------------ Inspector
    [Header("Texto (letras disponiveis: K R O M V X)")]
    public string linha1 = "KROMO";
    public string linha2 = "VOX";
    [Tooltip("Frase pequena embaixo do logo, digitada ao fim da intro. Vazio = sem subtitulo.")]
    public string subtitulo = "FIND THE COLOR";
    [Tooltip("Opcional: fonte pixel do menu (TMP Font Asset). Vazio = fonte padrao do TMP.")]
    public TMP_FontAsset font;
    public float tamanhoSubtitulo = 34f;

    [Header("Tamanho (base 1920x1080)")]
    [Tooltip("Tamanho de cada voxel em pixels. O logo inteiro cresce junto.")]
    public float tamanhoVoxel = 16f;
    [Range(0f, 0.25f)] public float espacoEntreVoxels = 0.06f;
    [Tooltip("Espessura do contorno, em voxels.")]
    [Range(0f, 0.8f)] public float contorno = 0.28f;
    [Tooltip("Espaco entre letras, em voxels.")]
    public float espacoLetras = 1f;
    [Tooltip("Espaco entre as duas linhas, em voxels.")]
    public float espacoLinhas = 1.5f;

    [Header("Camadas")]
    [Tooltip("DESLIGADO (recomendado): o titulo respeita a ordem da hierarquia, entao o fade preto " +
             "e os paineis que vem DEPOIS dele na hierarquia ficam por cima. " +
             "Ligado: Canvas aninhado, mais leve, mas desenha acima de tudo do Canvas pai.")]
    public bool canvasIsolado = false;

    [Header("Cores")]
    public Color corLetra = Color.white;
    public Color corContorno = new Color(0.05f, 0.02f, 0.12f, 1f);
    public Color corSombra = new Color(0f, 0f, 0f, 0.45f);
    public Color corSubtitulo = new Color(0.24f, 0.86f, 0.52f);
    public Color corGlitchA = new Color(0.20f, 1.00f, 0.90f);   // ciano
    public Color corGlitchB = new Color(1.00f, 0.20f, 0.80f);   // magenta

    [Header("Intro (queda dos voxels)")]
    [Tooltip("Tempo que cada voxel leva para cair e assentar.")]
    public float duracaoVoxel = 0.55f;
    [Tooltip("Atraso entre uma letra e a proxima.")]
    public float atrasoPorLetra = 0.11f;
    [Tooltip("Sorteio extra de atraso por voxel (deixa a queda 'chovendo').")]
    public float variacaoAtraso = 0.18f;
    [Tooltip("Toca um tique suave (hover do AudioManager) quando cada letra pousa.")]
    public bool somDasLetras = true;

    [Header("Animacao em loop")]
    [Tooltip("Quanto o logo 'bate' na batida da musica (0 = desliga).")]
    [Range(0f, 0.1f)] public float pulsoNaBatida = 0.035f;
    [Tooltip("Balanco suave das letras, em voxels.")]
    [Range(0f, 0.6f)] public float balanco = 0.22f;
    [Tooltip("Altura da onda que passa pelas letras a cada compasso, em voxels (0 = desliga).")]
    [Range(0f, 2f)] public float alturaDaOnda = 0.8f;
    [Tooltip("Intervalo entre os brilhos arco-iris, em segundos.")]
    public float intervaloBrilho = 4.5f;
    public float duracaoBrilho = 1.1f;
    [Range(0.05f, 0.5f)] public float larguraBrilho = 0.2f;
    [Tooltip("Intervalo (min/max) entre glitches ocasionais, em segundos.")]
    public float intervaloGlitchMin = 5f;
    public float intervaloGlitchMax = 9f;

    // ------------------------------------------------------------------ Fonte de pixels
    const int GlyphH = 7;

    static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'K', new[] { "110011", "110110", "111100", "111000", "111100", "110110", "110011" } },
        { 'R', new[] { "111110", "110011", "110011", "111110", "110110", "110011", "110011" } },
        { 'O', new[] { "111111", "110011", "110011", "110011", "110011", "110011", "111111" } },
        { 'M', new[] { "1100011", "1110111", "1111111", "1101011", "1100011", "1100011", "1100011" } },
        { 'V', new[] { "110011", "110011", "110011", "110011", "011110", "011110", "001100" } },
        { 'X', new[] { "110011", "011110", "001100", "001100", "001100", "011110", "110011" } },
    };

    // ------------------------------------------------------------------ Runtime
    class Voxel
    {
        public RectTransform fillRt, outRt;
        public Image fillImg, outImg;
        public Vector2 basePos;
        public Vector2 startOffset;
        public float delay;
        public int letter;
        public int row;          // linha global de pixels (para o glitch)
        public float xN, yN;     // 0..1 dentro do logo (para o brilho)
    }

    readonly List<Voxel> voxels = new List<Voxel>();
    RectTransform titleRoot;
    TextMeshProUGUI txtSub;

    float[] letterEnd;
    bool[] letterLanded;
    float[] rowOffset;
    int totalLetters;
    int totalRows;
    float introTotal;

    float tStart;
    bool introDone;
    float typeStart;

    float sweepStart = -999f;
    float nextSweep;
    float nextGlitch;
    float glitchEnd;
    float glitchNextReroll;
    float glitchStrength = 1f;

    float ultimoNorm = -1f;
    float semAvanco;

    // ------------------------------------------------------------------ Ciclo de vida
    void Awake()
    {
        Build();
    }

    void Start()
    {
        Replay();
    }

    void OnEnable()
    {
        // Voltou de outro painel (ex.: Opcoes): da uma piscada em vez de repetir a intro.
        if (voxels.Count > 0 && introDone) Glitch(0.6f);
    }

    /// <summary>Roda a intro de novo (queda dos voxels).</summary>
    [ContextMenu("Tocar intro de novo")]
    public void Replay()
    {
        tStart = Time.unscaledTime;
        introDone = false;
        sweepStart = -999f;
        glitchEnd = 0f;

        if (letterLanded != null)
            for (int i = 0; i < letterLanded.Length; i++) letterLanded[i] = false;

        if (txtSub != null) txtSub.maxVisibleCharacters = 0;
    }

    /// <summary>Glitch rapido (use no OnClick do JOGAR, por exemplo).</summary>
    public void Glitch()
    {
        Glitch(1f);
    }

    public void Glitch(float forca)
    {
        glitchStrength = Mathf.Max(0.2f, forca);
        glitchEnd = Time.unscaledTime + 0.22f + 0.1f * forca;
        glitchNextReroll = 0f;
    }

    // ------------------------------------------------------------------ Update
    void Update()
    {
        if (voxels.Count == 0) return;

        float now = Time.unscaledTime;
        float t = now - tStart;

        float beatLen;
        float clock = LerRelogio(out beatLen);
        float beatT = Mathf.Repeat(clock / beatLen, 1f);                  // 0 = na batida
        float barT = Mathf.Repeat(clock / (beatLen * 4f), 1f);            // 0..1 no compasso

        // Som de cada letra pousando.
        var am = AudioManager.Instance;
        for (int i = 0; i < totalLetters; i++)
        {
            if (!letterLanded[i] && t >= letterEnd[i])
            {
                letterLanded[i] = true;
                if (somDasLetras && am != null) am.PlayHover();
            }
        }

        // Fim da intro: impacto.
        if (!introDone && t >= introTotal)
        {
            introDone = true;
            typeStart = now;
            nextSweep = now;
            nextGlitch = now + Random.Range(intervaloGlitchMin, intervaloGlitchMax);
            Glitch(1.3f);
        }

        if (introDone)
        {
            if (now >= nextSweep)
            {
                sweepStart = now;
                nextSweep = now + Mathf.Max(duracaoBrilho + 0.2f, intervaloBrilho);
            }

            if (now >= nextGlitch && now >= glitchEnd)
            {
                Glitch(1f);
                nextGlitch = now + Random.Range(intervaloGlitchMin, intervaloGlitchMax);
            }
        }

        bool gl = now < glitchEnd;
        if (gl && now >= glitchNextReroll)
        {
            RerollGlitch();
            glitchNextReroll = now + 0.045f;
        }

        float sweepT = (now - sweepStart) / Mathf.Max(0.1f, duracaoBrilho);

        // Raiz: pulso na batida + tremida durante o glitch.
        float pulso = introDone ? pulsoNaBatida * Mathf.Pow(1f - beatT, 3f) : 0f;
        titleRoot.localScale = Vector3.one * (1f + pulso);
        titleRoot.anchoredPosition = gl
            ? new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * tamanhoVoxel * 0.12f * glitchStrength
            : Vector2.zero;

        // Onda que atravessa as letras uma vez por compasso.
        float ondaPos = barT * (totalLetters + 3f) - 1.5f;

        for (int i = 0; i < voxels.Count; i++)
        {
            Voxel v = voxels[i];

            float p = Mathf.Clamp01((t - v.delay) / Mathf.Max(0.01f, duracaoVoxel));
            Vector2 pos;
            float sc;
            float a;

            if (p < 1f)
            {
                // Queda: x suave, y com quique; escala com "overshoot".
                float x = Mathf.Lerp(v.basePos.x + v.startOffset.x, v.basePos.x, EaseOutCubic(p));
                float y = Mathf.Lerp(v.basePos.y + v.startOffset.y, v.basePos.y, EaseOutBounce(p));
                pos = new Vector2(x, y);
                sc = Mathf.LerpUnclamped(0.2f, 1f, EaseOutBack(p));
                a = Mathf.Clamp01(p * 6f);
            }
            else
            {
                pos = v.basePos;
                sc = 1f;
                a = 1f;
            }

            if (introDone)
            {
                float bob = Mathf.Sin(now * 2.4f + v.letter * 0.7f) * balanco * tamanhoVoxel;

                float hop = Mathf.Clamp01(1f - Mathf.Abs(ondaPos - v.letter) / 1.6f);
                hop = hop * hop * (3f - 2f * hop);

                pos.y += bob + hop * alturaDaOnda * tamanhoVoxel;
            }

            float desloc = gl ? rowOffset[v.row] : 0f;
            pos.x += desloc;

            // Cor: branco, com brilho arco-iris e tinta de glitch.
            Color c = corLetra;

            if (sweepT >= 0f && sweepT <= 1f)
            {
                float diag = v.xN * 0.8f + v.yN * 0.2f;
                float centro = Mathf.Lerp(-0.25f, 1.25f, sweepT);
                float k = Mathf.Clamp01(1f - Mathf.Abs(diag - centro) / larguraBrilho);
                k = k * k * (3f - 2f * k);

                Color arco = Color.HSVToRGB(Mathf.Repeat(diag * 0.85f + sweepT * 0.3f, 1f), 0.55f, 1f);
                c = Color.Lerp(c, arco, k);
            }

            if (gl && Mathf.Abs(desloc) > 0.01f)
                c = Color.Lerp(c, desloc > 0f ? corGlitchA : corGlitchB, 0.85f);

            c.a = a;

            Color o = corContorno;
            o.a = a;

            v.fillRt.anchoredPosition = pos;
            v.outRt.anchoredPosition = pos;
            v.fillRt.localScale = new Vector3(sc, sc, 1f);
            v.outRt.localScale = new Vector3(sc, sc, 1f);
            v.fillImg.color = c;
            v.outImg.color = o;
        }

        // Subtitulo: digita ao fim da intro e respira de leve na batida.
        if (txtSub != null)
        {
            if (introDone)
            {
                int total = subtitulo.Length;
                float tipo = Mathf.Clamp01((now - typeStart) / 0.8f);
                txtSub.maxVisibleCharacters = Mathf.CeilToInt(total * tipo);

                Color sub = corSubtitulo;
                sub.a = tipo < 1f ? 1f : 0.78f + 0.22f * (1f - beatT);
                txtSub.color = sub;
            }
            else
            {
                txtSub.maxVisibleCharacters = 0;
            }
        }
    }

    // ------------------------------------------------------------------ Relogio (musica ou interno)
    float LerRelogio(out float beatLen)
    {
        var am = AudioManager.Instance;
        beatLen = 0.8f;

        if (am == null) return Time.unscaledTime;

        beatLen = Mathf.Max(0.05f, am.BeatLength);
        return AudioAvancando(am) ? am.MusicTime : Time.unscaledTime;
    }

    /// <summary>
    /// True so se a musica esta tocando E o relogio dela anda de verdade
    /// (no WebGL o audio pode ficar parado ate o primeiro clique).
    /// </summary>
    bool AudioAvancando(AudioManager am)
    {
        if (!am.IsPlaying)
        {
            ultimoNorm = -1f;
            semAvanco = 0f;
            return false;
        }

        float n = am.Normalized;

        if (Mathf.Abs(n - ultimoNorm) > 0.00001f)
        {
            ultimoNorm = n;
            semAvanco = 0f;
            return true;
        }

        semAvanco += Time.unscaledDeltaTime;
        return semAvanco < 0.5f;
    }

    // ------------------------------------------------------------------ Glitch
    void RerollGlitch()
    {
        for (int i = 0; i < rowOffset.Length; i++)
        {
            rowOffset[i] = Random.value < 0.30f
                ? (Random.value < 0.5f ? -1f : 1f) * Random.Range(1f, 3.2f) * tamanhoVoxel * glitchStrength
                : 0f;
        }
    }

    // ------------------------------------------------------------------ Construcao
    void Build()
    {
        RectTransform host = (RectTransform)transform;

        // Sem Canvas por perto: cria um proprio (o logo fica no centro da tela).
        if (GetComponentInParent<Canvas>() == null)
        {
            var cgo = new GameObject("TitleCanvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);

            var cv = cgo.AddComponent<Canvas>();
            cv.renderMode = RenderMode.ScreenSpaceOverlay;
            cv.sortingOrder = 10;

            var sc = cgo.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080);
            sc.matchWidthOrHeight = 0.5f;

            host = (RectTransform)cgo.transform;
        }

        titleRoot = NovoRect("TitleRoot", host);

        // Canvas aninhado desenha POR CIMA de todo o Canvas pai (ignora a ordem da hierarquia):
        // ficaria acima do painel de Opcoes e do fade preto do Play. Por isso fica desligado.
        if (canvasIsolado)
            titleRoot.gameObject.AddComponent<Canvas>();

        RectTransform outLayer = NovoRect("Contorno", titleRoot);
        RectTransform fillLayer = NovoRect("Letras", titleRoot);

        // Linhas validas.
        var linhas = new List<string>();
        if (!string.IsNullOrEmpty(linha1)) linhas.Add(linha1.ToUpperInvariant());
        if (!string.IsNullOrEmpty(linha2)) linhas.Add(linha2.ToUpperInvariant());

        float larguraMax = 1f;
        foreach (string l in linhas) larguraMax = Mathf.Max(larguraMax, LarguraDaLinha(l));

        float alturaTotal = linhas.Count * GlyphH + Mathf.Max(0, linhas.Count - 1) * espacoLinhas;

        float tamFill = tamanhoVoxel * (1f - espacoEntreVoxels);
        float tamOut = tamanhoVoxel + contorno * 2f * tamanhoVoxel;

        int letraGlobal = 0;
        var fins = new List<float>();

        for (int li = 0; li < linhas.Count; li++)
        {
            string texto = linhas[li];
            float cursor = -LarguraDaLinha(texto) * 0.5f;
            float yTopo = alturaTotal * 0.5f - li * (GlyphH + espacoLinhas);

            for (int ci = 0; ci < texto.Length; ci++)
            {
                char ch = texto[ci];

                if (ch == ' ')
                {
                    cursor += 3f;
                    continue;
                }

                string[] g;
                if (!Glyphs.TryGetValue(ch, out g))
                {
                    Debug.LogWarning("KromovoxTitle: letra '" + ch + "' nao existe na tabela Glyphs.", this);
                    continue;
                }

                int larguraGlifo = g[0].Length;

                for (int r = 0; r < GlyphH; r++)
                {
                    for (int c = 0; c < larguraGlifo; c++)
                    {
                        if (g[r][c] != '1') continue;

                        var v = new Voxel();
                        v.basePos = new Vector2((cursor + c + 0.5f) * tamanhoVoxel, (yTopo - r - 0.5f) * tamanhoVoxel);
                        v.startOffset = new Vector2(Random.Range(-3f, 3f), Random.Range(14f, 26f)) * tamanhoVoxel;
                        v.delay = letraGlobal * atrasoPorLetra + Random.Range(0f, variacaoAtraso) + r * 0.01f;
                        v.letter = letraGlobal;
                        v.row = li * GlyphH + r;
                        v.xN = Mathf.Clamp01((v.basePos.x / tamanhoVoxel + larguraMax * 0.5f) / larguraMax);
                        v.yN = Mathf.Clamp01((v.basePos.y / tamanhoVoxel + alturaTotal * 0.5f) / alturaTotal);

                        v.outImg = NovoQuadrado(outLayer, "o", tamOut, corContorno);
                        v.outRt = v.outImg.rectTransform;

                        var sombra = v.outImg.gameObject.AddComponent<Shadow>();
                        sombra.effectColor = corSombra;
                        sombra.effectDistance = new Vector2(tamanhoVoxel * 0.35f, -tamanhoVoxel * 0.45f);
                        sombra.useGraphicAlpha = true;

                        v.fillImg = NovoQuadrado(fillLayer, "v", tamFill, corLetra);
                        v.fillRt = v.fillImg.rectTransform;

                        voxels.Add(v);

                        while (fins.Count <= letraGlobal) fins.Add(0f);
                        fins[letraGlobal] = Mathf.Max(fins[letraGlobal], v.delay + duracaoVoxel);
                    }
                }

                cursor += larguraGlifo + espacoLetras;
                letraGlobal++;
            }
        }

        totalLetters = letraGlobal;
        totalRows = linhas.Count * GlyphH;
        rowOffset = new float[Mathf.Max(1, totalRows)];
        letterEnd = fins.ToArray();
        letterLanded = new bool[totalLetters];

        introTotal = 0f;
        for (int i = 0; i < letterEnd.Length; i++) introTotal = Mathf.Max(introTotal, letterEnd[i]);
        introTotal += 0.05f;

        // Subtitulo (TextMeshPro).
        if (!string.IsNullOrEmpty(subtitulo))
        {
            RectTransform rt = NovoRect("Subtitulo", titleRoot);
            rt.sizeDelta = new Vector2(1400f, tamanhoSubtitulo * 2f);
            rt.anchoredPosition = new Vector2(0f, -(alturaTotal * 0.5f + 2.2f) * tamanhoVoxel);

            txtSub = rt.gameObject.AddComponent<TextMeshProUGUI>();
            txtSub.text = subtitulo;
            txtSub.fontSize = tamanhoSubtitulo;
            txtSub.color = corSubtitulo;
            txtSub.alignment = TextAlignmentOptions.Center;
            txtSub.fontStyle = FontStyles.Bold;
            txtSub.characterSpacing = 20f;
            txtSub.overflowMode = TextOverflowModes.Overflow;
            txtSub.raycastTarget = false;
            txtSub.outlineWidth = 0.2f;
            txtSub.outlineColor = corContorno;
            txtSub.maxVisibleCharacters = 0;
            if (font) txtSub.font = font;
        }
    }

    float LarguraDaLinha(string texto)
    {
        float w = 0f;
        int letras = 0;

        foreach (char ch in texto)
        {
            if (ch == ' ')
            {
                w += 3f;
                continue;
            }

            string[] g;
            if (!Glyphs.TryGetValue(ch, out g)) continue;

            w += g[0].Length;
            letras++;
        }

        if (letras > 1) w += (letras - 1) * espacoLetras;
        return w;
    }

    static RectTransform NovoRect(string nome, RectTransform pai)
    {
        var go = new GameObject(nome, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(pai, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return rt;
    }

    static Image NovoQuadrado(RectTransform pai, string nome, float tamanho, Color cor)
    {
        RectTransform rt = NovoRect(nome, pai);
        rt.sizeDelta = new Vector2(tamanho, tamanho);

        var img = rt.gameObject.AddComponent<Image>();
        img.color = cor;
        img.raycastTarget = false;
        return img;
    }

    // ------------------------------------------------------------------ Easings
    static float EaseOutCubic(float x)
    {
        return 1f - Mathf.Pow(1f - x, 3f);
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
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
