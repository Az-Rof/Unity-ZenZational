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

        public string framePrefix;
        public float FPS = 12f;
        public bool loop = true;

        [HideInInspector]
        public Sprite[] frames;
    }

    public SpriteRenderer spriteRenderer;
    public List<AnimationData> animations = new List<AnimationData>();

    private AnimationData currentAnimation;
    private Coroutine animationCoroutine;

    void Awake()
    {
        LoadAllAnimations();
    }

    void Start()
    {
        // Test animation
        if (animations.Count > 0)
        {
            PlayAnimation(animations[0].name);
        }
    }

    void LoadAllAnimations()
    {
        foreach (AnimationData animation in animations)
        {
            Sprite[] loadedSprites =
                Resources.LoadAll<Sprite>(animation.resourceDirectory);

            Debug.Log(
                $"Loaded {loadedSprites.Length} sprites from " +
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

            validFrames.Sort((a, b) =>
                ExtractNumber(a.name, animation.framePrefix)
                .CompareTo(
                ExtractNumber(b.name, animation.framePrefix))
            );

            animation.frames = validFrames.ToArray();

            Debug.Log(
                $"Animation '{animation.name}' found " +
                $"{animation.frames.Length} frames."
            );

            foreach (Sprite frame in animation.frames)
            {
                Debug.Log("  " + frame.name);
            }
        }
    }

    int ExtractNumber(string name, string prefix)
    {
        string number = name.Substring(prefix.Length);

        if (int.TryParse(number, out int result))
            return result;

        Debug.LogWarning(
            $"Couldn't extract frame number from {name}"
        );

        return 0;
    }

    public void PlayAnimation(string animationName)
    {
        AnimationData animation =
            animations.Find(x => x.name == animationName);

        if (animation == null)
        {
            Debug.LogError(
                $"Animation '{animationName}' doesn't exist!"
            );

            return;
        }

        if (animation.frames == null || animation.frames.Length == 0)
        {
            Debug.LogError(
                $"Animation '{animationName}' has no frames!"
            );

            return;
        }

        // Don't restart the same animation
        if (currentAnimation == animation)
            return;

        currentAnimation = animation;

        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);

        animationCoroutine =
            StartCoroutine(PlayAnimationCoroutine(animation));
    }

    IEnumerator PlayAnimationCoroutine(AnimationData animation)
    {
        float frameTime = 1f / animation.FPS;

        do
        {
            foreach (Sprite frame in animation.frames)
            {
                spriteRenderer.sprite = frame;

                yield return new WaitForSeconds(frameTime);
            }

        } while (animation.loop);
    }

    public void StopAnimation()
    {
        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }
    }
}