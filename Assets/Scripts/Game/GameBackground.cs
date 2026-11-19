using UnityEngine;

/// <summary>
/// Fundo vivo do KROMOVOX: pinta a cor de fundo da camera com um roxo escuro que
/// muda de matiz bem devagar e "respira" no ritmo da musica.
///
/// Nao depende do GameManager. Usa so o AudioManager (IsPlaying e BeatProgress, os mesmos
/// que o PlayerJuice ja usa). Sem AudioManager ou com a musica parada, ele continua
/// funcionando so com a deriva de matiz.
///
/// COMO USAR: coloque no objeto "Background" da cena do jogo. Se o campo Camera ficar
/// vazio, usa a Camera.main.
///
/// OTIMIZACAO: a cor so e escrita na camera quando muda de verdade.
/// </summary>
public class GameBackground : MonoBehaviour
{
    [Header("Camera")]
    [Tooltip("Vazio = Camera.main.")]
    public Camera alvo;

    [Header("Cor")]
    [Range(0f, 1f)] public float matizInicial = 0.72f;      // roxo
    [Range(0f, 1f)] public float saturacao = 0.75f;
    [Range(0f, 0.5f)] public float brilhoBase = 0.10f;      // fundo escuro, nao compete com os tiles

    [Header("Movimento")]
    [Tooltip("Quanto o matiz anda por segundo (0 = parado).")]
    public float velocidadeMatiz = 0.01f;
    [Tooltip("Quanto o matiz pode se afastar do inicial (0 a 0.5).")]
    [Range(0f, 0.5f)] public float amplitudeMatiz = 0.08f;

    [Header("Batida")]
    [Tooltip("Brilho extra na batida da musica (0 = desliga).")]
    [Range(0f, 0.3f)] public float pulsoNaBatida = 0.06f;

    Color corAplicada = new Color(-1f, -1f, -1f, -1f);
    float tempo;

    void Awake()
    {
        if (alvo == null) alvo = Camera.main;

        if (alvo == null)
        {
            Debug.LogWarning("GameBackground: nenhuma camera encontrada.", this);
            enabled = false;
            return;
        }

        alvo.clearFlags = CameraClearFlags.SolidColor;
    }

    void Update()
    {
        tempo += Time.unscaledDeltaTime;

        // Deriva lenta de matiz, indo e voltando em volta do matiz inicial.
        float matiz = Mathf.Repeat(
            matizInicial + Mathf.Sin(tempo * velocidadeMatiz * Mathf.PI * 2f) * amplitudeMatiz,
            1f);

        // Pulso no ritmo da musica: forte na batida, some ate a proxima.
        float brilho = brilhoBase;
        var am = AudioManager.Instance;
        if (pulsoNaBatida > 0f && am != null && am.IsPlaying)
        {
            float k = 1f - am.BeatProgress;
            brilho += pulsoNaBatida * k * k;
        }

        Aplicar(Color.HSVToRGB(matiz, saturacao, Mathf.Clamp01(brilho)));
    }

    void Aplicar(Color cor)
    {
        if (cor == corAplicada) return;

        corAplicada = cor;
        alvo.backgroundColor = cor;
    }
}