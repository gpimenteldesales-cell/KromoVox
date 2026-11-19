using UnityEngine;

/// <summary>
/// Um tile do tabuleiro.
///
/// OTIMIZACOES: a instancia do material e criada UMA vez, o ID da propriedade de cor e descoberto
/// UMA vez (em vez de resolver o nome a cada troca de cor) e nada e escrito se a cor nao mudou.
/// Isso importa porque as ondas, pulsos e revelacoes trocam a cor dos 25 tiles todo frame.
/// A instancia do material e destruida junto com o tile (sem vazamento ao recarregar a cena).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class GridBlock : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");   // URP / HDRP
    static readonly int LegacyColorId = Shader.PropertyToID("_Color");     // Built-in

    public Color BlockColor { get; private set; }

    private Renderer rend;
    private Material mat;
    private int colorId;
    private Color corAplicada = new Color(-1f, -1f, -1f, -1f);

    private static bool avisouSemGameManager;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        mat = rend.material;   // instancia propria deste tile (criada uma unica vez)
        colorId = mat.HasProperty(BaseColorId) ? BaseColorId : LegacyColorId;
    }

    private void OnDestroy()
    {
        if (mat != null) Destroy(mat);
    }

    /// <summary>Define a cor "verdadeira" do tile (a que vale para o jogo) e a mostra.</summary>
    public void SetColor(Color color)
    {
        BlockColor = color;
        Aplicar(color);
    }

    /// <summary>Muda so o visual (ondas, pulsos, revelacao). BlockColor nao muda.</summary>
    public void SetVisualColor(Color color)
    {
        Aplicar(color);
    }

    private void Aplicar(Color color)
    {
        if (color == corAplicada) return;

        corAplicada = color;
        mat.SetColor(colorId, color);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (GameManager.Instance == null)
        {
            if (!avisouSemGameManager)
            {
                avisouSemGameManager = true;
                Debug.LogError($"GridBlock '{name}': GameManager não encontrado.");
            }

            return;
        }

        GameManager.Instance.OnPlayerEnteredBlock(this);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (GameManager.Instance == null)
            return;

        GameManager.Instance.OnPlayerExitedBlock(this);
    }
}