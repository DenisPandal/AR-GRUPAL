using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de Narrador por Voz exclusivo para los diálogos de la historia y subtítulos.
/// Reproduce locución hablada en español sincronizada mientras los diálogos se muestran en pantalla,
/// con atenuación automática de música (Audio Ducking) y compatibilidad multiplataforma (Clips / Android TTS).
/// </summary>
public class DialogueNarrator : MonoBehaviour
{
    public static DialogueNarrator I { get; private set; }

    [Header("Componentes de Audio")]
    [Tooltip("AudioSource dedicado exclusivamente a la voz del narrador.")]
    public AudioSource voiceAudioSource;

    [Tooltip("AudioSource de la música de fondo para aplicar atenuación (ducking) mientras habla el narrador.")]
    public AudioSource musicAudioSource;

    [Header("Ajustes de Volumen")]
    [Range(0f, 1f)] public float voiceVolume = 1f;
    [Range(0f, 1f)] public float duckedMusicVolume = 0.05f;
    [Range(0.5f, 2.0f)] public float speechRate = 1.0f;

    [Header("Opciones de Narración")]
    [Tooltip("Habilita la reproducción de clips de voz precargados en alta fidelidad.")]
    public bool enableAudioClips = true;

    [Tooltip("Habilita síntesis de voz (TTS) como respaldo para textos dinámicos o cuando no haya clip disponible.")]
    public bool enableTTSFallback = true;

    // Diccionario de clips de audio de voz indexados por clave
    private readonly Dictionary<string, AudioClip> mClipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);

    private Coroutine mMusicDuckingRoutine;
    private Coroutine mSpeechTrackingRoutine;
    private float mOriginalMusicVolume = 0.35f;
    private bool mIsSpeaking = false;

    public bool IsSpeaking => mIsSpeaking;

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject mAndroidTTS;
    private bool mAndroidTTSReady = false;
#endif

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;

        // Configurar o crear AudioSource de voz exclusivo (independiente de los efectos de sonido)
        if (voiceAudioSource == null)
        {
            Transform existingChild = transform.Find("VoiceNarrator_AudioSource");
            if (existingChild != null)
            {
                voiceAudioSource = existingChild.GetComponent<AudioSource>();
            }
            if (voiceAudioSource == null)
            {
                GameObject childGo = new GameObject("VoiceNarrator_AudioSource");
                childGo.transform.SetParent(transform, false);
                voiceAudioSource = childGo.AddComponent<AudioSource>();
            }
        }

        voiceAudioSource.playOnAwake = false;
        voiceAudioSource.loop = false;
        voiceAudioSource.spatialBlend = 0f; // 2D Audio para máxima claridad estéreo frontal
        voiceAudioSource.priority = 0; // Prioridad máxima en el mixer de Unity para que nunca sea cortado
        voiceAudioSource.volume = voiceVolume;
        voiceAudioSource.bypassEffects = true;
        voiceAudioSource.bypassListenerEffects = true;
        voiceAudioSource.bypassReverbZones = true;

        // Cargar todos los clips de voz de narrador desde Resources
        LoadVoiceClipsFromResources();

        // Inicializar Android TTS si estamos en Android
#if UNITY_ANDROID && !UNITY_EDITOR
        InitAndroidTTS();
#endif
    }

    private void Start()
    {
        if (musicAudioSource == null && StoryDirector.I != null)
        {
            musicAudioSource = StoryDirector.I.musicSource;
        }

        if (musicAudioSource != null)
        {
            mOriginalMusicVolume = musicAudioSource.volume;
        }
    }

    private void OnDestroy()
    {
        if (I == this) I = null;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (mAndroidTTS != null)
        {
            try
            {
                mAndroidTTS.Call("stop");
                mAndroidTTS.Call("shutdown");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DialogueNarrator] Error cerrando TTS de Android: {ex.Message}");
            }
            mAndroidTTS = null;
        }
