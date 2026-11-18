using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dá vida ao cubo do player: amassa ao começar/parar de andar, balança um pouco
/// enquanto anda, "respira" parado e dá uma leve batidinha no ritmo da música.
///
/// Só mexe no VISUAL. O colisor, o Rigidbody e os triggers dos blocos continuam
/// exatamente como estão (por isso o tamanho do player na física nunca muda).
///
/// Como usar: coloque este script no objeto Player. Ele cria sozinho um "VisualPivot"
/// com a base do cubo como ponto fixo e move o visual para dentro dele. Se algo ficar
/// estranho no seu Player, arraste manualmente o objeto do visual no campo "Visual".
/// </summary>
public class PlayerJuice : MonoBehaviour
{
    [Header("Visual (opcional)")]
    [Tooltip("Objeto que será esticado/amassado. Vazio = criado automaticamente.")]
    public Transform visual;

    [Header("Amassar e esticar")]
    [Tooltip("Impulso ao começar a andar (negativo = amassa para baixo).")]
    public float impulsoInicio = -2.2f;
    [Tooltip("Impulso ao parar (positivo = estica para cima).")]
    public float impulsoParada = 1.4f;
    [Tooltip("Força da mola que devolve o cubo ao tamanho normal.")]
    public float mola = 220f;
    [Tooltip("Quanto a mola 'freia'. Menor = balança mais antes de assentar.")]
    public float amortecimento = 16f;

    [Header("Andando")]
    [Range(0f, 0.2f)] public float balanco = 0.04f;
    public float frequenciaBalanco = 11f;

    [Header("Parado")]
    [Range(0f, 0.1f)] public float respiracao = 0.012f;
    [Tooltip("Batidinha no ritmo da música (0 = desliga).")]
    [Range(0f, 0.1f)] public float pulsoNaBatida = 0.025f;

    Vector3 escalaBase = Vector3.one;
    float s;            // desvio da escala em Y (0 = normal)
    float v;            // velocidade desse desvio
    float vel01;        // 0 = parado, 1 = andando (suavizado)
    bool estavaMovendo;
    Vector2 ultimaDirecao = Vector2.up;
    PlayerMove playerMove;

    void Awake()
    {
        playerMove = GetComponent<PlayerMove>();

        if (visual == null)
            visual = CriarPivot();

        if (visual != null)
            escalaBase = visual.localScale;
    }

    void Update()
    {
        if (visual == null) return;

        float dt = Mathf.Min(Time.deltaTime, 0.033f);
        if (dt <= 0f) return;

        // Mesma leitura de input do PlayerMove (teclado, controle e toque).
        Vector2 input = playerMove != null ? playerMove.InputAtual : Vector2.zero;
        bool movendo = input.sqrMagnitude > 0.01f;

        if (movendo != estavaMovendo)
        {
            v += movendo ? impulsoInicio : impulsoParada;
            estavaMovendo = movendo;
        }
        else if (movendo && Vector2.Dot(input.normalized, ultimaDirecao) < -0.2f)
        {
            v += impulsoInicio * 0.5f;   // inverteu a direção: amassa de leve
        }

        if (movendo) ultimaDirecao = input.normalized;

        vel01 = Mathf.MoveTowards(vel01, movendo ? 1f : 0f, dt * 6f);

        // Mola amortecida: o impulso vira um "boing" que assenta sozinho.
        float aceleracao = -mola * s - amortecimento * v;
        v += aceleracao * dt;
        s += v * dt;

        float t = Time.time;
        float bob = Mathf.Sin(t * frequenciaBalanco) * balanco * vel01;
        float resp = Mathf.Sin(t * 2.2f) * respiracao * (1f - vel01);

        float batida = 0f;
        AudioManager am = AudioManager.Instance;
        if (pulsoNaBatida > 0f && am != null && am.IsPlaying)
        {
            float k = 1f - am.BeatProgress;                 // 1 na batida, 0 até a próxima
            batida = pulsoNaBatida * k * k * (1f - vel01);
        }

        float y = Mathf.Clamp(1f + s + bob + resp - batida, 0.6f, 1.4f);
        float xz = 1f / Mathf.Sqrt(y);                      // mantém o "volume": amassou, alarga

        visual.localScale = new Vector3(
            escalaBase.x * xz,
            escalaBase.y * y,
            escalaBase.z * xz
        );
    }

    /// <summary>
    /// Cria um pivô com a BASE do cubo como ponto fixo e coloca o visual dentro dele.
    /// O colisor e o Rigidbody ficam na raiz, intocados.
    /// </summary>
    Transform CriarPivot()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>();

        if (rends.Length == 0)
        {
            Debug.LogWarning("PlayerJuice: nenhum Renderer encontrado no Player.", this);
            return null;
        }

        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++)
            b.Encapsulate(rends[i].bounds);

        var pivotGo = new GameObject("VisualPivot");
        Transform pivot = pivotGo.transform;
        pivot.SetParent(transform, false);
        pivot.position = new Vector3(b.center.x, b.min.y, b.center.z);
        pivot.rotation = transform.rotation;

        // Caso 1: o cubo é o próprio objeto raiz (MeshRenderer ao lado do colisor).
        // Cria uma cópia visual dentro do pivô e esconde o renderer original.
        var rootMr = GetComponent<MeshRenderer>();
        var rootMf = GetComponent<MeshFilter>();

        if (rootMr != null && rootMf != null)
        {
            var proxyGo = new GameObject("Visual");
            proxyGo.layer = gameObject.layer;

            Transform proxy = proxyGo.transform;
            proxy.SetParent(pivot, false);
            proxy.position = transform.position;
            proxy.rotation = transform.rotation;

            proxyGo.AddComponent<MeshFilter>().sharedMesh = rootMf.sharedMesh;

            var mr = proxyGo.AddComponent<MeshRenderer>();
            mr.sharedMaterials = rootMr.sharedMaterials;
            mr.shadowCastingMode = rootMr.shadowCastingMode;
            mr.receiveShadows = rootMr.receiveShadows;

            rootMr.enabled = false;
        }

        // Caso 2: partes visuais em filhos (ex.: contorno). Vão para dentro do pivô,
        // exceto as que têm colisor (essas ficam como estão).
        var mover = new List<Transform>();
        foreach (Transform filho in transform)
        {
            if (filho == pivot) continue;
            if (filho.GetComponentInChildren<Renderer>(true) == null) continue;
            if (filho.GetComponentInChildren<Collider>(true) != null) continue;
            mover.Add(filho);
        }

        foreach (Transform f in mover)
            f.SetParent(pivot, true);

        return pivot;
    }
}