using System.Collections;
using UnityEngine;

namespace Cave.Enemies
{
    /// <summary>Small actor-local frame player used exclusively by Anti-Pyre artillery cues.</summary>
    [DisallowMultipleComponent]
    public sealed class GothVfxPlayback : MonoBehaviour
    {
        [SerializeField] private string sortingLayer = "Default";
        [SerializeField] private int sortingOrder = 4;

        public void Play(Sprite[] frames, Vector3 position, float framesPerSecond, float scale, bool loop = false, float loopDuration = 0f)
        {
            if (frames == null || frames.Length == 0) return;
            SpriteRenderer renderer = CreateEffect("AntiPyre VFX", position, scale, Quaternion.identity);
            StartCoroutine(PlayFrames(renderer, frames, framesPerSecond, loop, loopDuration));
        }

        public void PlayTravel(Sprite[] frames, Vector3 from, Vector3 to, float duration, float framesPerSecond, float scale)
        {
            if (frames == null || frames.Length == 0) return;
            SpriteRenderer renderer = CreateEffect("AntiPyre VFX Travel", from, scale, Quaternion.identity);
            StartCoroutine(PlayTravelFrames(renderer, frames, from, to, duration, framesPerSecond));
        }

        public void PlayBeam(Sprite[] startup, Sprite[] loop, Sprite[] ending, Vector3 position, Vector2 direction,
            float framesPerSecond, float scale, float activeDuration)
        {
            if (!HasFrames(startup) && !HasFrames(loop) && !HasFrames(ending)) return;
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            SpriteRenderer renderer = CreateEffect("AntiPyre Beam", position, scale, Quaternion.Euler(0f, 0f, angle));
            StartCoroutine(PlayBeamFrames(renderer, startup, loop, ending, framesPerSecond, activeDuration));
        }

        private SpriteRenderer CreateEffect(string effectName, Vector3 position, float scale, Quaternion rotation)
        {
            GameObject effect = new GameObject(effectName);
            effect.transform.position = position;
            effect.transform.rotation = rotation;
            effect.transform.localScale = Vector3.one * Mathf.Max(.01f, scale);
            SpriteRenderer renderer = effect.AddComponent<SpriteRenderer>();
            renderer.sortingLayerName = sortingLayer;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static IEnumerator PlayFrames(SpriteRenderer renderer, Sprite[] frames, float fps, bool loop, float loopDuration)
        {
            float frameDuration = 1f / Mathf.Max(1f, fps);
            float end = loop ? Time.time + Mathf.Max(frameDuration, loopDuration) : 0f;
            int index = 0;
            while (renderer != null && (loop ? Time.time < end : index < frames.Length))
            {
                Sprite frame = frames[index % frames.Length];
                if (renderer.sprite != frame) renderer.sprite = frame;
                index++;
                yield return new WaitForSeconds(frameDuration);
            }
            if (renderer != null) Destroy(renderer.gameObject);
        }

        private static IEnumerator PlayTravelFrames(SpriteRenderer renderer, Sprite[] frames, Vector3 from, Vector3 to, float duration, float fps)
        {
            float safeDuration = Mathf.Max(.01f, duration);
            float frameDuration = 1f / Mathf.Max(1f, fps);
            float started = Time.time;
            int index = 0;
            while (renderer != null && Time.time - started < safeDuration)
            {
                renderer.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01((Time.time - started) / safeDuration));
                Sprite frame = frames[index % frames.Length];
                if (renderer.sprite != frame) renderer.sprite = frame;
                index++;
                yield return new WaitForSeconds(frameDuration);
            }
            if (renderer != null) Destroy(renderer.gameObject);
        }

        private static IEnumerator PlayBeamFrames(SpriteRenderer renderer, Sprite[] startup, Sprite[] loop, Sprite[] ending, float fps, float activeDuration)
        {
            yield return PlaySequence(renderer, startup, fps, 0f);
            yield return PlaySequence(renderer, loop, fps, Mathf.Max(0f, activeDuration));
            yield return PlaySequence(renderer, ending, fps, 0f);
            if (renderer != null) Destroy(renderer.gameObject);
        }

        // A duration of zero means one complete pass; a positive duration means loop.
        private static IEnumerator PlaySequence(SpriteRenderer renderer, Sprite[] frames, float fps, float loopDuration)
        {
            if (!HasFrames(frames)) yield break;
            float frameDuration = 1f / Mathf.Max(1f, fps);
            float end = loopDuration > 0f ? Time.time + loopDuration : 0f;
            int index = 0;
            while (renderer != null && (loopDuration > 0f ? Time.time < end : index < frames.Length))
            {
                Sprite frame = frames[index % frames.Length];
                if (renderer.sprite != frame) renderer.sprite = frame;
                index++;
                yield return new WaitForSeconds(frameDuration);
            }
        }

        private static bool HasFrames(Sprite[] frames) => frames != null && frames.Length > 0;
    }
}
