using UnityEngine;
using UnityEngine.Playables;
using System;
using System.Collections;

// -----------------------------------------------------------------
// Which animation to play for a given (page, object) pairing.
// Lives on the manager's per-page entries, NOT on the click/drag
// object itself - so the same object can have a different one of
// these per page (e.g. beaker plays anim A on page 2, anim B on
// page 5).
//
// Play() is the single shared playback+completion-wait routine,
// used by ClickAnimManager, DragDropAnimManager, and
// PageEnterAnimManager alike - one implementation, no duplicated
// logic to keep in sync between the three.
// -----------------------------------------------------------------
[System.Serializable]
public class AnimationSource
{
    [Tooltip("Use this OR the Animator below, not both.")]
    public PlayableDirector director;

    [Header("Animator (clip-driven)")]
    public Animator animator;
    [Tooltip("Drag the AnimationClip to play. Its name is used to call animator.Play(clip.name) - so a state with a matching name must exist in the Animator Controller.")]
    public AnimationClip clip;

    // "runner" is whatever MonoBehaviour should own the coroutine
    // (needs to be something active in the scene - pass "this" from
    // the calling manager, NOT the clicked/dragged object itself,
    // in case that object gets deactivated mid-animation).
    public IEnumerator Play(MonoBehaviour runner, Action onComplete)
    {
        if (director != null)
        {
            bool done = false;
            void Handler(PlayableDirector d) { done = true; Debug.Log($"[AnimationSource] director '{director.gameObject.name}' fired stopped event."); }
            director.stopped += Handler;

            Debug.Log($"[AnimationSource] Playing director '{director.gameObject.name}': state={director.state}, duration={director.duration}, playableAsset={(director.playableAsset != null ? director.playableAsset.name : "NULL")}");

            director.Play();

            Debug.Log($"[AnimationSource] After Play() call: state={director.state}, time={director.time}");

            while (!done) yield return null;
            director.stopped -= Handler;
        }
        else if (animator != null && clip != null)
        {
            animator.Play(clip.name, 0, 0f);

            // Wait one frame so the new state actually takes effect
            // before we start checking progress against it.
            yield return null;

            var stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (!stateInfo.IsName(clip.name))
            {
                Debug.LogWarning($"[AnimationSource] {runner.name}: Animator has no state named '{clip.name}' (must match the clip name exactly). Falling back to a fixed wait of {clip.length}s based on the clip's length.");
                yield return new WaitForSeconds(clip.length);
            }
            else
            {
                while (animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
                    yield return null;
            }
        }
        else
        {
            Debug.LogWarning($"[AnimationSource] {runner.name}: AnimationSource has neither a PlayableDirector nor an Animator+clip assigned - completing immediately.");
        }

        onComplete?.Invoke();
    }

    public bool IsValid => director != null || (animator != null && clip != null);
}