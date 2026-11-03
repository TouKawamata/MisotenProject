using System;
using System.Collections.Generic;
using UnityEngine;

namespace AT_Murayama.PlayerStatusUI
{
    /// <summary>
    /// プレイヤーのUI表示状態（優先度：Stunned > Boosting > Shielding > Normal/MoveLeft/MoveRight）
    /// </summary>
    public enum PlayerUIState
    {
        [InspectorName("通常（中央・ニュートラル）")]
        Normal = 0,

        [InspectorName("左移動（Steering < 0）")]
        MoveLeft = 1,

        [InspectorName("右移動（Steering > 0）")]
        MoveRight = 2,

        [InspectorName("ブースト中（IsBoosting）")]
        Boosting = 3,

        [InspectorName("シールド展開中（IsShielding）")]
        Shielding = 4,

        [InspectorName("スタン中（IsStunned ※最優先）")]
        Stunned = 5
    }

    /// <summary>
    /// 1つのボーン（関節Transform）のローカル回転・位置スナップショット
    /// </summary>
    [Serializable]
    public struct BoneTransformSnapshot
    {
        [Tooltip("モデルルートからの相対パスまたはボーン名")]
        public string bonePath;

        [Tooltip("ボーン名（検索フォールバック用）")]
        public string boneName;

        [Tooltip("保存時のローカル回転（Quaternion）")]
        public Quaternion localRotation;

        [Tooltip("保存時のローカル座標（ルートや腰の上下オフセット用）")]
        public Vector3 localPosition;

        public BoneTransformSnapshot(string path, string name, Quaternion rotation, Vector3 position)
        {
            bonePath = path;
            boneName = name;
            localRotation = rotation;
            localPosition = position;
        }
    }

    /// <summary>
    /// 1つの状態（例：Boosting、Shielding、Stunnedなど）に対する全身ポーズ＆ホログラム発光カラー設定
    /// </summary>
    [Serializable]
    public class StatePoseEntry
    {
        [Tooltip("対象のプレイヤー状態")]
        public PlayerUIState state = PlayerUIState.Normal;

        [Tooltip("UI等に表示する状態ラベル")]
        public string displayLabel = "NORMAL";

        [Tooltip("この状態へ遷移するときの補間スピード（大きいほど素早くポーズが切り替わる）")]
        [Range(2f, 30f)]
        public float transitionSpeed = 10f;

        [Header("ホログラム発光カラー設定")]
        [ColorUsage(true, true)]
        public Color mainHoloColor = new Color(0.0f, 2.2f, 3.2f, 0.80f);

        [ColorUsage(true, true)]
        public Color rimGlowColor = new Color(0.4f, 3.8f, 4.5f, 0.95f);

        [ColorUsage(true, true)]
        public Color accentColor = new Color(0.2f, 4.0f, 3.5f, 1.0f);

        [Header("モデル全体の向き・位置オフセット（ポーズ補助用）")]
        [Tooltip("モデル全体の追加オイラー回転オフセット（前傾やバンク角の微調整に便利）")]
        public Vector3 modelRootRotationOffset = Vector3.zero;

        [Tooltip("モデル全体の追加位置オフセット")]
        public Vector3 modelRootPositionOffset = Vector3.zero;

        [Header("保存された各ボーンの回転リスト")]
        public List<BoneTransformSnapshot> boneSnapshots = new List<BoneTransformSnapshot>();
    }

    /// <summary>
    /// 実行前に作成・編集した全状態のポーズパターンをアセット（.asset）として永続保存するための ScriptableObject。
    /// Playモード中にポーズや色を微調整してもアセットに保存できるため、変更が消えません。
    /// </summary>
    [CreateAssetMenu(
        fileName = "PlayerPosePresetAsset",
        menuName = "AT_Murayama/PlayerStatusUI/Player Pose Preset Asset")]
    public class PlayerPosePresetData : ScriptableObject
    {
        [Tooltip("各状態（Normal / MoveLeft / MoveRight / Boosting / Shielding / Stunned）のポーズ定義リスト")]
        public List<StatePoseEntry> statePoses = new List<StatePoseEntry>();