#endif
    }

    private void OnDisable()
    {
        StopSpeaking();
        StopAllCoroutines();
        mMusicDuckingRoutine = null;
        if (musicAudioSource != null) musicAudioSource.volume = mOriginalMusicVolume;
    }

    /// <summary>
    /// Carga todos los clips de audio pregrabados desde Resources/Audio/Narrator.
    /// </summary>
    private void LoadVoiceClipsFromResources()
    {
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Audio/Narrator");
        foreach (var c in clips)
        {
            if (c != null && !mClipCache.ContainsKey(c.name))
            {
                mClipCache[c.name] = c;
            }
        }
        Debug.Log($"[DialogueNarrator] {mClipCache.Count} clips de voz de narrador cargados con éxito.");
    }

    /// <summary>
    /// Narra el diálogo especificado: reproduce la voz humana/sintetizada y atenúa la música.
    /// Retorna la duración estimada o real en segundos de la locución para sincronizar subtítulos.
    /// </summary>
    /// <param name="line">Línea de diálogo con hablante y texto.</param>
    /// <returns>Duración en segundos de la locución.</returns>
    public float Speak(SubtitleLine line)
    {
        if (line == null || string.IsNullOrWhiteSpace(line.text))
        {
            return 0f;
        }

        return Speak(line.speaker, line.text, line.audioKey, line.customAudioClip);
    }

    /// <summary>
    /// Narra un diálogo a partir de hablante y texto.
    /// </summary>
    public float Speak(string speaker, string text, string audioKey = null, AudioClip customClip = null)
    {
        StopSpeaking();

        AudioClip clipToPlay = customClip;

        // 1. Si no hay clip directo, buscar por audioKey o por coincidencia heurística
        if (clipToPlay == null && enableAudioClips)
        {
            clipToPlay = ResolveClip(audioKey, text, speaker);
        }

        float speechDuration = 0f;

        if (clipToPlay != null)
        {
            // Reproducir clip grabado de alta fidelidad
            voiceAudioSource.clip = clipToPlay;
            voiceAudioSource.volume = voiceVolume;
            voiceAudioSource.pitch = speechRate;
            voiceAudioSource.Play();
            speechDuration = clipToPlay.length / speechRate;
            mIsSpeaking = true;
            StartSpeechTracking(speechDuration);
            DuckingMusic(true);
            Debug.Log($"[DialogueNarrator] Narrando diálogo con clip '{clipToPlay.name}' ({speechDuration:F1}s): \"{text}\"");
        }
        else if (enableTTSFallback)
        {
            // Fallback: Síntesis de voz dinámica
            speechDuration = SpeakTTS(text);
            if (speechDuration > 0f)
            {
                mIsSpeaking = true;
                StartSpeechTracking(speechDuration);
                DuckingMusic(true);
            }
        }

        return speechDuration;
    }

    /// <summary>
    /// Detiene inmediatamente la locución del narrador y restablece la música de fondo.
    /// </summary>
    public void StopSpeaking()
    {
        if (voiceAudioSource != null && voiceAudioSource.isPlaying)
        {
            voiceAudioSource.Stop();
        }

        if (mSpeechTrackingRoutine != null)
        {
            StopCoroutine(mSpeechTrackingRoutine);
            mSpeechTrackingRoutine = null;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        if (mAndroidTTS != null && mAndroidTTSReady)
        {
            try
            {
                mAndroidTTS.Call<int>("stop");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DialogueNarrator] Error deteniendo Android TTS: {ex.Message}");
            }
        }
#endif

        if (mIsSpeaking)
        {
            mIsSpeaking = false;
            DuckingMusic(false);
        }
    }

    /// <summary>
    /// Resuelve automáticamente el clip de voz adecuado según la clave o el contenido del diálogo.
    /// </summary>
    private AudioClip ResolveClip(string key, string text, string speaker)
    {
        // 1. Búsqueda por clave directa si fue provista
        if (!string.IsNullOrEmpty(key) && mClipCache.TryGetValue(key, out AudioClip clip))
        {
            return clip;
        }

        string lowerText = text.ToLowerInvariant();
        string lowerSpk = !string.IsNullOrEmpty(speaker) ? speaker.ToLowerInvariant() : "";

        // 2. Mapeo heurístico inteligente por contenido de los diálogos del caso
        // Diálogos de Bienvenida e Introducción al entrar al juego
        if (lowerText.Contains("bienvenido a la simulación") || lowerText.Contains("ciberataque financiero de 2016") || lowerText.Contains("dos mil dieciséis"))
        {
            return GetCachedClip("intro_welcome_1");
        }
        if (lowerText.Contains("analizarás cronológicamente") || lowerText.Contains("desarrolló el ataque desde el viernes"))
        {
            return GetCachedClip("intro_welcome_2");
        }
        if (lowerText.Contains("revivirás los hechos reales") || (lowerText.Contains("modo ataque") && lowerText.Contains("58 millones")))
        {
            return GetCachedClip("intro_welcome_3");
        }
        if (lowerText.Contains("aplicarás las 3 medidas clave") || lowerText.Contains("aplicarás las tres medidas") || (lowerText.Contains("modo defensa") && lowerText.Contains("congelar los fondos")))
        {
            return GetCachedClip("intro_welcome_4");
        }
        if (lowerText.Contains("selecciona modo ataque o modo defensa") || lowerText.Contains("comenzar tu investigación en la mesa"))
        {
            return GetCachedClip("intro_welcome_5");
        }

        if (lowerText.Contains("hugo benavides ve la impresora") || lowerText.Contains("sala swift del banco sobre tu mesa"))
        {
            return GetCachedClip("vie_intro_1");
        }
        if (lowerText.Contains("seguro es una avería común") || lowerText.Contains("está apagada otra vez"))
        {
            return GetCachedClip("vie_intro_2");
        }
        if (lowerText.Contains("sin plan de respuesta, hugo no reporta"))
        {
            return GetCachedClip("vie_attack");
        }
        if (lowerText.Contains("file is missing or changed") || (lowerText.Contains("sábado 6") && lowerText.Contains("reiniciar")))
        {
            return GetCachedClip("sab_intro_1");
        }
        if (lowerText.Contains("nadie investiga qué archivo"))
        {
            return GetCachedClip("sab_intro_2");
        }
        if (lowerText.Contains("sin conciliación automática") || lowerText.Contains("pasa el sábado 6 y todo el domingo"))
        {
            return GetCachedClip("sab_attack");
        }
        if (lowerText.Contains("banco intenta pedir que congelen los fondos"))
        {
            return GetCachedClip("lun_intro_1");
        }
        if (lowerText.Contains("feriado en filipinas") || lowerText.Contains("nadie contesta del otro lado"))
        {
            return GetCachedClip("lun_intro_2");
        }
        if (lowerText.Contains("sin canal de escalamiento inmediato") || lowerText.Contains("queda sin leerse hasta el martes"))
        {
            return GetCachedClip("lun_attack");
        }
        if (lowerText.Contains("frente a ti en la mesa está la bóveda"))
        {
            return GetCachedClip("mar_intro_1");
        }
        if (lowerText.Contains("por fin se lee la orden de congelar"))
        {
            return GetCachedClip("mar_outcome_1");
        }
        if (lowerText.Contains("se acaban de retirar") || lowerText.Contains("la orden de congelar llega el martes"))
        {
            return GetCachedClip("mar_outcome_2");
        }
        if (lowerText.Contains("ahora puedes cambiar a modo defensa") || lowerText.Contains("detener el ataque el viernes"))
        {
            return GetCachedClip("mar_outcome_3");
        }
        if (lowerText.Contains("modo defensa (vie 5)") || lowerText.Contains("ve la impresora de confirmaciones swift apagada"))
        {
            return GetCachedClip("def_intro_1");
        }
        if (lowerText.Contains("hoy cuentas con un plan de respuesta") || lowerText.Contains("a diferencia del ataque de 2016"))
        {
            return GetCachedClip("def_intro_2");
        }
        if (lowerText.Contains("plan de respuesta ejecutado en vie 5") || lowerText.Contains("activa el protocolo y reporta"))
        {
            return GetCachedClip("def_victory_1");
        }
        if (lowerText.Contains("bloquean las transferencias el mismo viernes") || lowerText.Contains("cero horas perdidas"))
        {
            return GetCachedClip("def_victory_2");
        }
        if (lowerText.Contains("dinero no sale") || lowerText.Contains("58 millones") && lowerText.Contains("no salen del banco"))
        {
            return GetCachedClip("def_victory_3");
        }

        // Inspecciones de objetos 3D
        if (lowerText.Contains("impresora swift apagada: hugo benavides cree"))
        {
            return GetCachedClip("inspect_vie_impresora_ataque");
        }
        if (lowerText.Contains("identificas la falla como una anomalía crítica"))
        {
            return GetCachedClip("inspect_vie_impresora_defensa");
        }
        if (lowerText.Contains("hugo benavides está frente a la impresora apagada"))
        {
            return GetCachedClip("inspect_vie_hugo_ataque");
        }
        if (lowerText.Contains("hugo benavides recibe el protocolo de roles"))
        {
            return GetCachedClip("inspect_vie_hugo_defensa");
        }
        if (lowerText.Contains("monitor sobre el escritorio") || lowerText.Contains("alteró la base de datos"))
        {
            return GetCachedClip("inspect_sab_monitor");
        }
        if (lowerText.Contains("rack del servidor en la sala"))
        {
            return GetCachedClip("inspect_sab_servidor");
        }
        if (lowerText.Contains("enviar la orden de congelamiento a manila"))
        {
            return GetCachedClip("inspect_lun_banco");
        }
        if (lowerText.Contains("barrera horaria y el feriado en filipinas"))
        {
            return GetCachedClip("inspect_lun_barrera");
        }
        if (lowerText.Contains("frente a la bóveda a las 9:11"))
        {
            return GetCachedClip("inspect_mar_boveda");
        }
        if (lowerText.Contains("reservas de") && (lowerText.Contains("58") || lowerText.Contains("cincuenta y ocho")))
        {
            return GetCachedClip("inspect_mar_reservas");
        }

        // Respuestas a decisiones del jugador
        if (lowerText.Contains("excelente elección") || lowerText.Contains("confirmaste correctamente"))
        {
            return GetCachedClip("decision_correct");
        }
        if (lowerText.Contains("incorrecta") || lowerText.Contains("tu elección ("))
        {
            return GetCachedClip("decision_wrong");
        }

        return null;
    }

    private AudioClip GetCachedClip(string clipName)
    {
        if (mClipCache.TryGetValue(clipName, out AudioClip clip))
        {
            return clip;
        }

        // Intento de carga tardía
        clip = Resources.Load<AudioClip>($"Audio/Narrator/{clipName}");
        if (clip != null)
        {
            mClipCache[clipName] = clip;
        }
        return clip;
    }

    /// <summary>
    /// Invoca el motor de Texto a Voz (TTS) si no hay clip estático pregrabado.
    /// </summary>
    private float SpeakTTS(string text)
    {
        // Limpiar formato RichText como <b>, <color>, etc. para el sintetizador
        string cleanText = RemoveRichTextTags(text);
        float estimatedDuration = Mathf.Max(2.2f, cleanText.Length * 0.065f / speechRate);

#if UNITY_ANDROID && !UNITY_EDITOR
        if (mAndroidTTSReady && mAndroidTTS != null)
        {
            try
            {
                mAndroidTTS.Call<int>("setSpeechRate", Mathf.Clamp(speechRate, 0.5f, 2f));
                int result = mAndroidTTS.Call<int>("speak", cleanText, 0, null, "dialogue_id");
                return result == 0 ? estimatedDuration : 0f;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[DialogueNarrator] Error invocando Android TTS: {ex.Message}");
            }
        }
#endif

        Debug.LogWarning("[DialogueNarrator] No hay clip ni motor TTS disponible para: " + cleanText);
        return 0f;
    }

    private void StartSpeechTracking(float duration)
    {
        if (mSpeechTrackingRoutine != null)
        {
            StopCoroutine(mSpeechTrackingRoutine);
        }
        mSpeechTrackingRoutine = StartCoroutine(SpeechTrackingRoutine(duration));
    }

    private IEnumerator SpeechTrackingRoutine(float duration)
    {
        float timer = 0f;
        while (voiceAudioSource != null && voiceAudioSource.isPlaying || timer < duration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        mIsSpeaking = false;
        DuckingMusic(false);
        mSpeechTrackingRoutine = null;
    }

    /// <summary>
    /// Baja el volumen de la música mientras el narrador habla y lo restaura cuando termina.
    /// </summary>
    private void DuckingMusic(bool duck)
    {
        if (musicAudioSource == null) return;

        if (mMusicDuckingRoutine != null)
        {
            StopCoroutine(mMusicDuckingRoutine);
        }

        float targetVolume = duck ? duckedMusicVolume : mOriginalMusicVolume;
        if (!isActiveAndEnabled)
        {
            musicAudioSource.volume = targetVolume;
            return;
        }
        mMusicDuckingRoutine = StartCoroutine(FadeMusicVolume(targetVolume, 0.35f));
    }

    private IEnumerator FadeMusicVolume(float targetVolume, float duration)
    {
        if (musicAudioSource == null) yield break;

        float start = musicAudioSource.volume;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicAudioSource.volume = Mathf.Lerp(start, targetVolume, elapsed / duration);
            yield return null;
        }

        musicAudioSource.volume = targetVolume;
    }

    /// <summary>
    /// Remueve etiquetas de formato como <color>, <b>, <i> para que el motor de voz no las lea.
    /// </summary>
    public static string RemoveRichTextTags(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var sb = new System.Text.StringBuilder();
        bool inside = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == '<') inside = true;
            else if (c == '>') inside = false;
            else if (!inside) sb.Append(c);
        }
        return sb.ToString();
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    private void InitAndroidTTS()
    {
        try
        {
            using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
            {
                // Crear Android TextToSpeech con Locale en Español
                mAndroidTTS = new AndroidJavaObject("android.speech.tts.TextToSpeech", currentActivity, new AndroidTTSInitListener(this));
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[DialogueNarrator] No se pudo inicializar TextToSpeech de Android: {ex.Message}");
        }
    }

    private class AndroidTTSInitListener : AndroidJavaProxy
    {
        private readonly DialogueNarrator mOwner;
        public AndroidTTSInitListener(DialogueNarrator owner) : base("android.speech.tts.TextToSpeech$OnInitListener")
        {
            mOwner = owner;
        }

        public void onInit(int status)
        {
            if (status == 0) // TextToSpeech.SUCCESS
            {
                try
                {
                    using (var localeClass = new AndroidJavaClass("java.util.Locale"))
                    using (var spanishLocale = new AndroidJavaObject("java.util.Locale", "es", "ES"))
                    {
                        mOwner.mAndroidTTS.Call<int>("setLanguage", spanishLocale);
                        mOwner.mAndroidTTS.Call<int>("setSpeechRate", mOwner.speechRate);
                    }
                    mOwner.mAndroidTTSReady = true;
                    Debug.Log("[DialogueNarrator] Android TextToSpeech inicializado en Español exitosamente.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[DialogueNarrator] Error configurando idioma en Android TTS: {ex.Message}");
                }
            }
        }
    }
#endif
}
