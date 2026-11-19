using UnityEngine;

/// <summary>
/// Utilidades de cor do KROMOVOX.
///
/// OTIMIZACOES: o HSV da cor-alvo e calculado uma unica vez por busca (antes era recalculado a cada
/// tentativa), e o sorteio "sem preto" nao usa mais um loop sem limite.
/// </summary>
public static class ColorUtils
{
    // =========================================================
    // ORIGINAIS (mantidos: o menu e o GameManager usam)
    // =========================================================

    /// <summary>
    /// Gera uma cor RGB aleatoria, evitando tons muito proximos do preto.
    /// (Sorteia no maximo 12 vezes; se nao achar, clareia a ultima em vez de ficar em loop.)
    /// </summary>
    public static Color RandomColorNoBlack(float minBrightness = 0.3f)
    {
        Color cor = new Color(Random.value, Random.value, Random.value);

        for (int i = 0; i < 12 && cor.grayscale < minBrightness; i++)
            cor = new Color(Random.value, Random.value, Random.value);

        if (cor.grayscale < minBrightness)
        {
            float k = minBrightness / Mathf.Max(0.01f, cor.grayscale);
            cor = new Color(Mathf.Min(1f, cor.r * k), Mathf.Min(1f, cor.g * k), Mathf.Min(1f, cor.b * k));
        }

        return cor;
    }

    /// <summary>
    /// Compara duas cores com uma pequena tolerancia (evita erro de ponto flutuante).
    /// </summary>
    public static bool CoresIguais(Color a, Color b, float tolerancia = 0.01f)
    {
        return Mathf.Abs(a.r - b.r) < tolerancia &&
               Mathf.Abs(a.g - b.g) < tolerancia &&
               Mathf.Abs(a.b - b.b) < tolerancia;
    }

    // =========================================================
    // CORES VIVAS E COMPARACAO JUSTA
    // =========================================================

    /// <summary>
    /// Cor viva: matiz aleatorio, saturacao e brilho sempre altos.
    /// Nada de cinza, pastel ou tom "sujo" (que o RGB aleatorio gera).
    /// </summary>
    public static Color RandomVibrant(float satMin = 0.65f, float valMin = 0.85f)
    {
        return Color.HSVToRGB(
            Random.value,
            Random.Range(Mathf.Clamp01(satMin), 1f),
            Random.Range(Mathf.Clamp01(valMin), 1f)
        );
    }

    /// <summary>
    /// Distancia visual aproximada entre duas cores, de 0 (iguais) a ~1 (opostas).
    /// O matiz pesa mais, mas so quando a cor e saturada (em cinza, matiz nao importa).
    /// </summary>
    public static float Distancia(Color a, Color b)
    {
        Color.RGBToHSV(a, out float h1, out float s1, out float v1);
        return Distancia(h1, s1, v1, b);
    }

    /// <summary>Mesma distancia, com o HSV da primeira cor ja calculado (evita refazer a conta em buscas).</summary>
    public static float Distancia(float h1, float s1, float v1, Color b)
    {
        Color.RGBToHSV(b, out float h2, out float s2, out float v2);

        // Matiz e circular: 0.95 e 0.05 estao pertinho.
        float dh = Mathf.Abs(h1 - h2);
        if (dh > 0.5f) dh = 1f - dh;
        dh *= 2f;                                // 0..1

        float matiz = dh * Mathf.Min(s1, s2);
        float sat = Mathf.Abs(s1 - s2) * 0.6f;
        float brilho = Mathf.Abs(v1 - v2) * 0.6f;

        return Mathf.Sqrt(matiz * matiz + sat * sat + brilho * brilho);
    }

    /// <summary>
    /// Cor viva que fica a pelo menos "distanciaMin" de "alvo".
    /// Tenta varias vezes; se nao conseguir, devolve a mais distante que achou.
    /// </summary>
    public static Color RandomVibrantAwayFrom(
        Color alvo,
        float distanciaMin,
        float satMin = 0.65f,
        float valMin = 0.85f,
        int tentativas = 60)
    {
        Color.RGBToHSV(alvo, out float h, out float s, out float v);

        Color melhor = RandomVibrant(satMin, valMin);
        float melhorDist = Distancia(h, s, v, melhor);

        if (melhorDist >= distanciaMin)
            return melhor;

        for (int i = 0; i < tentativas; i++)
        {
            Color c = RandomVibrant(satMin, valMin);
            float d = Distancia(h, s, v, c);

            if (d >= distanciaMin)
                return c;

            if (d > melhorDist)
            {
                melhor = c;
                melhorDist = d;
            }
        }

        return melhor;
    }
}