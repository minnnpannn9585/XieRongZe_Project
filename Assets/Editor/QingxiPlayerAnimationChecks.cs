using System;
using System.Collections.Generic;
using System.Linq;
using Qingxi;
using UnityEditor;
using UnityEngine;

namespace Qingxi.Editor
{
    public static class QingxiPlayerAnimationChecks
    {
        static int stage;
        static PlayerMovement player;
        static Rigidbody2D body;
        static PlayerWalkAnimation driver;
        static SpriteRenderer illustration;
        static Vector2 savedPosition;
        static Vector3 savedScale;
        static readonly HashSet<Sprite> observed = new HashSet<Sprite>();

        public static void Validate(PlayerMovement movement)
        {
            var animation = movement.GetComponent<PlayerWalkAnimation>();
            Require(animation != null && animation.Animator != null && animation.Animator.runtimeAnimatorController != null, "Missing saved walk Animator");
            Require(!animation.Animator.applyRootMotion, "Sprite animation must not move the player root");
            var frames = QingxiPlayerAnimationBuilder.Frames();
            Require(frames.Length == 4 && frames.Distinct().Count() == 4 && frames.All(AssetDatabase.Contains), "Four persistent walk frames required");
            Require(frames.All(f => Mathf.Approximately(f.bounds.size.y, frames[0].bounds.size.y)), "Walk frame sizes change");
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(QingxiPlayerAnimationBuilder.WalkPath);
            Require(clip != null && clip.isLooping, "Walk clip must loop");
            Require(AnimationUtility.GetCurveBindings(clip).Length == 0, "Walk animation contains transform/bobbing curves");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            Require(binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Sprite", "Walk clip animates more than sprite frames");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Require(keys.Select(k => k.value).SequenceEqual(frames.Cast<UnityEngine.Object>().Concat(new[] { (UnityEngine.Object)frames[0] })), "Incorrect four-frame sequence");
            Require(movement.GetComponentInChildren<SpriteRenderer>().transform.localPosition == Vector3.zero, "Player illustration offset still bobs");
        }

        /// <summary>Exercises the real Animator while Rigidbody2D moves on clear land in batch Play Mode.</summary>
        public static bool Advance(QingxiGame game)
        {
            switch (stage++)
            {
                case 0:
                    var world = UnityEngine.Object.FindAnyObjectByType<TownExplorer>();
                    player = world.Player;
                    Validate(player);
                    driver = player.GetComponent<PlayerWalkAnimation>();
                    body = player.GetComponent<Rigidbody2D>();
                    illustration = player.GetComponentInChildren<SpriteRenderer>();
                    Require(!driver.IsWalking, "Stationary player starts walking");
                    savedPosition = body.position; savedScale = illustration.transform.localScale;
                    Vector2 testPosition = new Vector2(-7, -4.5f);
                    Require(QingxiSceneChecks.IsFree(world, testPosition), "Animation test must start on clear land");
                    player.enabled = false; body.position = testPosition; Physics2D.SyncTransforms();
                    observed.Clear(); EditorApplication.update += Observe;
                    body.linearVelocity = Vector2.right * player.Speed;
                    return false;
                case 1:
                    Require(driver.IsWalking && driver.Animator.GetCurrentAnimatorStateInfo(0).IsName("Walk"), "Moving body did not enter Walk");
                    Require(observed.Count == 4, "Real Animator did not play all four frames: " + observed.Count);
                    Require(!illustration.flipX, "Rightward walking faces left");
                    CheckAnchor(); body.linearVelocity = Vector2.zero;
                    return false;
                case 2:
                    Require(!driver.IsWalking && driver.Animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), "Stationary body did not stop walking");
                    Require(illustration.sprite == QingxiPlayerAnimationBuilder.Frames()[1], "Stopped player did not return to idle pose");
                    CheckAnchor(); body.linearVelocity = Vector2.left * player.Speed;
                    return false;
                case 3:
                    Require(driver.IsWalking && illustration.flipX, "Leftward walking did not mirror the character");
                    CheckAnchor(); body.linearVelocity = Vector2.zero; body.position = savedPosition;
                    Physics2D.SyncTransforms(); player.enabled = true; StopObserving();
                    Debug.Log("WALK CHECK: four rendered frames / real movement / idle on stop / left-right facing / stable visual anchor passed.");
                    return false;
                default: return true;
            }
        }

        static void Observe() { if (driver != null && driver.IsWalking && illustration != null) observed.Add(illustration.sprite); }
        public static void StopObserving() => EditorApplication.update -= Observe;
        static void CheckAnchor()
        {
            Require(illustration.transform.localPosition == Vector3.zero && illustration.transform.localScale == savedScale,
                "Walking changes the visual position or scale");
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
