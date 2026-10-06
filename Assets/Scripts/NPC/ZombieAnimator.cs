using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class ZombieAnimator : MonoBehaviour
{
    [Serializable]
    public class AnimationData
    {
        public string name;

        [Tooltip("Path inside Resources folder, without Resources/")]
        public string resourceDirectory;

        [Tooltip("Beginning of the frame filename, e.g. ZombieWalk")]
        public string framePrefix;

        [Min(1f)]
        public float FPS = 12f;

        public bool loop = true;

        [HideInInspector]
        public Sprite[] frames;
    }

    [Header("References")]
    public SpriteRenderer spriteRenderer;

    [Header("Animations")]
    public List<AnimationData> animations = new List<AnimationData>();

    private AnimationData currentAnimation;
    private Coroutine animationCoroutine;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        LoadAllAnimations();
    }

    private void Start()
    {
        if (animations.Count > 0)
        {
            PlayAnimation(animations[0].name);
        }
    }

    // =========================================================
    // LOAD ANIMATIONS
    // =========================================================

    private void LoadAllAnimations()
    {
        foreach (AnimationData animation in animations)
        {
            Sprite[] loadedSprites =
                Resources.LoadAll<Sprite>(animation.resourceDirectory);

            Debug.Log(
                $"[{name}] Loaded {loadedSprites.Length} sprites from " +
                $"Resources/{animation.resourceDirectory}"
            );

            List<Sprite> validFrames = new List<Sprite>();

            foreach (Sprite sprite in loadedSprites)
            {
                if (sprite.name.StartsWith(animation.framePrefix))
                {
                    validFrames.Add(sprite);
                }
            }


            // Sort frames numerically based on the number
            // at the end of their filename.
            validFrames.Sort((a, b) =>
            {
                int numberA = ExtractNumber(a.name, animation.framePrefix);
                int numberB = ExtractNumber(b.name, animation.framePrefix);

                return numberA.CompareTo(numberB);
            });

            animation.frames = validFrames.ToArray();

            Debug.Log(
                $"[{name}] Animation '{animation.name}' found " +
                $"{animation.frames.Length} frames."
            );

            // Print frame order for debugging
            for (int i = 0; i < animation.frames.Length; i++)
            {
                Debug.Log(
                    $"[{animation.name}] Frame {i}: " +
                    animation.frames[i].name
                );
            }
        }
    }

    int ExtractNumber(string fileName, string prefix)
    {
        string numberPart = fileName.Substring(prefix.Length);

        int underscoreIndex = numberPart.IndexOf('_');

        if (underscoreIndex >= 0)
        {
            numberPart = numberPart.Substring(0, underscoreIndex);
        }

        if (int.TryParse(numberPart, out int result))
        {
            return result;
        }

        Debug.LogWarning(
            $"Couldn't extract frame number from '{fileName}'"
        );

        return 0;
    }

    // =========================================================
    // PLAY ANIMATION
    // =========================================================

    public void PlayAnimation(string animationName)
    {
        PlayAnimation(animationName, false);
    }

    public void PlayAnimation(string animationName, bool forceRestart)
    {

        AnimationData animation =
            animations.Find(x => x.name == animationName);

        if (animation == null)
        {
            Debug.LogError(
                $"[{name}] Animation '{animationName}' doesn't exist!"
            );

            return;
        }

        if (animation.frames == null ||
            animation.frames.Length == 0)
        {
            Debug.LogError(
                $"[{name}] Animation '{animationName}' has no frames!"
            );

            return;
        }

        // Don't restart the same animation unless
        // forceRestart is true.
        if (currentAnimation == animation && !forceRestart)
        {
            return;
        }

        // Stop previous animation
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        currentAnimation = animation;

        animationCoroutine =
            StartCoroutine(PlayAnimationCoroutine(animation));
    }

    // =========================================================
    // ANIMATION PLAYBACK
    // =========================================================

    private IEnumerator PlayAnimationCoroutine(AnimationData animation)
    {
        if (animation.FPS <= 0)
        {
            Debug.LogError(
                $"[{name}] Animation '{animation.name}' has an FPS <= 0!"
            );

            yield break;
        }

        float frameDuration = 1f / animation.FPS;

        int currentFrame = 0;

        float timer = 0f;

        // Immediately display the first frame.
        spriteRenderer.sprite = animation.frames[currentFrame];

        while (true)
        {
            timer += Time.deltaTime;

            if (timer >= frameDuration)
            {
                // Account for excess time so the animation
                // doesn't slowly drift.
                timer -= frameDuration;

                currentFrame++;

                // Reached the end of the animation
                if (currentFrame >= animation.frames.Length)
                {
                    if (animation.loop)
                    {
                        currentFrame = 0;
                    }
                    else
                    {
                        // Hold on the final frame.
                        currentFrame =
                            animation.frames.Length - 1;

                        currentAnimation = null;
                        animationCoroutine = null;

                        yield break;
                    }
                }

                spriteRenderer.sprite =
                    animation.frames[currentFrame];
            }

            yield return null;
        }
    }

    // =========================================================
    // STOP
    // =========================================================

    public void StopAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);

            animationCoroutine = null;
        }

        currentAnimation = null;
    }

    // =========================================================
    // GET CURRENT ANIMATION
    // =========================================================

    public string GetCurrentAnimation()
    {
        if (currentAnimation == null)
        {
            return "";
        }

        return currentAnimation.name;
    }

    public bool IsPlaying()
    {
        return currentAnimation != null;
    }
}