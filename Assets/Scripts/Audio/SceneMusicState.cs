using System.Collections;
using UnityEngine;

/// <summary>
/// Coloque um em cada cena para definir o clima da musica ao entrar
/// (MainMenu = Menu, cena do jogo = Gameplay, tela de derrota = GameOver...).
///
/// CORRECAO: se o AudioManager ainda nao existir no primeiro frame (ordem de execucao), tenta de novo
/// por alguns frames em vez de desistir em silencio.
/// </summary>
public class SceneMusicState : MonoBehaviour
{
    public AudioManager.MusicState state = AudioManager.MusicState.Menu;
    public float fade = 1f;
    [Tooltip("1 = velocidade normal. Na partida, o GameManager sobe a velocidade com a dificuldade.")]
    public float musicSpeed = 1f;

    void Start()
    {
        if (!Aplicar())
            StartCoroutine(TentarDeNovo());
    }

    bool Aplicar()
    {
        var am = AudioManager.Instance;
        if (am == null) return false;

        am.SetState(state, fade);
        am.SetMusicSpeed(musicSpeed, fade);
        return true;
    }

    IEnumerator TentarDeNovo()
    {
        for (int i = 0; i < 30; i++)
        {
            yield return null;
            if (Aplicar()) yield break;
        }
    }
}