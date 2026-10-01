using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Música")]
    [SerializeField] private AudioClip ambientMusic;
    [SerializeField] private AudioClip chaseMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.4f;
    [SerializeField] private float musicFadeTime = 1.5f;

    [Header("Enemigos")]
    [SerializeField] private AudioClip alertSound;
    [SerializeField] private AudioClip detectedSound;
    [SerializeField, Range(0f, 1f)] private float enemyVolume = 1f;

    [Header("Jugador")]
    [SerializeField] private AudioClip whistleSound;
    [SerializeField] private AudioClip hideSound;
    [SerializeField] private AudioClip[] footstepSounds;
    [SerializeField, Range(0f, 1f)] private float playerVolume = 0.8f;
    [SerializeField, Range(0f, 0.3f)] private float footstepPitchVariation = 0.08f;

    [Header("Resultado")]
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip loseSound;
    [SerializeField, Range(0f, 1f)] private float resultVolume = 1f;

    private AudioSource sfxSource;
    private AudioSource footstepSource;
    private AudioSource ambientSource;
    private AudioSource chaseSource;

    private EnemyAI[] enemies = new EnemyAI[0];
    private bool ended;

    private void Awake()
    {
        Instance = this;

        sfxSource = CreateSource(false);
        footstepSource = CreateSource(false);
        ambientSource = CreateSource(true);
        chaseSource = CreateSource(true);

        StartLoop(ambientSource, ambientMusic);
        StartLoop(chaseSource, chaseMusic);
        ambientSource.volume = musicVolume;
        chaseSource.volume = 0f;
    }

    private void Start()
    {
        enemies = FindObjectsByType<EnemyAI>(FindObjectsSortMode.None);
        for (int i = 0; i < enemies.Length; i++)
        {
            enemies[i].StateChanged += OnEnemyStateChanged;
            enemies[i].Alerted += OnEnemyAlerted;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] == null) continue;

            enemies[i].StateChanged -= OnEnemyStateChanged;
            enemies[i].Alerted -= OnEnemyAlerted;
        }

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (ended) return;

        bool chasing = false;
        for (int i = 0; i < enemies.Length; i++)
        {
            if (enemies[i] != null && enemies[i].CurrentState == EnemyState.Chase)
            {
                chasing = true;
                break;
            }
        }

        float step = musicVolume * Time.unscaledDeltaTime / Mathf.Max(0.05f, musicFadeTime);
        ambientSource.volume = Mathf.MoveTowards(ambientSource.volume, chasing ? 0f : musicVolume, step);
        chaseSource.volume = Mathf.MoveTowards(chaseSource.volume, chasing ? musicVolume : 0f, step);
    }

    private AudioSource CreateSource(bool loop)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        return source;
    }

    private void StartLoop(AudioSource source, AudioClip clip)
    {
        if (clip == null) return;
        source.clip = clip;
        source.Play();
    }

    private void OnEnemyStateChanged(EnemyState state)
    {
        if (state == EnemyState.Chase) PlayOne(detectedSound, enemyVolume);
    }

    private void OnEnemyAlerted()
    {
        PlayOne(alertSound, enemyVolume);
    }

    private void PlayOne(AudioClip clip, float volume)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    private void PlayStep(float volumeScale)
    {
        if (footstepSounds == null || footstepSounds.Length == 0) return;

        AudioClip clip = footstepSounds[Random.Range(0, footstepSounds.Length)];
        if (clip == null) return;

        footstepSource.pitch = 1f + Random.Range(-footstepPitchVariation, footstepPitchVariation);
        footstepSource.PlayOneShot(clip, playerVolume * volumeScale);
    }

    private void PlayEnd(bool won)
    {
        if (ended) return;
        ended = true;

        ambientSource.Stop();
        chaseSource.Stop();
        PlayOne(won ? winSound : loseSound, resultVolume);
    }

    public static void PlayWhistle()
    {
        if (Instance != null) Instance.PlayOne(Instance.whistleSound, Instance.playerVolume);
    }

    public static void PlayHide()
    {
        if (Instance != null) Instance.PlayOne(Instance.hideSound, Instance.playerVolume);
    }

    public static void PlayFootstep(float volumeScale)
    {
        if (Instance != null) Instance.PlayStep(volumeScale);
    }

    public static void PlayResult(bool won)
    {
        if (Instance != null) Instance.PlayEnd(won);
    }
}