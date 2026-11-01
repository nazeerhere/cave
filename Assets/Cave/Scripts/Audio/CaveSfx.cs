using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cave.Audio
{
    /// <summary>Opaque reference to one explicitly-owned loop.</summary>
    public struct CaveLoopHandle
    {
        internal CaveLoopHandle(int id) { Id = id; }
        internal int Id { get; }
        public bool IsValid => Id != 0;
    }

    /// <summary>
    /// Canonical Cave SFX owner. Legacy APIs remain available while bounded
    /// pooled playback supports positional and persistent sound safely.
    /// </summary>
    public static class CaveSfx
    {
        private const string LibraryResourceName = "CaveSfxLibrary";
        private const int WorldVoiceCapacity = 12;
        private const int UiVoiceCapacity = 4;
        private const int LoopCapacity = 8;
        private const float PositionalSpatialBlend = .8f;
        private const float PositionalMinDistance = 2f;
        private const float PositionalMaxDistance = 22f;

        private sealed class VoiceSlot
        {
            public AudioSource Source;
            public int CueIndex = -1;
            public CaveAudioChannel Channel;
            public float VolumeScale = 1f;
        }

        private sealed class LoopSlot
        {
            public AudioSource Source;
            public int Id;
            public bool Active;
            public CaveAudioChannel Channel;
            public float VolumeScale;
            public Object Owner;
        }

        private static CaveSfxLibrary library;
        private static GameObject root;
        private static VoiceSlot[] worldVoices;
        private static VoiceSlot[] uiVoices;
        private static LoopSlot[] loopSlots;
        private static float[] lastCueTimes;
        private static int[] lastVariantIndices;
        private static int nextLoopId;
        private static bool subscribed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            CaveAudioSettings.VolumesChanged -= HandleVolumesChanged;
            StopAllLoopsInternal();
            if (root != null && Application.isPlaying)
                Object.Destroy(root);
            library = null;
            root = null;
            worldVoices = null;
            uiVoices = null;
            loopSlots = null;
            lastCueTimes = null;
            lastVariantIndices = null;
            nextLoopId = 0;
            subscribed = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            EnsureInitialized();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureInitialListener()
        {
            EnsureSingleListener();
        }

        // Backward-compatible non-positional gameplay cue.
        public static void Play(CaveSfxCue cue, float volumeScale = 1f)
        {
            PlayCue(cue, CaveAudioChannel.Sfx, false, Vector3.zero, volumeScale);
        }

        // Backward-compatible optional Inspector-assigned clip playback.
        public static void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            PlayClip(clip, CaveAudioChannel.Sfx, volumeScale);
        }

        // Backward-compatible UI path, now physically separate from world SFX.
        public static void PlayUi(CaveSfxCue cue, float volumeScale = 1f)
        {
            PlayCue(cue, CaveAudioChannel.Ui, false, Vector3.zero, volumeScale);
        }

        public static void PlayAt(CaveSfxCue cue, Vector3 worldPosition, float volumeScale = 1f)
        {
            PlayCue(cue, CaveAudioChannel.Sfx, true, worldPosition, volumeScale);
        }

        public static void PlayClip(AudioClip clip, CaveAudioChannel channel, float volumeScale = 1f)
        {
            PlayRawClip(clip, channel, false, Vector3.zero, volumeScale);
        }

        public static void PlayClipAt(AudioClip clip, Vector3 worldPosition, float volumeScale = 1f)
        {
            PlayRawClip(clip, CaveAudioChannel.Sfx, true, worldPosition, volumeScale);
        }

        public static void PlayClipAt(AudioClip clip, Vector3 worldPosition, CaveAudioChannel channel, float volumeScale = 1f)
        {
            PlayRawClip(clip, channel, true, worldPosition, volumeScale);
        }

        public static CaveLoopHandle StartLoop(CaveSfxCue cue, float volumeScale = 1f, Object owner = null)
        {
            AudioClip clip;
            float pitch;
            if (!TryResolveCue(cue, out clip, out pitch, out _)) return default(CaveLoopHandle);
            return StartLoopInternal(clip, CaveAudioChannel.Sfx, false, Vector3.zero, volumeScale, pitch, owner);
        }

        public static CaveLoopHandle StartLoopAt(CaveSfxCue cue, Vector3 worldPosition, float volumeScale = 1f, Object owner = null)
        {
            AudioClip clip;
            float pitch;
            if (!TryResolveCue(cue, out clip, out pitch, out _)) return default(CaveLoopHandle);
            return StartLoopInternal(clip, CaveAudioChannel.Sfx, true, worldPosition, volumeScale, pitch, owner);
        }

        public static CaveLoopHandle StartLoop(AudioClip clip, CaveAudioChannel channel = CaveAudioChannel.Sfx,
            float volumeScale = 1f, Object owner = null)
        {
            return StartLoopInternal(clip, channel, false, Vector3.zero, volumeScale, 1f, owner);
        }

        public static CaveLoopHandle StartLoopAt(AudioClip clip, Vector3 worldPosition,
            CaveAudioChannel channel = CaveAudioChannel.Sfx, float volumeScale = 1f, Object owner = null)
        {
            return StartLoopInternal(clip, channel, true, worldPosition, volumeScale, 1f, owner);
        }

        public static void StopLoop(CaveLoopHandle handle)
        {
            if (!handle.IsValid || loopSlots == null) return;
            for (int i = 0; i < loopSlots.Length; i++)
            {
                LoopSlot slot = loopSlots[i];
                if (slot != null && slot.Active && slot.Id == handle.Id)
                {
                    ReleaseLoop(slot);
                    return;
                }
            }
        }

        public static void SetLoopVolume(CaveLoopHandle handle, float volumeScale)
        {
            if (!handle.IsValid || loopSlots == null) return;
            for (int i = 0; i < loopSlots.Length; i++)
            {
                LoopSlot slot = loopSlots[i];
                if (slot != null && slot.Active && slot.Id == handle.Id)
                {
                    slot.VolumeScale = Mathf.Clamp01(volumeScale);
                    ApplyLoopVolume(slot);
                    return;
                }
            }
        }

        public static void StopAllLoops()
        {
            StopAllLoopsInternal();
        }

        internal static int ResolveVariantIndex(int variantCount, int previousIndex, int candidateIndex)
        {
            if (variantCount <= 1) return variantCount == 1 ? 0 : -1;
            int resolved = Mathf.Clamp(candidateIndex, 0, variantCount - 1);
            return resolved == previousIndex ? (resolved + 1) % variantCount : resolved;
        }

        internal static float ResolvePitch(float minimumPitch, float maximumPitch, float normalizedRandom)
        {
            float min = Mathf.Min(minimumPitch, maximumPitch);
            float max = Mathf.Max(minimumPitch, maximumPitch);
            return Mathf.Lerp(min, max, Mathf.Clamp01(normalizedRandom));
        }

        internal static bool IsCooldownReady(float previousTime, float cooldownSeconds, float now)
        {
            return cooldownSeconds <= 0f || previousTime < 0f || now - previousTime >= cooldownSeconds;
        }

        internal static int ConfiguredWorldVoiceCapacity => WorldVoiceCapacity;
        internal static int ConfiguredUiVoiceCapacity => UiVoiceCapacity;
        internal static int ConfiguredLoopCapacity => LoopCapacity;

        internal static void UpdateOwnedLoops()
        {
            if (loopSlots == null) return;
            for (int i = 0; i < loopSlots.Length; i++)
            {
                LoopSlot slot = loopSlots[i];
                if (slot == null || !slot.Active) continue;
                if (slot.Source == null || !slot.Source.isPlaying || !OwnerIsActive(slot.Owner))
                    ReleaseLoop(slot);
            }
        }

        private static bool EnsureInitialized()
        {
            if (root != null) return true;
            if (!Application.isPlaying) return false;

            library = Resources.Load<CaveSfxLibrary>(LibraryResourceName);
            root = new GameObject("[Cave] SFX");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<CaveSfxRuntime>();
            worldVoices = CreateVoicePool("World Voice", WorldVoiceCapacity, 128);
            uiVoices = CreateVoicePool("UI Voice", UiVoiceCapacity, 64);
            loopSlots = CreateLoopPool();

            int cueCount = System.Enum.GetValues(typeof(CaveSfxCue)).Length;
            lastCueTimes = new float[cueCount];
            lastVariantIndices = new int[cueCount];
            for (int i = 0; i < cueCount; i++)
            {
                lastCueTimes[i] = -1f;
                lastVariantIndices[i] = -1;
            }

            if (!subscribed)
            {
                SceneManager.sceneLoaded += HandleSceneLoaded;
                CaveAudioSettings.VolumesChanged += HandleVolumesChanged;
                subscribed = true;
            }
            return true;
        }

        private static VoiceSlot[] CreateVoicePool(string label, int count, int priority)
        {
            VoiceSlot[] slots = new VoiceSlot[count];
            for (int i = 0; i < count; i++)
                slots[i] = new VoiceSlot { Source = CreateSource(label + " " + (i + 1), priority) };
            return slots;
        }

        private static LoopSlot[] CreateLoopPool()
        {
            LoopSlot[] slots = new LoopSlot[LoopCapacity];
            for (int i = 0; i < slots.Length; i++)
                slots[i] = new LoopSlot { Source = CreateSource("Loop " + (i + 1), 96) };
            return slots;
        }

        private static AudioSource CreateSource(string label, int priority)
        {
            GameObject sourceObject = new GameObject(label);
            sourceObject.transform.SetParent(root.transform, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.priority = priority;
            source.dopplerLevel = 0f;
            source.spatialBlend = 0f;
            return source;
        }

        private static void PlayCue(CaveSfxCue cue, CaveAudioChannel channel, bool positional, Vector3 worldPosition, float volumeScale)
        {
            AudioClip clip;
            float pitch;
            CaveSfxCueProfile profile;
            if (!TryResolveCue(cue, out clip, out pitch, out profile) || !EnsureInitialized()) return;

            float now = Time.unscaledTime;
            int cueIndex = (int)cue;
            float cooldown = profile != null ? profile.CooldownSeconds : GetDefaultCooldown(cue);
            if (!IsCooldownReady(lastCueTimes[cueIndex], cooldown, now)) return;
            int maximumConcurrentVoices = profile != null
                ? profile.MaximumConcurrentVoices
                : GetDefaultMaximumConcurrentVoices(cue);
            if (maximumConcurrentVoices > 0
                && CountActiveCueVoices(cueIndex) >= maximumConcurrentVoices) return;

            VoiceSlot voice = AcquireVoice(channel);
            if (voice == null) return;
            StartOneShot(voice, clip, channel, positional, worldPosition, volumeScale, pitch, cueIndex);
            lastCueTimes[cueIndex] = now;
        }

        private static void PlayRawClip(AudioClip clip, CaveAudioChannel channel, bool positional, Vector3 worldPosition, float volumeScale)
        {
            if (clip == null || !EnsureInitialized()) return;
            VoiceSlot voice = AcquireVoice(channel);
            if (voice == null) return;
            StartOneShot(voice, clip, channel, positional, worldPosition, volumeScale, 1f, -1);
        }

        private static bool TryResolveCue(CaveSfxCue cue, out AudioClip clip, out float pitch, out CaveSfxCueProfile profile)
        {
            clip = null;
            pitch = 1f;
            profile = null;
            if (!EnsureInitialized() || library == null) return false;

            profile = library.GetProfile(cue);
            if (profile != null)
            {
                pitch = ResolvePitch(profile.MinimumPitch, profile.MaximumPitch, Random.value);
                if (profile.HasVariants) clip = SelectVariant(cue, profile);
            }
            if (clip == null) clip = library.GetClip(cue);
            if (clip == null)
            {
                CaveSfxCue fallbackCue = GetTemporaryFallbackCue(cue);
                if (fallbackCue != cue) clip = library.GetClip(fallbackCue);
            }
            return clip != null;
        }

        // These mappings keep core controls audible until final semantic clips
        // are authored in CaveSfxLibrary profiles. A profile variant always
        // takes precedence, and HeavyCharge intentionally remains silent until
        // it has a loop asset rather than looping an unrelated transient clip.
        private static CaveSfxCue GetTemporaryFallbackCue(CaveSfxCue cue)
        {
            switch (cue)
            {
                case CaveSfxCue.PlayerJump:
                case CaveSfxCue.PlayerDash:
                case CaveSfxCue.SwordSwing:
                case CaveSfxCue.SpinSwing:
                case CaveSfxCue.HeavyRelease:
                case CaveSfxCue.GroundSmashDescent:
                    return CaveSfxCue.Whoosh;
                case CaveSfxCue.PlayerLanding:
                    return CaveSfxCue.Landing;
                case CaveSfxCue.PlayerHurt:
                case CaveSfxCue.BlockImpact:
                    return CaveSfxCue.Hit;
                case CaveSfxCue.PlayerDeath:
                case CaveSfxCue.HeavyImpact:
                case CaveSfxCue.GuardBreak:
                case CaveSfxCue.GroundSmashImpact:
                    return CaveSfxCue.Explosion;
                case CaveSfxCue.PlayerRespawn:
                    return CaveSfxCue.Spawn;
                case CaveSfxCue.HeavyChargeStage:
                case CaveSfxCue.Parry:
                case CaveSfxCue.PerfectParry:
                    return CaveSfxCue.Bonus;
                case CaveSfxCue.EnemyMeleeSwing:
                    return CaveSfxCue.Whoosh;
                case CaveSfxCue.EnemyBodyImpact:
                case CaveSfxCue.EnemyBlockImpact:
                case CaveSfxCue.EnemyStagger:
                    return CaveSfxCue.Hit;
                case CaveSfxCue.EnemyHeavyImpact:
                case CaveSfxCue.EnemyGuardBreak:
                case CaveSfxCue.EnemyDeath:
                    return CaveSfxCue.Explosion;
                case CaveSfxCue.EnemyProjectileLaunch:
                case CaveSfxCue.StatusBurnStart:
                    return CaveSfxCue.Shot;
                case CaveSfxCue.StatusSlowStart:
                case CaveSfxCue.StatusImmobilizeStart:
                case CaveSfxCue.StatusReleased:
                    return CaveSfxCue.Bonus;
                case CaveSfxCue.PlayerProjectileLaunch:
                    return CaveSfxCue.Shot;
                case CaveSfxCue.PlayerProjectileImpact:
                    return CaveSfxCue.Hit;
                case CaveSfxCue.SkillPathHeat:
                case CaveSfxCue.SkillPathOrder:
                case CaveSfxCue.SkillPathFlow:
                case CaveSfxCue.SkillPathMass:
                case CaveSfxCue.BraceQuick:
                case CaveSfxCue.BraceFull:
                case CaveSfxCue.BraceDeep:
                case CaveSfxCue.BraceExit:
                case CaveSfxCue.StrengthShieldReady:
                case CaveSfxCue.StrengthShieldRecharge:
                case CaveSfxCue.FrenzyActivated:
                case CaveSfxCue.FrenzyExpired:
                case CaveSfxCue.ResonanceStep:
                case CaveSfxCue.DestabilizedEnd:
                case CaveSfxCue.Counterphase:
                case CaveSfxCue.FrequencyFiltering:
                case CaveSfxCue.PhaseOpening:
                case CaveSfxCue.LatentThreshold:
                case CaveSfxCue.TemporalState:
                case CaveSfxCue.TemporalRate:
                case CaveSfxCue.TemporalAcceleration:
                case CaveSfxCue.StateLock:
                case CaveSfxCue.RateLock:
                case CaveSfxCue.AccelerationLock:
                case CaveSfxCue.ControlledEquilibrium:
                case CaveSfxCue.ManifestationThreshold:
                case CaveSfxCue.MasteryMilestone:
                    return CaveSfxCue.Bonus;
                case CaveSfxCue.StrengthShieldBreak:
                case CaveSfxCue.FrenzyBreak:
                case CaveSfxCue.ResonanceBreak:
                case CaveSfxCue.DestabilizedStart:
                case CaveSfxCue.PhaseCollapse:
                case CaveSfxCue.Convergence:
                    return CaveSfxCue.Explosion;
                // Sustain is a future loop identity. It intentionally stays
                // silent until an explicitly authored loop profile exists.
                case CaveSfxCue.FrenzySustain:
                    return cue;
                default:
                    return cue;
            }
        }

        private static float GetDefaultCooldown(CaveSfxCue cue)
        {
            // Accepted rapid player damage still gives feedback, but cannot
            // produce an uncontrolled pile-up when several sources land at once.
            switch (cue)
            {
                case CaveSfxCue.PlayerHurt:
                case CaveSfxCue.EnemyStagger:
                    return .08f;
                case CaveSfxCue.EnemyBodyImpact:
                case CaveSfxCue.EnemyHeavyImpact:
                    return .03f;
                case CaveSfxCue.StatusBurnStart:
                case CaveSfxCue.StatusSlowStart:
                case CaveSfxCue.StatusImmobilizeStart:
                case CaveSfxCue.StatusReleased:
                    return .12f;
                case CaveSfxCue.PlayerProjectileImpact:
                    return .03f;
                case CaveSfxCue.ResonanceStep:
                case CaveSfxCue.TemporalState:
                case CaveSfxCue.TemporalRate:
                case CaveSfxCue.TemporalAcceleration:
                    return .1f;
                case CaveSfxCue.LatentThreshold:
                case CaveSfxCue.ManifestationThreshold:
                case CaveSfxCue.MasteryMilestone:
                    return .2f;
                default:
                    return 0f;
            }
        }

        private static int GetDefaultMaximumConcurrentVoices(CaveSfxCue cue)
        {
            switch (cue)
            {
                case CaveSfxCue.EnemyBodyImpact:
                case CaveSfxCue.EnemyHeavyImpact:
                    return 4;
                case CaveSfxCue.EnemyMeleeSwing:
                case CaveSfxCue.EnemyProjectileLaunch:
                    return 6;
                case CaveSfxCue.EnemyDeath:
                case CaveSfxCue.EnemyStagger:
                case CaveSfxCue.StatusBurnStart:
                case CaveSfxCue.StatusSlowStart:
                case CaveSfxCue.StatusImmobilizeStart:
                case CaveSfxCue.PlayerProjectileImpact:
                case CaveSfxCue.ResonanceStep:
                case CaveSfxCue.TemporalState:
                case CaveSfxCue.TemporalRate:
                case CaveSfxCue.TemporalAcceleration:
                    return 3;
                case CaveSfxCue.PlayerProjectileLaunch:
                    return 6;
                default:
                    return 0;
            }
        }

        private static AudioClip SelectVariant(CaveSfxCue cue, CaveSfxCueProfile profile)
        {
            AudioClip[] variants = profile.Variants;
            if (variants == null || variants.Length == 0) return null;
            int validCount = 0;
            for (int i = 0; i < variants.Length; i++) if (variants[i] != null) validCount++;
            if (validCount == 0) return null;

            int candidate = Random.Range(0, variants.Length);
            int previous = lastVariantIndices[(int)cue];
            if (validCount > 1 && candidate == previous) candidate = (candidate + 1) % variants.Length;
            for (int offset = 0; offset < variants.Length; offset++)
            {
                int index = (candidate + offset) % variants.Length;
                if (variants[index] == null || (validCount > 1 && index == previous)) continue;
                lastVariantIndices[(int)cue] = index;
                return variants[index];
            }
            for (int i = 0; i < variants.Length; i++)
                if (variants[i] != null) { lastVariantIndices[(int)cue] = i; return variants[i]; }
            return null;
        }

        private static VoiceSlot AcquireVoice(CaveAudioChannel channel)
        {
            VoiceSlot[] pool = channel == CaveAudioChannel.Ui ? uiVoices : worldVoices;
            if (pool == null) return null;
            for (int i = 0; i < pool.Length; i++)
            {
                VoiceSlot voice = pool[i];
                if (voice != null && voice.Source != null && !voice.Source.isPlaying) return voice;
            }
            // Bounded deterministic policy: reject the newest voice rather than
            // interrupting an active sound when the relevant pool is exhausted.
            return null;
        }

        private static int CountActiveCueVoices(int cueIndex)
        {
            return CountActiveCueVoices(worldVoices, cueIndex) + CountActiveCueVoices(uiVoices, cueIndex);
        }

        private static int CountActiveCueVoices(VoiceSlot[] pool, int cueIndex)
        {
            if (pool == null) return 0;
            int count = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                VoiceSlot voice = pool[i];
                if (voice != null && voice.CueIndex == cueIndex && voice.Source != null && voice.Source.isPlaying) count++;
            }
            return count;
        }

        private static void StartOneShot(VoiceSlot voice, AudioClip clip, CaveAudioChannel channel, bool positional,
            Vector3 worldPosition, float volumeScale, float pitch, int cueIndex)
        {
            AudioSource source = voice.Source;
            ResetSource(source);
            ConfigureSpatial(source, positional, worldPosition);
            source.clip = clip;
            source.loop = false;
            source.pitch = pitch;
            voice.Channel = channel;
            voice.VolumeScale = Mathf.Clamp01(volumeScale);
            ApplyVoiceVolume(voice);
            voice.CueIndex = cueIndex;
            source.Play();
        }

        private static CaveLoopHandle StartLoopInternal(AudioClip clip, CaveAudioChannel channel, bool positional,
            Vector3 worldPosition, float volumeScale, float pitch, Object owner)
        {
            if (clip == null || !EnsureInitialized()) return default(CaveLoopHandle);
            LoopSlot slot = AcquireLoopSlot();
            if (slot == null) return default(CaveLoopHandle);

            ResetSource(slot.Source);
            ConfigureSpatial(slot.Source, positional, worldPosition);
            slot.Id = NextLoopId();
            slot.Active = true;
            slot.Channel = channel;
            slot.VolumeScale = Mathf.Clamp01(volumeScale);
            slot.Owner = owner;
            slot.Source.clip = clip;
            slot.Source.loop = true;
            slot.Source.pitch = pitch;
            ApplyLoopVolume(slot);
            slot.Source.Play();
            return new CaveLoopHandle(slot.Id);
        }

        private static LoopSlot AcquireLoopSlot()
        {
            if (loopSlots == null) return null;
            for (int i = 0; i < loopSlots.Length; i++)
                if (loopSlots[i] != null && !loopSlots[i].Active) return loopSlots[i];
            return null;
        }

        private static int NextLoopId()
        {
            nextLoopId++;
            if (nextLoopId == 0) nextLoopId++;
            return nextLoopId;
        }

        private static void ApplyLoopVolume(LoopSlot slot)
        {
            if (slot == null || slot.Source == null) return;
            slot.Source.volume = slot.VolumeScale * CaveAudioSettings.VolumeFor(slot.Channel);
        }

        private static void ApplyVoiceVolume(VoiceSlot voice)
        {
            if (voice == null || voice.Source == null) return;
            voice.Source.volume = voice.VolumeScale * CaveAudioSettings.VolumeFor(voice.Channel);
        }

        private static void ReleaseLoop(LoopSlot slot)
        {
            if (slot == null) return;
            if (slot.Source != null) ResetSource(slot.Source);
            slot.Id = 0;
            slot.Active = false;
            slot.Owner = null;
            slot.VolumeScale = 1f;
            slot.Channel = CaveAudioChannel.Sfx;
        }

        private static void StopAllLoopsInternal()
        {
            if (loopSlots == null) return;
            for (int i = 0; i < loopSlots.Length; i++) ReleaseLoop(loopSlots[i]);
        }

        private static void StopAllOneShots()
        {
            StopAllOneShots(worldVoices);
            StopAllOneShots(uiVoices);
        }

        private static void StopAllOneShots(VoiceSlot[] pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                VoiceSlot voice = pool[i];
                if (voice == null || voice.Source == null) continue;
                ResetSource(voice.Source);
                voice.CueIndex = -1;
                voice.Channel = CaveAudioChannel.Sfx;
                voice.VolumeScale = 1f;
            }
        }

        private static void ResetSource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            source.clip = null;
            source.loop = false;
            source.volume = 1f;
            source.pitch = 1f;
            source.spatialBlend = 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = PositionalMinDistance;
            source.maxDistance = PositionalMaxDistance;
            source.dopplerLevel = 0f;
            source.transform.localPosition = Vector3.zero;
        }

        private static void ConfigureSpatial(AudioSource source, bool positional, Vector3 worldPosition)
        {
            source.spatialBlend = positional ? PositionalSpatialBlend : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = PositionalMinDistance;
            source.maxDistance = PositionalMaxDistance;
            if (positional) source.transform.position = worldPosition;
        }

        private static bool OwnerIsActive(Object owner)
        {
            if (object.ReferenceEquals(owner, null)) return true;
            if (owner == null) return false;
            GameObject gameObject = owner as GameObject;
            if (gameObject == null)
            {
                Component component = owner as Component;
                if (component != null) gameObject = component.gameObject;
            }
            return gameObject == null || gameObject.activeInHierarchy;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            StopAllOneShots();
            StopAllLoopsInternal();
            EnsureSingleListener();
        }

        private static void HandleVolumesChanged()
        {
            ApplyVoiceVolumes(worldVoices);
            ApplyVoiceVolumes(uiVoices);
            if (loopSlots == null) return;
            for (int i = 0; i < loopSlots.Length; i++)
            {
                LoopSlot slot = loopSlots[i];
                if (slot != null && slot.Active) ApplyLoopVolume(slot);
            }
        }

        private static void ApplyVoiceVolumes(VoiceSlot[] pool)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                VoiceSlot voice = pool[i];
                if (voice != null && voice.Source != null && voice.Source.isPlaying)
                    ApplyVoiceVolume(voice);
            }
        }

        private static void EnsureSingleListener()
        {
            AudioListener[] listeners = Object.FindObjectsOfType<AudioListener>();
            if (listeners.Length == 1) return;
            if (listeners.Length > 1)
            {
                Debug.LogWarning("Multiple AudioListeners are active/configured. Cave preserved them for manual review.");
                return;
            }
            Camera targetCamera = Camera.main != null ? Camera.main : Object.FindObjectOfType<Camera>();
            if (targetCamera != null) targetCamera.gameObject.AddComponent<AudioListener>();
        }
    }

}
