#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AT_Murayama.PlayerAnimation.Editor
{
    /// <summary>
    /// RacerAnimationProfile の設定から AnimatorController を完全自動構築・更新するビルダー。
    /// ・既存 .controller アセットがある場合は削除せずインプレースで中身を再構築するため、GUID参照切れが起きません。
    /// ・案A（DiscreteStates：ステート遷移）と案B（BlendTree：1D左右 / 2D上下左右自動拡張）を1クリックで切替再構築できます。
    /// ・旧 BlendTree サブアセットのゴミが残らないよう自動クリーンアップします。
    /// </summary>
    public static class RacerAnimatorBuilder
    {
        public static AnimatorController BuildOrUpdateController(RacerAnimationProfile profile, out string summary)
        {
            summary = string.Empty;
            if (profile == null)
            {
                summary = "エラー: Profile が未設定です。";
                return null;
            }

            string folder = profile.GeneratedAssetFolder;
            RacerAnimationClipUtility.EnsureAssetFolderExists(folder);

            string controllerPath = $"{folder}/{profile.ControllerAssetName}.controller";
            AnimatorController controller = ResolveOrCreateController(profile, controllerPath);
            if (controller == null)
            {
                summary = $"エラー: AnimatorController の作成に失敗しました ({controllerPath})";
                return null;
            }

            // 1. 既存の中身と不要な BlendTree サブアセットを完全クリーンアップ（GUIDは維持）
            ClearControllerContents(controller, controllerPath);

            // 2. パラメータを標準定義で再登録
            AddStandardParameters(controller);

            // 3. Base Layer 取得＆構築
            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            sm.entryPosition = new Vector3(0f, -120f, 0f);
            sm.anyStatePosition = new Vector3(-280f, 240f, 0f);
            sm.exitPosition = new Vector3(560f, -120f, 0f);

            AnimationClip flyFallback = profile.FlyClip;
            AnimationClip setUpClip = profile.SetUpClip != null ? profile.SetUpClip : flyFallback;
            AnimationClip takeOffClip = profile.TakeOffClip;
            AnimationClip rightClip = profile.RightCurveClip != null ? profile.RightCurveClip : flyFallback;
            AnimationClip leftClip = profile.LeftCurveClip != null ? profile.LeftCurveClip : flyFallback;
            AnimationClip stunClip = profile.StunClip != null ? profile.StunClip : flyFallback;
            AnimationClip fireClip = profile.FireClip != null ? profile.FireClip : flyFallback;

            // --- SetUp（デフォルトステート）---
            AnimatorState setUpState = sm.AddState(RacerAnimationProfile.StateSetUp, new Vector3(0f, 0f, 0f));
            setUpState.motion = setUpClip;
            setUpState.writeDefaultValues = true;
            sm.defaultState = setUpState;

            // --- TakeOff（飛行まで補間）---
            AnimatorState takeOffState = null;
            if (takeOffClip != null)
            {
                takeOffState = sm.AddState(RacerAnimationProfile.StateTakeOff, new Vector3(0f, 90f, 0f));
                takeOffState.motion = takeOffClip;
                takeOffState.writeDefaultValues = true;
            }

            // --- メイン飛行ステート（案A: Fly / 案B: FlightBlend）---
            AnimatorState mainFlightState;
            string modeLabel;

            if (profile.TurnMode == FlightTurnAnimationMode.BlendTree)
            {
                mainFlightState = BuildBlendTreeFlightState(controller, sm, profile, flyFallback, leftClip, rightClip, out modeLabel);
            }
            else
            {
                mainFlightState = BuildDiscreteStatesFlight(sm, profile, flyFallback, leftClip, rightClip, out modeLabel);
            }

            // --- SetUp → TakeOff → MainFlight の導入フロー接続 ---
            if (takeOffState != null)
            {
                // GO! Trigger で即時 TakeOff
                AnimatorStateTransition trStart = setUpState.AddTransition(takeOffState);
                ConfigureImmediateTransition(trStart, profile.SetUpToTakeOffDuration);
                trStart.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamStartPlay);

                // 安全網：Trigger 取りこぼし時でも IsRacing == true なら TakeOff へ進む
                AnimatorStateTransition trFallback = setUpState.AddTransition(takeOffState);
                ConfigureImmediateTransition(trFallback, profile.SetUpToTakeOffDuration);
                trFallback.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamIsRacing);

                // TakeOff 再生完了で自動的に MainFlight へ
                AnimatorStateTransition trTakeOffToFly = takeOffState.AddTransition(mainFlightState);
                trTakeOffToFly.hasExitTime = true;
                trTakeOffToFly.exitTime = profile.TakeOffExitTime;
                trTakeOffToFly.hasFixedDuration = true;
                trTakeOffToFly.duration = profile.TakeOffToFlyDuration;
                trTakeOffToFly.canTransitionToSelf = false;
            }
            else
            {
                AnimatorStateTransition trDirect = setUpState.AddTransition(mainFlightState);
                ConfigureImmediateTransition(trDirect, profile.SetUpToTakeOffDuration);
                trDirect.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamIsRacing);
            }

            // --- Stun（最優先割り込み：AnyState → Stun → MainFlight）---
            AnimatorState stunState = sm.AddState(RacerAnimationProfile.StateStun, new Vector3(-280f, 350f, 0f));
            stunState.motion = stunClip;
            stunState.writeDefaultValues = true;

            AnimatorStateTransition trAnyToStun = sm.AddAnyStateTransition(stunState);
            ConfigureImmediateTransition(trAnyToStun, profile.StunTransitionDuration);
            trAnyToStun.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamIsStunned);

            AnimatorStateTransition trStunToFlight = stunState.AddTransition(mainFlightState);
            ConfigureImmediateTransition(trStunToFlight, profile.StunTransitionDuration);
            trStunToFlight.AddCondition(AnimatorConditionMode.IfNot, 0f, RacerAnimationProfile.ParamIsStunned);

            // --- Fire（弾発射ワンショット：MainFlight/旋回 → Fire → HasExitTime で MainFlight）---
            AnimatorState fireState = sm.AddState(RacerAnimationProfile.StateFire, new Vector3(280f, 350f, 0f));
            fireState.motion = fireClip;
            fireState.writeDefaultValues = true;

            AnimatorStateTransition trFlightToFire = mainFlightState.AddTransition(fireState);
            ConfigureImmediateTransition(trFlightToFire, profile.FireTransitionDuration);
            trFlightToFire.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamFire);
            trFlightToFire.AddCondition(AnimatorConditionMode.IfNot, 0f, RacerAnimationProfile.ParamIsStunned);

            // 案Aの左右旋回ステート中からも弾発射可能にする
            if (profile.TurnMode == FlightTurnAnimationMode.DiscreteStates)
            {
                AddFireTransitionIfStateExists(sm, RacerAnimationProfile.StateLeftCurve, fireState, profile.FireTransitionDuration);
                AddFireTransitionIfStateExists(sm, RacerAnimationProfile.StateRightCurve, fireState, profile.FireTransitionDuration);
                AddFireTransitionIfStateExists(sm, RacerAnimationProfile.StateMoveUp, fireState, profile.FireTransitionDuration);
                AddFireTransitionIfStateExists(sm, RacerAnimationProfile.StateMoveDown, fireState, profile.FireTransitionDuration);
            }

            AnimatorStateTransition trFireToFlight = fireState.AddTransition(mainFlightState);
            trFireToFlight.hasExitTime = true;
            trFireToFlight.exitTime = profile.FireExitTime;
            trFireToFlight.hasFixedDuration = true;
            trFireToFlight.duration = profile.FireTransitionDuration;
            trFireToFlight.canTransitionToSelf = false;

            // --- 拡張：Boost / Shield（クリップ設定時のみステート追加）---
            int optionalCount = 0;
            if (profile.HasBoostClip)
            {
                AnimatorState boostState = sm.AddState(RacerAnimationProfile.StateBoost, new Vector3(360f, 180f, 0f));
                boostState.motion = profile.BoostClip;
                boostState.writeDefaultValues = true;

                AnimatorStateTransition trToBoost = mainFlightState.AddTransition(boostState);
                ConfigureImmediateTransition(trToBoost, profile.OptionalStateTransitionDuration);
                trToBoost.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamIsBoosting);
                trToBoost.AddCondition(AnimatorConditionMode.IfNot, 0f, RacerAnimationProfile.ParamIsStunned);

                AnimatorStateTransition trFromBoost = boostState.AddTransition(mainFlightState);
                ConfigureImmediateTransition(trFromBoost, profile.OptionalStateTransitionDuration);
                trFromBoost.AddCondition(AnimatorConditionMode.IfNot, 0f, RacerAnimationProfile.ParamIsBoosting);
                optionalCount++;
            }

            if (profile.HasShieldClip)
            {
                AnimatorState shieldState = sm.AddState(RacerAnimationProfile.StateShield, new Vector3(360f, 250f, 0f));
                shieldState.motion = profile.ShieldClip;
                shieldState.writeDefaultValues = true;

                AnimatorStateTransition trToShield = mainFlightState.AddTransition(shieldState);
                ConfigureImmediateTransition(trToShield, profile.OptionalStateTransitionDuration);
                trToShield.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamIsShielding);
                trToShield.AddCondition(AnimatorConditionMode.IfNot, 0f, RacerAnimationProfile.ParamIsStunned);

                AnimatorStateTransition trFromShield = shieldState.AddTransition(mainFlightState);
                ConfigureImmediateTransition(trFromShield, profile.OptionalStateTransitionDuration);
                trFromShield.AddCondition(AnimatorConditionMode.IfNot, 0f, RacerAnimationProfile.ParamIsShielding);
                optionalCount++;
            }

            profile.GeneratedController = controller;
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            string actualPath = AssetDatabase.GetAssetPath(controller);
            summary = $"✅ AnimatorController を構築・更新しました（GUID維持）\n" +
                      $"・パス: {actualPath}\n" +
                      $"・方式: {modeLabel}\n" +
                      $"・ステート数: {sm.states.Length} / 拡張(Boost/Shield): {optionalCount}件\n" +
                      $"・未着枠プレースホルダー: Stun={(profile.HasStunClip ? "設定済" : "待機枠")}, Fire={(profile.HasFireClip ? "設定済" : "待機枠")}";
            return controller;
        }

        // ============================================================
        // 案A：ステート遷移モード（Fly ⇄ LeftCurve / RightCurve [+ MoveUp / MoveDown]）
        // ============================================================
        private static AnimatorState BuildDiscreteStatesFlight(
            AnimatorStateMachine sm,
            RacerAnimationProfile profile,
            AnimationClip flyClip,
            AnimationClip leftClip,
            AnimationClip rightClip,
            out string modeLabel)
        {
            AnimatorState flyState = sm.AddState(RacerAnimationProfile.StateFly, new Vector3(0f, 200f, 0f));
            flyState.motion = flyClip;
            flyState.writeDefaultValues = true;

            AnimatorState leftState = sm.AddState(RacerAnimationProfile.StateLeftCurve, new Vector3(-110f, 350f, 0f));
            leftState.motion = leftClip;
            leftState.writeDefaultValues = true;

            AnimatorState rightState = sm.AddState(RacerAnimationProfile.StateRightCurve, new Vector3(110f, 350f, 0f));
            rightState.motion = rightClip;
            rightState.writeDefaultValues = true;

            float blend = profile.CurveTransitionDuration;

            // Fly ⇄ LeftCurve
            AnimatorStateTransition trFlyToLeft = flyState.AddTransition(leftState);
            ConfigureImmediateTransition(trFlyToLeft, blend);
            trFlyToLeft.AddCondition(AnimatorConditionMode.Equals, -1f, RacerAnimationProfile.ParamTurnDir);

            AnimatorStateTransition trLeftToFly = leftState.AddTransition(flyState);
            ConfigureImmediateTransition(trLeftToFly, blend);
            trLeftToFly.AddCondition(AnimatorConditionMode.Equals, 0f, RacerAnimationProfile.ParamTurnDir);

            // Fly ⇄ RightCurve
            AnimatorStateTransition trFlyToRight = flyState.AddTransition(rightState);
            ConfigureImmediateTransition(trFlyToRight, blend);
            trFlyToRight.AddCondition(AnimatorConditionMode.Equals, 1f, RacerAnimationProfile.ParamTurnDir);

            AnimatorStateTransition trRightToFly = rightState.AddTransition(flyState);
            ConfigureImmediateTransition(trRightToFly, blend);
            trRightToFly.AddCondition(AnimatorConditionMode.Equals, 0f, RacerAnimationProfile.ParamTurnDir);

            // LeftCurve ⇄ RightCurve の直接切り返し（素早い左右入力対応）
            AnimatorStateTransition trLeftToRight = leftState.AddTransition(rightState);
            ConfigureImmediateTransition(trLeftToRight, blend);
            trLeftToRight.AddCondition(AnimatorConditionMode.Equals, 1f, RacerAnimationProfile.ParamTurnDir);

            AnimatorStateTransition trRightToLeft = rightState.AddTransition(leftState);
            ConfigureImmediateTransition(trRightToLeft, blend);
            trRightToLeft.AddCondition(AnimatorConditionMode.Equals, -1f, RacerAnimationProfile.ParamTurnDir);

            // 将来拡張：MoveUp / MoveDown（クリップが設定された場合のみステート＆遷移を自動追加）
            int verticalCount = 0;
            if (profile.HasMoveUpClip)
            {
                AnimatorState upState = sm.AddState(RacerAnimationProfile.StateMoveUp, new Vector3(-280f, 150f, 0f));
                upState.motion = profile.MoveUpClip;
                upState.writeDefaultValues = true;

                AnimatorStateTransition trFlyToUp = flyState.AddTransition(upState);
                ConfigureImmediateTransition(trFlyToUp, blend);
                trFlyToUp.AddCondition(AnimatorConditionMode.Equals, 0f, RacerAnimationProfile.ParamTurnDir);
                trFlyToUp.AddCondition(AnimatorConditionMode.Equals, 1f, RacerAnimationProfile.ParamVerticalDir);

                AnimatorStateTransition trUpToFly1 = upState.AddTransition(flyState);
                ConfigureImmediateTransition(trUpToFly1, blend);
                trUpToFly1.AddCondition(AnimatorConditionMode.NotEqual, 1f, RacerAnimationProfile.ParamVerticalDir);

                AnimatorStateTransition trUpToFly2 = upState.AddTransition(flyState);
                ConfigureImmediateTransition(trUpToFly2, blend);
                trUpToFly2.AddCondition(AnimatorConditionMode.NotEqual, 0f, RacerAnimationProfile.ParamTurnDir);
                verticalCount++;
            }

            if (profile.HasMoveDownClip)
            {
                AnimatorState downState = sm.AddState(RacerAnimationProfile.StateMoveDown, new Vector3(-280f, 210f, 0f));
                downState.motion = profile.MoveDownClip;
                downState.writeDefaultValues = true;

                AnimatorStateTransition trFlyToDown = flyState.AddTransition(downState);
                ConfigureImmediateTransition(trFlyToDown, blend);
                trFlyToDown.AddCondition(AnimatorConditionMode.Equals, 0f, RacerAnimationProfile.ParamTurnDir);
                trFlyToDown.AddCondition(AnimatorConditionMode.Equals, -1f, RacerAnimationProfile.ParamVerticalDir);

                AnimatorStateTransition trDownToFly1 = downState.AddTransition(flyState);
                ConfigureImmediateTransition(trDownToFly1, blend);
                trDownToFly1.AddCondition(AnimatorConditionMode.NotEqual, -1f, RacerAnimationProfile.ParamVerticalDir);

                AnimatorStateTransition trDownToFly2 = downState.AddTransition(flyState);
                ConfigureImmediateTransition(trDownToFly2, blend);
                trDownToFly2.AddCondition(AnimatorConditionMode.NotEqual, 0f, RacerAnimationProfile.ParamTurnDir);
                verticalCount++;
            }

            modeLabel = verticalCount > 0
                ? $"案A：ステート遷移（左右＋上下拡張 {verticalCount}種）"
                : "案A：ステート遷移（Fly ⇄ LeftCurve / RightCurve）";
            return flyState;
        }

        // ============================================================
        // 案B：BlendTree モード（1D 左右 / 上下クリップ設定時は自動で 2D 拡張）
        // ============================================================
        private static AnimatorState BuildBlendTreeFlightState(
            AnimatorController controller,
            AnimatorStateMachine sm,
            RacerAnimationProfile profile,
            AnimationClip flyClip,
            AnimationClip leftClip,
            AnimationClip rightClip,
            out string modeLabel)
        {
            AnimatorState blendState = sm.AddState(RacerAnimationProfile.StateFlightBlend, new Vector3(0f, 200f, 0f));
            blendState.writeDefaultValues = true;

            BlendTree tree = new BlendTree
            {
                name = "FlightBlendTree",
                useAutomaticThresholds = false,
                hideFlags = HideFlags.HideInHierarchy
            };

            if (profile.HasVerticalClips)
            {
                tree.blendType = BlendTreeType.FreeformDirectional2D;
                tree.blendParameter = RacerAnimationProfile.ParamMoveX;
                tree.blendParameterY = RacerAnimationProfile.ParamMoveY;

                tree.AddChild(flyClip, new Vector2(0f, 0f));
                tree.AddChild(leftClip, new Vector2(-1f, 0f));
                tree.AddChild(rightClip, new Vector2(1f, 0f));
                tree.AddChild(profile.MoveUpClip != null ? profile.MoveUpClip : flyClip, new Vector2(0f, 1f));
                tree.AddChild(profile.MoveDownClip != null ? profile.MoveDownClip : flyClip, new Vector2(0f, -1f));

                modeLabel = "案B：2D BlendTree（MoveX 左右 ＋ MoveY 上下連続ブレンド）";
            }
            else
            {
                tree.blendType = BlendTreeType.Simple1D;
                tree.blendParameter = RacerAnimationProfile.ParamMoveX;

                tree.AddChild(leftClip, -1f);
                tree.AddChild(flyClip, 0f);
                tree.AddChild(rightClip, 1f);

                modeLabel = "案B：1D BlendTree（MoveX -1:Left / 0:Fly / +1:Right 連続ブレンド）";
            }

            AssetDatabase.AddObjectToAsset(tree, controller);
            blendState.motion = tree;
            return blendState;
        }

        // ============================================================
        // 内部ヘルパー
        // ============================================================
        private static AnimatorController ResolveOrCreateController(RacerAnimationProfile profile, string targetPath)
        {
            // 既に Profile が参照している AnimatorController がプロジェクト内にあれば最優先で再利用（GUID維持）
            if (profile.GeneratedController is AnimatorController linkedController)
            {
                string linkedPath = AssetDatabase.GetAssetPath(linkedController);
                if (!string.IsNullOrEmpty(linkedPath))
                {
                    return linkedController;
                }
            }

            AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(targetPath);
            if (existing != null)
            {
                return existing;
            }

            return AnimatorController.CreateAnimatorControllerAtPath(targetPath);
        }

        private static void ClearControllerContents(AnimatorController controller, string controllerPath)
        {
            string actualPath = AssetDatabase.GetAssetPath(controller);
            if (string.IsNullOrEmpty(actualPath))
            {
                actualPath = controllerPath;
            }

            // パラメータを全削除
            while (controller.parameters.Length > 0)
            {
                controller.RemoveParameter(0);
            }

            // 余分なレイヤーを削除し、レイヤー0を必ず確保
            while (controller.layers.Length > 1)
            {
                controller.RemoveLayer(controller.layers.Length - 1);
            }

            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base Layer");
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;

            // AnyState / Entry 遷移を全削除
            AnimatorStateTransition[] anyTransitions = sm.anyStateTransitions;
            for (int i = anyTransitions.Length - 1; i >= 0; i--)
            {
                sm.RemoveAnyStateTransition(anyTransitions[i]);
            }

            AnimatorTransition[] entryTransitions = sm.entryTransitions;
            for (int i = entryTransitions.Length - 1; i >= 0; i--)
            {
                sm.RemoveEntryTransition(entryTransitions[i]);
            }

            // 全ステートを削除
            ChildAnimatorState[] states = sm.states;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                sm.RemoveState(states[i].state);
            }

            // 全サブステートマシンを削除
            ChildAnimatorStateMachine[] subMachines = sm.stateMachines;
            for (int i = subMachines.Length - 1; i >= 0; i--)
            {
                sm.RemoveStateMachine(subMachines[i].stateMachine);
            }

            // 旧 BlendTree サブアセットのゴミをクリーンアップ
            if (!string.IsNullOrEmpty(actualPath))
            {
                UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(actualPath);
                for (int i = 0; i < subAssets.Length; i++)
                {
                    if (subAssets[i] is BlendTree)
                    {
                        UnityEngine.Object.DestroyImmediate(subAssets[i], allowDestroyingAssets: true);
                    }
                }
            }
        }

        private static void AddStandardParameters(AnimatorController controller)
        {
            controller.AddParameter(RacerAnimationProfile.ParamIsRacing, AnimatorControllerParameterType.Bool);
            controller.AddParameter(RacerAnimationProfile.ParamStartPlay, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(RacerAnimationProfile.ParamMoveX, AnimatorControllerParameterType.Float);
            controller.AddParameter(RacerAnimationProfile.ParamMoveY, AnimatorControllerParameterType.Float);
            controller.AddParameter(RacerAnimationProfile.ParamTurnDir, AnimatorControllerParameterType.Int);
            controller.AddParameter(RacerAnimationProfile.ParamVerticalDir, AnimatorControllerParameterType.Int);
            controller.AddParameter(RacerAnimationProfile.ParamIsStunned, AnimatorControllerParameterType.Bool);
            controller.AddParameter(RacerAnimationProfile.ParamIsBoosting, AnimatorControllerParameterType.Bool);
            controller.AddParameter(RacerAnimationProfile.ParamIsShielding, AnimatorControllerParameterType.Bool);
            controller.AddParameter(RacerAnimationProfile.ParamFire, AnimatorControllerParameterType.Trigger);
        }

        private static void ConfigureImmediateTransition(AnimatorStateTransition tr, float durationSeconds)
        {
            tr.hasExitTime = false;
            tr.exitTime = 0f;
            tr.hasFixedDuration = true;
            tr.duration = Mathf.Max(0f, durationSeconds);
            tr.canTransitionToSelf = false;
        }

        private static void AddFireTransitionIfStateExists(
            AnimatorStateMachine sm,
            string sourceStateName,
            AnimatorState fireState,
            float durationSeconds)
        {
            ChildAnimatorState[] states = sm.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state.name == sourceStateName)
                {
                    AnimatorStateTransition tr = states[i].state.AddTransition(fireState);
                    ConfigureImmediateTransition(tr, durationSeconds);
                    tr.AddCondition(AnimatorConditionMode.If, 0f, RacerAnimationProfile.ParamFire);
                    tr.AddCondition(AnimatorConditionMode.IfNot, 0f, RacerAnimationProfile.ParamIsStunned);
                    return;
                }
            }
        }
    }
}
#endif
