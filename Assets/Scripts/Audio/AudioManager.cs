using System;
using UnityEngine;

/// <summary>
/// KROMOVOX - gerenciador de audio unico (musica em stems + SFX + relogio de batidas).
///
/// - Persiste entre cenas (DontDestroyOnLoad). Coloque UMA vez na primeira cena (MainMenu).
/// - Musica: 7 stems tocados em sincronia por amostra (PlayScheduled), com estados e fades.
/// - SFX: pool de AudioSources, com variacao leve de pitch/volume a cada disparo.
/// - Ritmo: eventos OnBeat / OnBar calculados a partir da posicao REAL da musica
///   (continuam corretos mesmo se a velocidade/pitch da musica mudar).
/// - Volumes (master/musica/sfx) salvos em PlayerPrefs (com debounce: nao grava a cada frame do slider).
///
/// OTIMIZACOES desta versao:
///  - Tudo que e caro (dspTime, timeSamples) e lido UMA vez por frame e reaproveitado por todos os scripts.
///  - Clock: relogio de ritmo que SEMPRE anda. Se o audio travar (WebGL antes do primeiro clique),
///    ele continua batendo sozinho e volta a seguir a musica quando ela destravar.
///  - Filtro "abafado" so fica ligado quando esta de fato abafando (7 filtros ligados o tempo todo custam CPU).
///  - Presets sem alocacao, SFX sem consultar isPlaying, volumes/pitch so escritos quando mudam.
/// </summary>
[DefaultExecutionOrder(-200)]
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    public enum MusicState { Menu, Options, Starting, Gameplay, Paused, GameOver, Silent }

    enum Stem { Bass, Arp, Pad, Lead, Drums, Fx, Vhs }
    const int StemCount = 7;

    // ---------------------------------------------------------------- Inspector
    [Header("Música - stems (todos com o mesmo tamanho)")]
    public AudioClip bass;
    public AudioClip arp;
    public AudioClip pad;
    public AudioClip lead;
    public AudioClip drums;
    public AudioClip fx;
    public AudioClip vhs;

    [Header("Ritmo da música")]
    public float bpm = 75f;
    public int beatsPerBar = 4;

    [Header("SFX de interface")]
    public AudioClip[] hover;      // ui_hover_1..3 (sorteia sem repetir seguido)
    public AudioClip click;        // ui_click
    public AudioClip back;         // ui_back
    public AudioClip error;        // ui_error   (cor errada)
    public AudioClip correct;      // ui_correct (cor certa)
    public AudioClip jingle;       // hook_jingle

    [Header("Tique (últimos segundos da rodada)")]
    [Tooltip("Opcional. Se vazio, o tique é gerado por código (um 'toc' curto e suave).")]
    public AudioClip tick;
    [Range(0f, 1f)] public float tickVolume = 0.45f;

    [Header("Notas das cores (vermelho, amarelo, verde, ciano, azul, roxo)")]
    public AudioClip[] colorNotes;

    [Header("Volumes")]
    [Range(0f, 1f)] public float master = 1f;
    [Range(0f, 1f)] public float music = 1f;
    [Range(0f, 1f)] public float sfx = 1f;
    [Range(0f, 1f)] public float vhsIntensity = 1f;   // chiado de fita (ligue ao shader de VHS)

    [Header("Estado inicial / SFX")]
    public MusicState initialState = MusicState.Menu;
    public int sfxPoolSize = 10;

    [Header("Velocidade da música (gameplay)")]
    [Tooltip("Acima de 1.25 o pitch sobe mais de 4 semitons; prefira ficar até 1.25.")]
    public float maxMusicSpeed = 1.25f;

    [Header("Filtro 'abafado' (Opções / Pause / Game Over)")]
    public float openCutoff = 22000f;
    [Tooltip("No WebGL os filtros de áudio costumam não funcionar e ainda gastam CPU. " +
             "Deixe DESLIGADO; o abafado fica só pela mixagem dos stems.")]
    public bool filtroNoWebGL = false;

    // ---------------------------------------------------------------- Eventos de ritmo
    /// <summary>Disparado a cada batida. Parâmetro = batida dentro do loop (0..LoopBeats-1).</summary>
    public event Action<int> OnBeat;
    /// <summary>Disparado a cada compasso. Parâmetro = compasso dentro do loop.</summary>
    public event Action<int> OnBar;

    // ---------------------------------------------------------------- Estado interno
    class Layer
    {
        public AudioSource src;
        public AudioLowPassFilter lp;
        public float cur, target, speed;
        public float lastVol = -1f;
    }

    const string KeyMaster = "audio_master";
    const string KeyMusic = "audio_music";
    const string KeySfx = "audio_sfx";

    readonly Layer[] _layers = new Layer[StemCount];
    Layer _ref;                       // stem usado como relógio
    AudioSource[] _pool;
    float[] _poolTime;                // quando cada voz começou (para roubar a mais antiga)
    float[] _poolEnd;                 // quando cada voz termina (evita consultar isPlaying)

    float _duck = 1f, _duckTarget = 1f;
    float _cutoff, _cutoffTarget, _cutoffSpeed;
    float _speed = 1f, _speedTarget = 1f, _speedRate = 1f;

    // aplicados nos AudioSources (só escrevemos quando muda)
    float _pitchAplicado = -1f;
    float _cutoffAplicado = -1f;
    bool _temFiltro;
    bool _filtroAtivo;

    double _startDsp;
    bool _started;
    int _lastBeat = -1;
    int _lastHover = -1;

    // cache por frame
    bool _playing;
    float _musicTime;
    float _clock;
    float _fallbackClock;
    float _lastRawTime = -1f;
    float _stalledFor;
    bool _clockRunning;

    bool _prefsSujo;
    float _prefsT;

    // ---------------------------------------------------------------- Propriedades públicas
    /// <summary>True quando a música já começou a tocar (valor atualizado 1x por frame).</summary>
    public bool IsPlaying => _playing;

    /// <summary>
    /// True quando o relógio da música anda de verdade. No WebGL o navegador só libera o áudio
    /// depois do primeiro clique/tecla: antes disso isto fica false e o Clock usa o relógio de reserva.
    /// </summary>
    public bool ClockRunning => _clockRunning;

    /// <summary>Posição "crua" dentro do loop, em segundos (valor do frame; já considera pitch/velocidade).</summary>
    public float MusicTime => _musicTime;

    /// <summary>
    /// Posição dentro do loop, em segundos, que SEMPRE anda: segue a música quando ela está rodando
    /// e continua sozinha (na mesma velocidade) quando o áudio trava. Use este para animar no ritmo.
    /// </summary>
    public float Clock => _clock;

    public float LoopLength => (_ref != null && _ref.src.clip != null) ? _ref.src.clip.length : 76.8f;

    /// <summary>Posição no loop de 0 a 1 (use para girar a plataforma em sincronia).</summary>
    public float Normalized => LoopLength > 0f ? Mathf.Repeat(_clock / LoopLength, 1f) : 0f;

    public float BeatLength => 60f / Mathf.Max(1f, bpm);
    public int LoopBeats => Mathf.RoundToInt(LoopLength / BeatLength);

    /// <summary>Progresso dentro da batida atual: 0 = acabou de bater, 1 = próxima batida.</summary>
    public float BeatProgress => Mathf.Repeat(_clock / BeatLength, 1f);

    /// <summary>Progresso dentro do compasso atual (0..1).</summary>
    public float BarProgress => Mathf.Repeat(_clock / (BeatLength * Mathf.Max(1, beatsPerBar)), 1f);

    public float MasterVolume => master;
    public float MusicVolume => music;
    public float SfxVolume => sfx;
    public float MusicSpeed => _speed;

    // ---------------------------------------------------------------- Unity
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        master = PlayerPrefs.GetFloat(KeyMaster, master);
        music = PlayerPrefs.GetFloat(KeyMusic, music);
        sfx = PlayerPrefs.GetFloat(KeySfx, sfx);

        _cutoff = _cutoffTarget = _cutoffAplicado = openCutoff;
        BuildLayers();
        BuildSfxPool();

        if (tick == null) tick = CriarTick();
    }

    void Start()
    {
        if (Instance != this) return;
        RestartMusic();
        SetState(initialState, 0f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnApplicationPause(bool pausado)
    {
        if (pausado) SalvarPrefsAgora();
    }

    void OnApplicationQuit()
    {
        SalvarPrefsAgora();
    }

    void Update()
    {
        if (Instance != this) return;
        float dt = Time.unscaledDeltaTime;

        AtualizarRelogio(dt);

        _duck = Mathf.MoveTowards(_duck, _duckTarget, dt * 1.5f);
        _cutoff = Mathf.MoveTowards(_cutoff, _cutoffTarget, _cutoffSpeed * dt);
        _speed = Mathf.MoveTowards(_speed, _speedTarget, _speedRate * dt);

        bool pitchMudou = Mathf.Abs(_speed - _pitchAplicado) > 0.0005f;
        if (pitchMudou) _pitchAplicado = _speed;

        // Filtro: só liga quando realmente está abafando.
        if (_temFiltro)
        {
            bool precisa = _cutoff < openCutoff - 1f;

            if (precisa != _filtroAtivo)
            {
                _filtroAtivo = precisa;
                for (int i = 0; i < StemCount; i++)
                {
                    var l = _layers[i];
                    if (l == null || l.lp == null) continue;
                    if (precisa) l.lp.cutoffFrequency = _cutoff;
                    l.lp.enabled = precisa;
                }
                _cutoffAplicado = _cutoff;
            }
        }

        bool cutoffMudou = _filtroAtivo && Mathf.Abs(_cutoff - _cutoffAplicado) > 1f;
        if (cutoffMudou) _cutoffAplicado = _cutoff;

        for (int i = 0; i < StemCount; i++)
        {
            var l = _layers[i];
            if (l == null) continue;

            l.cur = Mathf.MoveTowards(l.cur, l.target, l.speed * dt);

            float v = l.cur * _duck * master * music;
            if (i == (int)Stem.Vhs) v *= vhsIntensity;

            if (Mathf.Abs(v - l.lastVol) > 0.0005f || (v <= 0f && l.lastVol != 0f))
            {
                l.src.volume = v;
                l.lastVol = v;
            }

            if (pitchMudou) l.src.pitch = _pitchAplicado;
            if (cutoffMudou && l.lp != null) l.lp.cutoffFrequency = _cutoffAplicado;
        }

        UpdateBeatClock();

        if (_prefsSujo && Time.unscaledTime - _prefsT > 0.6f)
            SalvarPrefsAgora();
    }

    // ---------------------------------------------------------------- Relógio (1x por frame)
    void AtualizarRelogio(float dt)
    {
        _playing = _started && _ref != null && AudioSettings.dspTime >= _startDsp;

        if (_playing && _ref.src.clip != null)
            _musicTime = _ref.src.timeSamples / (float)_ref.src.clip.frequency;
        else
            _musicTime = 0f;

        // Relógio de reserva: anda na mesma velocidade da música.
        _fallbackClock = Mathf.Repeat(_fallbackClock + dt * _speed, Mathf.Max(0.01f, LoopLength));

        if (_playing)
        {
            if (Mathf.Abs(_musicTime - _lastRawTime) > 0.00001f)
            {
                _lastRawTime = _musicTime;
                _stalledFor = 0f;
                _clockRunning = true;
            }
            else
            {
                _stalledFor += dt;
                if (_stalledFor > 0.5f) _clockRunning = false;
            }
        }
        else
        {
            _clockRunning = false;
            _stalledFor = 0f;
            _lastRawTime = -1f;
        }

        if (_clockRunning)
        {
            _clock = _musicTime;
            _fallbackClock = _musicTime;   // se travar de novo, continua daqui
        }
        else
        {
            _clock = _fallbackClock;
        }
    }

    // ---------------------------------------------------------------- Montagem
    void BuildLayers()
    {
        _temFiltro = Application.platform != RuntimePlatform.WebGLPlayer || filtroNoWebGL;

        AudioClip[] clips = { bass, arp, pad, lead, drums, fx, vhs };
        for (int i = 0; i < StemCount; i++)
        {
            if (clips[i] == null)
            {
                Debug.LogWarning($"AudioManager: stem '{(Stem)i}' sem clip.");
                continue;
            }

            var go = new GameObject("Stem_" + (Stem)i);
            go.transform.SetParent(transform, false);

            var s = go.AddComponent<AudioSource>();
            s.clip = clips[i];
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.volume = 0f;

            AudioLowPassFilter lp = null;
            if (_temFiltro)
            {
                lp = go.AddComponent<AudioLowPassFilter>();
                lp.cutoffFrequency = openCutoff;
                lp.enabled = false;   // ligado só quando abafa (Options/Pause/GameOver)
            }

            _layers[i] = new Layer { src = s, lp = lp };
        }

        // relógio: prefere o pad (sempre presente), senão o primeiro que existir
        _ref = _layers[(int)Stem.Pad];
        for (int i = 0; _ref == null && i < StemCount; i++) _ref = _layers[i];
    }

    void BuildSfxPool()
    {
        int n = Mathf.Max(2, sfxPoolSize);
        _pool = new AudioSource[n];
        _poolTime = new float[n];
        _poolEnd = new float[n];
        for (int i = 0; i < n; i++)
        {
            var go = new GameObject("Sfx_" + i);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;   // cliques funcionam mesmo com AudioListener.pause
            _pool[i] = s;
        }
    }

    // ---------------------------------------------------------------- Música
    /// <summary>Reinicia todos os stems do começo, alinhados (começa com o "hit" do sting).</summary>
    public void RestartMusic()
    {
        _startDsp = AudioSettings.dspTime + 0.25;
        for (int i = 0; i < StemCount; i++)
        {
            var l = _layers[i];
            if (l == null) continue;
            l.src.Stop();
            l.src.timeSamples = 0;
            l.src.PlayScheduled(_startDsp);
        }
        _lastBeat = -1;
        _lastRawTime = -1f;
        _stalledFor = 0f;
        _started = true;
    }

    // Presets (sem alocação): ordem dos stems = Bass, Arp, Pad, Lead, Drums, Fx, Vhs
    static readonly float[] VMenu = { 1f, 1f, 1f, 1f, 1f, 1f, 1f };
    static readonly float[] VOptions = { 1f, 0.8f, 1f, 0.25f, 0f, 0.5f, 1f };
    static readonly float[] VMudo = { 0f, 0f, 0f, 0f, 0f, 0f, 0f };
    static readonly float[] VGameplay = { 1f, 1f, 0.8f, 0.5f, 1f, 0f, 1f };   // ~3,7 dB mais baixa p/ SFX e notas
    static readonly float[] VPaused = { 1f, 0.6f, 1f, 0.2f, 0.3f, 0f, 1f };
    static readonly float[] VGameOver = { 0.6f, 0.4f, 1f, 0f, 0f, 0f, 1f };

    struct Preset
    {
        public float[] v;
        public float cutoff;
        public float duck;
    }

    Preset GetPreset(MusicState s)
    {
        switch (s)
        {
            case MusicState.Options: return new Preset { v = VOptions, cutoff = 1800f, duck = 1f };
            case MusicState.Starting: return new Preset { v = VMudo, cutoff = openCutoff, duck = 1f };
            case MusicState.Gameplay: return new Preset { v = VGameplay, cutoff = openCutoff, duck = 0.65f };
            case MusicState.Paused: return new Preset { v = VPaused, cutoff = 1400f, duck = 0.65f };
            case MusicState.GameOver: return new Preset { v = VGameOver, cutoff = 2500f, duck = 0.8f };
            case MusicState.Silent: return new Preset { v = VMudo, cutoff = openCutoff, duck = 1f };
            default: return new Preset { v = VMenu, cutoff = openCutoff, duck = 1f };
        }
    }

    /// <summary>Muda o "clima" da música com fade (segundos).</summary>
    public void SetState(MusicState state, float fadeSeconds = 1f)
    {
        var p = GetPreset(state);
        for (int i = 0; i < StemCount; i++)
        {
            var l = _layers[i];
            if (l == null) continue;
            l.target = Mathf.Clamp01(p.v[i]);
            if (fadeSeconds <= 0.001f) { l.cur = l.target; l.speed = 1000f; }
            else l.speed = Mathf.Abs(l.target - l.cur) / fadeSeconds;
        }

        _cutoffTarget = p.cutoff;
        _duckTarget = p.duck;
        if (fadeSeconds <= 0.001f)
        {
            _cutoff = p.cutoff;
            _duck = p.duck;
            _cutoffSpeed = 1e6f;
        }
        else
        {
            _cutoffSpeed = Mathf.Abs(p.cutoff - _cutoff) / fadeSeconds;
        }
    }

    /// <summary>
    /// Velocidade da música (1 = normal). Muda o pitch junto (efeito "fita acelerando", combina com o VHS).
    /// Use para acompanhar a dificuldade, ex.: SetMusicSpeed(1f + 0.25f * dificuldade01, 2f).
    /// </summary>
    public void SetMusicSpeed(float speed, float fadeSeconds = 1f)
    {
        _speedTarget = Mathf.Clamp(speed, 0.5f, Mathf.Max(1f, maxMusicSpeed));
        _speedRate = fadeSeconds <= 0.001f ? 1000f : Mathf.Abs(_speedTarget - _speed) / fadeSeconds;
        if (fadeSeconds <= 0.001f) _speed = _speedTarget;
    }

    /// <summary>Intensidade do chiado de VHS (0..1). Ligue ao valor do shader.</summary>
    public void SetVhsIntensity(float value) => vhsIntensity = Mathf.Clamp01(value);

    // ---------------------------------------------------------------- Relógio de batidas
    void UpdateBeatClock()
    {
        if (!_playing) return;

        int cur = Mathf.FloorToInt(_clock / BeatLength);

        if (_lastBeat < 0)
        {
            FireBeat(cur);
            _lastBeat = cur;
            return;
        }
        if (cur == _lastBeat) return;

        if (cur > _lastBeat && cur - _lastBeat <= 4)
        {
            for (int b = _lastBeat + 1; b <= cur; b++) FireBeat(b);
        }
        else
        {
            FireBeat(cur);   // loop reiniciou (ou o frame demorou demais)
        }
        _lastBeat = cur;
    }

    void FireBeat(int beat)
    {
        OnBeat?.Invoke(beat);
        if (beatsPerBar > 0 && beat % beatsPerBar == 0) OnBar?.Invoke(beat / beatsPerBar);
    }

    // ---------------------------------------------------------------- SFX
    public void PlayHover()
    {
        if (hover == null || hover.Length == 0) return;
        int i;
        int guard = 0;
        do { i = UnityEngine.Random.Range(0, hover.Length); }
        while (hover.Length > 1 && i == _lastHover && ++guard < 8);
        _lastHover = i;
        Play(hover[i], 0.025f, 0.08f);
    }

    public void PlayClick() => Play(click, 0.02f, 0.06f);
    public void PlayBack() => Play(back, 0.02f, 0.06f);
    public void PlayError() => Play(error, 0.02f, 0.05f);
    public void PlayCorrect() => Play(correct, 0.015f, 0.05f);
    public void PlayJingle() => Play(jingle, 0f, 0f);
    public void PlayTick() => Play(tick, 0.01f, 0.05f, tickVolume);

    /// <summary>Toca a nota da cor (0 vermelho, 1 amarelo, 2 verde, 3 ciano, 4 azul, 5 roxo).</summary>
    public void PlayColorNote(int colorIndex)
    {
        if (colorNotes == null || colorNotes.Length == 0) return;
        int n = colorNotes.Length;
        int i = ((colorIndex % n) + n) % n;
        // pitch quase fixo: as notas formam a melodia, não podem sair da escala
        Play(colorNotes[i], 0.003f, 0.08f);
    }

    /// <summary>"Toc" curto e suave (seno com queda rápida), usado se nenhum clip de tique foi definido.</summary>
    static AudioClip CriarTick()
    {
        const int sampleRate = 44100;
        int n = Mathf.RoundToInt(sampleRate * 0.06f);
        var data = new float[n];

        for (int i = 0; i < n; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 80f);
            float ataque = Mathf.Clamp01(i / 40f);        // evita o "clique" do início seco
            data[i] = Mathf.Sin(2f * Mathf.PI * 1250f * t) * envelope * ataque * 0.55f;
        }

        var clip = AudioClip.Create("tick", n, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    void Play(AudioClip clip, float pitchJitter, float volumeJitter, float gain = 1f)
    {
        if (clip == null || _pool == null) return;

        float agora = Time.unscaledTime;

        // Voz livre = a que já passou do fim estimado (sem consultar isPlaying no motor).
        int idx = -1;
        for (int i = 0; i < _pool.Length; i++)
        {
            if (agora >= _poolEnd[i]) { idx = i; break; }
        }
        if (idx < 0)   // todos ocupados: rouba o mais antigo
        {
            float oldest = float.MaxValue;
            for (int i = 0; i < _pool.Length; i++)
            {
                if (_poolTime[i] < oldest) { oldest = _poolTime[i]; idx = i; }
            }
        }

        var s = _pool[idx];
        float pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);

        s.clip = clip;
        s.pitch = pitch;
        s.volume = Mathf.Clamp01(master * sfx * gain * (1f + UnityEngine.Random.Range(-volumeJitter, volumeJitter)));
        s.Play();

        _poolTime[idx] = agora;
        _poolEnd[idx] = agora + clip.length / Mathf.Max(0.1f, pitch) + 0.02f;
    }

    // ---------------------------------------------------------------- Volumes (ligue aos sliders)
    public void SetMasterVolume(float v)
    {
        master = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMaster, master);
        MarcarPrefs();
    }

    public void SetMusicVolume(float v)
    {
        music = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeyMusic, music);
        MarcarPrefs();
    }

    public void SetSfxVolume(float v)
    {
        sfx = Mathf.Clamp01(v);
        PlayerPrefs.SetFloat(KeySfx, sfx);
        MarcarPrefs();
    }

    void MarcarPrefs()
    {
        _prefsSujo = true;
        _prefsT = Time.unscaledTime;
    }

    void SalvarPrefsAgora()
    {
        if (!_prefsSujo) return;
        PlayerPrefs.Save();
        _prefsSujo = false;
    }
}