        [Tooltip("モデルの初期バインドポーズ（Tポーズ）の記録（リセット・自動計算の基準用）")]
        public List<BoneTransformSnapshot> bindPoseSnapshots = new List<BoneTransformSnapshot>();

        /// <summary>
        /// 指定した状態の StatePoseEntry を取得します（存在しない場合は新規作成して返します）。
        /// </summary>
        public StatePoseEntry GetOrCreateEntry(PlayerUIState targetState)
        {
            for (int i = 0; i < statePoses.Count; i++)
            {
                if (statePoses[i] != null && statePoses[i].state == targetState)
                {
                    return statePoses[i];
                }
            }

            StatePoseEntry created = CreateDefaultMetadataEntry(targetState);
            statePoses.Add(created);
            return created;
        }

        public static StatePoseEntry CreateDefaultMetadataEntry(PlayerUIState state)
        {
            var entry = new StatePoseEntry { state = state };
            switch (state)
            {
                case PlayerUIState.Normal:
                    entry.displayLabel = "NORMAL";
                    entry.transitionSpeed = 9f;
                    entry.mainHoloColor = new Color(0.0f, 2.2f, 3.2f, 0.80f);
                    entry.rimGlowColor = new Color(0.4f, 3.8f, 4.5f, 0.95f);
                    entry.accentColor = new Color(0.2f, 4.0f, 3.5f, 1.0f);
                    break;

                case PlayerUIState.MoveLeft:
                    entry.displayLabel = "STEER LEFT";
                    entry.transitionSpeed = 11f;
                    entry.mainHoloColor = new Color(0.0f, 2.6f, 3.6f, 0.85f);
                    entry.rimGlowColor = new Color(0.5f, 4.2f, 4.6f, 0.95f);
                    entry.accentColor = new Color(0.3f, 4.2f, 3.8f, 1.0f);
                    break;

                case PlayerUIState.MoveRight:
                    entry.displayLabel = "STEER RIGHT";
                    entry.transitionSpeed = 11f;
                    entry.mainHoloColor = new Color(0.0f, 2.6f, 3.6f, 0.85f);
                    entry.rimGlowColor = new Color(0.5f, 4.2f, 4.6f, 0.95f);
                    entry.accentColor = new Color(0.3f, 4.2f, 3.8f, 1.0f);
                    break;

                case PlayerUIState.Boosting:
                    entry.displayLabel = "BOOST!!";
                    entry.transitionSpeed = 14f;
                    entry.mainHoloColor = new Color(3.6f, 1.1f, 0.2f, 0.90f);
                    entry.rimGlowColor = new Color(4.8f, 2.8f, 0.6f, 1.0f);
                    entry.accentColor = new Color(4.5f, 2.0f, 0.4f, 1.0f);
                    break;

                case PlayerUIState.Shielding:
                    entry.displayLabel = "SHIELD";
                    entry.transitionSpeed = 14f;
                    entry.mainHoloColor = new Color(0.1f, 3.2f, 1.4f, 0.88f);
                    entry.rimGlowColor = new Color(0.6f, 4.5f, 2.3f, 1.0f);
                    entry.accentColor = new Color(0.4f, 4.2f, 1.8f, 1.0f);
                    break;

                case PlayerUIState.Stunned:
                    entry.displayLabel = "STUNNED!";
                    entry.transitionSpeed = 16f;
                    entry.mainHoloColor = new Color(3.8f, 0.35f, 0.45f, 0.90f);
                    entry.rimGlowColor = new Color(4.8f, 2.0f, 0.3f, 1.0f);
                    entry.accentColor = new Color(4.5f, 0.6f, 0.6f, 1.0f);
                    break;
            }
            return entry;
        }
    }
}

