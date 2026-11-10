using System.Collections.Generic;
using UnityEngine;

namespace AT_Murayama.StadiumCrowd
{
    /// <summary>
    /// 観客1人分のベース3Dメッシュをプロシージャル生成（または外部メッシュを変換）するファクトリークラス。
    /// 頂点カラーにシェーダーアニメーション用のウェイト情報を焼き込みます：
    ///   Color.r : 部位マテリアルID (0.00=脚/パンツ, 0.25=胴体/服, 0.50=肌/顔/手, 0.75=髪/キャップ, 1.00=アクセント/旗/バイザー)
    ///   Color.g : 視線追従ウェイト (0.0=足元固定 ～ 0.45=胸/肩 ～ 0.92=頭部 ～ 1.0=掲げた腕)
    ///   Color.b : 腕振りコード (0.0=胴体/脚/頭, 0.01..0.50=左腕ウェイト, 0.51..1.00=右腕ウェイト)
    ///   Color.a : 身長正規化高さ (0.0=足裏 ～ 1.0=頭頂部)
    /// </summary>
    public static class SpectatorMeshFactory
    {
        public enum SpectatorStyle
        {
            [InspectorName("① 標準レース観客・純粋人間 (Pure Spectator / 持ち物なし)")]
            PureHuman = 0,

            [InspectorName("② レース応援・キャップ＆小旗/タオル (Racing Fan with Flag)")]
            RacingFanWithFlag = 1,

            [InspectorName("③ 近未来レース・サイバーバイザー観客 (Cyber Visor Spectator)")]
            CyberVisor = 2,

            [InspectorName("④ 超軽量遠景シルエット3D (Ultra-Light LOD / 大規模用)")]
            UltraLight3D = 3,

            [InspectorName("⑤ カスタム外部メッシュ差し替え (Custom Mesh Override)")]
            CustomMesh = 4
        }

        public struct MeshTemplateData
        {
            public Vector3[] vertices;
            public Vector3[] normals;
            public Color[] colors;
            public int[] triangles;
        }

        private const float TotalHeight = 1.72f;

        /// <summary>
        /// 指定したスタイルの観客1人分のテンプレートメッシュデータを生成します。
        /// </summary>
        public static MeshTemplateData CreateTemplate(SpectatorStyle style, Mesh customMesh = null)
        {
            if (style == SpectatorStyle.CustomMesh && customMesh != null)
            {
                return ConvertCustomMesh(customMesh);
            }

            List<Vector3> verts = new List<Vector3>(128);
            List<Vector3> norms = new List<Vector3>(128);
            List<Color> cols = new List<Color>(128);
            List<int> tris = new List<int>(256);

            if (style == SpectatorStyle.UltraLight3D)
            {
                BuildUltraLightSpectator(verts, norms, cols, tris);
            }
            else
            {
                BuildDetailedLowPolySpectator(style, verts, norms, cols, tris);
            }

            return new MeshTemplateData
            {
                vertices = verts.ToArray(),
                normals = norms.ToArray(),
                colors = cols.ToArray(),
                triangles = tris.ToArray()
            };
        }

        private static void BuildDetailedLowPolySpectator(
            SpectatorStyle style,
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris)
        {
            // 1. Left & Right Legs (Pants: partId = 0.0f, lookWeight = 0.0 -> 0.08)
            AddTaperedLimb(verts, norms, cols, tris,
                new Vector3(-0.105f, 0.00f, 0.00f), new Vector3(-0.10f, 0.76f, 0.00f),
                new Vector2(0.065f, 0.080f), new Vector2(0.085f, 0.095f),
                4, 0.00f, 0.00f, 0.05f, 0.00f, 0.00f, false, true);

            AddTaperedLimb(verts, norms, cols, tris,
                new Vector3(0.105f, 0.00f, 0.00f), new Vector3(0.10f, 0.76f, 0.00f),
                new Vector2(0.065f, 0.080f), new Vector2(0.085f, 0.095f),
                4, 0.00f, 0.00f, 0.05f, 0.00f, 0.00f, false, true);

            // 2. Torso: Pelvis -> Waist -> Chest -> Shoulders (Outfit: partId = 0.25f)
            Vector3[] torsoCenters = new Vector3[]
            {
                new Vector3(0f, 0.73f, 0.00f),
                new Vector3(0f, 1.02f, 0.01f),
                new Vector3(0f, 1.28f, 0.015f),
                new Vector3(0f, 1.38f, 0.00f)
            };
            Vector2[] torsoRadii = new Vector2[]
            {
                new Vector2(0.185f, 0.115f),
                new Vector2(0.175f, 0.110f),
                new Vector2(0.225f, 0.125f),
                new Vector2(0.150f, 0.095f)
            };
            float[] torsoLookWeights = new float[] { 0.08f, 0.25f, 0.45f, 0.55f };

            AddLoftedTube(verts, norms, cols, tris,
                torsoCenters, torsoRadii, torsoLookWeights,
                6, 0.25f, 0.00f, true);

            // 3. Head & Hair/Cap (Skin: partId = 0.50f, Hair/Cap: partId = 0.75f, lookWeight = 0.90f)
            Vector3[] headCenters = new Vector3[]
            {
                new Vector3(0f, 1.39f, 0.00f), // Neck base
                new Vector3(0f, 1.47f, 0.01f), // Jaw / Lower face
                new Vector3(0f, 1.60f, 0.02f), // Upper face / Forehead
                new Vector3(0f, 1.70f, 0.00f)  // Crown
            };
            Vector2[] headRadii = new Vector2[]
            {
                new Vector2(0.065f, 0.065f),
                new Vector2(0.105f, 0.115f),
                new Vector2(0.110f, 0.120f),
                new Vector2(0.075f, 0.085f)
            };
            float[] headLookWeights = new float[] { 0.65f, 0.88f, 0.94f, 0.96f };
            float[] headPartIds = new float[] { 0.50f, 0.50f, 0.75f, 0.75f };

            AddLoftedTubePerRingPart(verts, norms, cols, tris,
                headCenters, headRadii, headLookWeights, headPartIds,
                6, 0.00f, true);

            // Cap Brim / Cyber Visor protruding forward (+Z) so head gaze direction (pitch & yaw) is unmistakable!
            if (style == SpectatorStyle.CyberVisor)
            {
                // Glowing futuristic visor across the eyes (+Z)
                AddBoxPart(verts, norms, cols, tris,
                    new Vector3(0f, 1.56f, 0.115f),
                    new Vector3(0.21f, 0.055f, 0.07f),
                    1.00f, // Accent / Visor emission partId
                    0.95f,
                    0.00f);
            }
            else
            {
                // Sport / Racing cap visor brim (+Z)
                AddBoxPart(verts, norms, cols, tris,
                    new Vector3(0f, 1.60f, 0.135f),
                    new Vector3(0.18f, 0.028f, 0.11f),
                    0.75f, // Hair / Cap partId
                    0.95f,
                    0.00f);
            }

            // 4. Left Arm (Default pose: slightly angled down/forward from shoulder; shader rotates around shoulder)
            // armCode: 0.01 .. 0.50 encodes Left Arm weight (0..1)
            Vector3[] leftArmCenters = new Vector3[]
            {
                new Vector3(-0.21f, 1.31f, 0.00f), // Shoulder pivot
                new Vector3(-0.26f, 1.07f, 0.05f), // Elbow (Jacket sleeve)
                new Vector3(-0.24f, 0.85f, 0.14f)  // Hand / Wrist (Skin)
            };
            Vector2[] armRadii = new Vector2[]
            {
                new Vector2(0.060f, 0.060f),
                new Vector2(0.050f, 0.050f),
                new Vector2(0.045f, 0.045f)
            };
            float[] armLookWeights = new float[] { 0.55f, 0.78f, 0.92f };
            float[] armPartIds = new float[] { 0.25f, 0.25f, 0.50f };
            float[] leftArmCodes = new float[] { 0.06f, 0.28f, 0.49f };

            AddArticulatedArm(verts, norms, cols, tris,
                leftArmCenters, armRadii, armLookWeights, armPartIds, leftArmCodes, 4);

            // 5. Right Arm (armCode: 0.51 .. 1.00 encodes Right Arm weight 0..1)
            Vector3[] rightArmCenters = new Vector3[]
            {
                new Vector3(0.21f, 1.31f, 0.00f), // Shoulder pivot
                new Vector3(0.26f, 1.07f, 0.05f), // Elbow
                new Vector3(0.24f, 0.85f, 0.14f)  // Hand / Wrist
            };
            float[] rightArmCodes = new float[] { 0.56f, 0.78f, 0.99f };

            AddArticulatedArm(verts, norms, cols, tris,
                rightArmCenters, armRadii, armLookWeights, armPartIds, rightArmCodes, 4);

            // 6. Optional Racing Rally Flag / Towel in Right Hand
            if (style == SpectatorStyle.RacingFanWithFlag)
            {
                AddBoxPart(verts, norms, cols, tris,
                    new Vector3(0.26f, 0.72f, 0.22f),
                    new Vector3(0.04f, 0.28f, 0.18f),
                    1.00f, // Accent / Flag color
                    0.95f,
                    0.99f); // Follows right hand wave
            }
        }

        private static void BuildUltraLightSpectator(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris)
        {
            // Single 5-sided body column (Legs -> Torso -> Shoulders) + 5-sided Head + Simple Arms (~44 vertices total)
            Vector3[] bodyCenters = new Vector3[]
            {
                new Vector3(0f, 0.00f, 0.00f),
                new Vector3(0f, 0.78f, 0.00f),
                new Vector3(0f, 1.34f, 0.01f),
                new Vector3(0f, 1.44f, 0.01f),
                new Vector3(0f, 1.68f, 0.02f)
            };
            Vector2[] bodyRadii = new Vector2[]
            {
                new Vector2(0.15f, 0.10f),
                new Vector2(0.18f, 0.11f),
                new Vector2(0.23f, 0.12f),
                new Vector2(0.10f, 0.10f),
                new Vector2(0.10f, 0.12f)
            };
            float[] lookWeights = new float[] { 0.0f, 0.15f, 0.50f, 0.85f, 0.95f };
            float[] partIds = new float[] { 0.00f, 0.25f, 0.25f, 0.50f, 0.75f };

            AddLoftedTubePerRingPart(verts, norms, cols, tris,
                bodyCenters, bodyRadii, lookWeights, partIds, 5, 0.00f, true);

            // Left & Right simple arms
            AddTaperedLimb(verts, norms, cols, tris,
                new Vector3(-0.21f, 1.30f, 0.00f), new Vector3(-0.26f, 0.86f, 0.12f),
                new Vector2(0.055f, 0.055f), new Vector2(0.045f, 0.045f),
                3, 0.25f, 0.55f, 0.90f, 0.06f, 0.49f, false, false);

            AddTaperedLimb(verts, norms, cols, tris,
                new Vector3(0.21f, 1.30f, 0.00f), new Vector3(0.26f, 0.86f, 0.12f),
                new Vector2(0.055f, 0.055f), new Vector2(0.045f, 0.045f),
                3, 0.25f, 0.55f, 0.90f, 0.56f, 0.99f, false, false);
        }

        private static void AddTaperedLimb(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris,
            Vector3 p0,
            Vector3 p1,
            Vector2 r0,
            Vector2 r1,
            int sides,
            float partId,
            float look0,
            float look1,
            float arm0,
            float arm1,
            bool capStart,
            bool capEnd)
        {
            int baseIdx = verts.Count;
            float h0 = Mathf.Clamp01(p0.y / TotalHeight);
            float h1 = Mathf.Clamp01(p1.y / TotalHeight);

            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                float s = Mathf.Sin(a);
                float c = Mathf.Cos(a);
                Vector3 n = new Vector3(s, 0f, c).normalized;

                verts.Add(p0 + new Vector3(s * r0.x, 0f, c * r0.y));
                norms.Add(n);
                cols.Add(new Color(partId, look0, arm0, h0));
            }

            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f;
                float s = Mathf.Sin(a);
                float c = Mathf.Cos(a);
                Vector3 n = new Vector3(s, 0f, c).normalized;

                verts.Add(p1 + new Vector3(s * r1.x, 0f, c * r1.y));
                norms.Add(n);
                cols.Add(new Color(partId, look1, arm1, h1));
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                int b0 = baseIdx + i;
                int b1 = baseIdx + next;
                int t0 = baseIdx + sides + i;
                int t1 = baseIdx + sides + next;

                tris.Add(b0);
                tris.Add(b1);
                tris.Add(t1);

                tris.Add(b0);
                tris.Add(t1);
                tris.Add(t0);
            }

            if (capStart && sides >= 3)
            {
                for (int i = 1; i < sides - 1; i++)
                {
                    tris.Add(baseIdx);
                    tris.Add(baseIdx + i + 1);
                    tris.Add(baseIdx + i);
                }
            }

            if (capEnd && sides >= 3)
            {
                int topStart = baseIdx + sides;
                for (int i = 1; i < sides - 1; i++)
                {
                    tris.Add(topStart);
                    tris.Add(topStart + i);
                    tris.Add(topStart + i + 1);
                }
            }
        }

        private static void AddLoftedTube(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris,
            Vector3[] centers,
            Vector2[] radii,
            float[] lookWeights,
            int sides,
            float partId,
            float armCode,
            bool capTop)
        {
            float[] partIds = new float[centers.Length];
            for (int i = 0; i < partIds.Length; i++) partIds[i] = partId;
            AddLoftedTubePerRingPart(verts, norms, cols, tris, centers, radii, lookWeights, partIds, sides, armCode, capTop);
        }

        private static void AddLoftedTubePerRingPart(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris,
            Vector3[] centers,
            Vector2[] radii,
            float[] lookWeights,
            float[] partIds,
            int sides,
            float armCode,
            bool capTop)
        {
            int ringCount = centers.Length;
            int baseIdx = verts.Count;

            for (int r = 0; r < ringCount; r++)
            {
                float hNorm = Mathf.Clamp01(centers[r].y / TotalHeight);
                float ny = (r == ringCount - 1 && capTop) ? 0.35f : 0f;

                for (int i = 0; i < sides; i++)
                {
                    float a = (i / (float)sides) * Mathf.PI * 2f;
                    float s = Mathf.Sin(a);
                    float c = Mathf.Cos(a);

                    Vector3 pos = centers[r] + new Vector3(s * radii[r].x, 0f, c * radii[r].y);
                    Vector3 nrm = new Vector3(s, ny, c).normalized;

                    verts.Add(pos);
                    norms.Add(nrm);
                    cols.Add(new Color(partIds[r], lookWeights[r], armCode, hNorm));
                }
            }

            for (int r = 0; r < ringCount - 1; r++)
            {
                int rowA = baseIdx + r * sides;
                int rowB = baseIdx + (r + 1) * sides;

                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    int b0 = rowA + i;
                    int b1 = rowA + next;
                    int t0 = rowB + i;
                    int t1 = rowB + next;

                    tris.Add(b0);
                    tris.Add(b1);
                    tris.Add(t1);

                    tris.Add(b0);
                    tris.Add(t1);
                    tris.Add(t0);
                }
            }

            if (capTop && sides >= 3)
            {
                int topStart = baseIdx + (ringCount - 1) * sides;
                for (int i = 1; i < sides - 1; i++)
                {
                    tris.Add(topStart);
                    tris.Add(topStart + i);
                    tris.Add(topStart + i + 1);
                }
            }
        }

        private static void AddArticulatedArm(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris,
            Vector3[] centers,
            Vector2[] radii,
            float[] lookWeights,
            float[] partIds,
            float[] armCodes,
            int sides)
        {
            int ringCount = centers.Length;
            int baseIdx = verts.Count;

            for (int r = 0; r < ringCount; r++)
            {
                float hNorm = Mathf.Clamp01(centers[r].y / TotalHeight);
                for (int i = 0; i < sides; i++)
                {
                    float a = (i / (float)sides) * Mathf.PI * 2f;
                    float s = Mathf.Sin(a);
                    float c = Mathf.Cos(a);

                    Vector3 pos = centers[r] + new Vector3(s * radii[r].x, 0f, c * radii[r].y);
                    Vector3 nrm = new Vector3(s, 0f, c).normalized;

                    verts.Add(pos);
                    norms.Add(nrm);
                    cols.Add(new Color(partIds[r], lookWeights[r], armCodes[r], hNorm));
                }
            }

            for (int r = 0; r < ringCount - 1; r++)
            {
                int rowA = baseIdx + r * sides;
                int rowB = baseIdx + (r + 1) * sides;

                for (int i = 0; i < sides; i++)
                {
                    int next = (i + 1) % sides;
                    tris.Add(rowA + i);
                    tris.Add(rowA + next);
                    tris.Add(rowB + next);

                    tris.Add(rowA + i);
                    tris.Add(rowB + next);
                    tris.Add(rowB + i);
                }
            }

            // Cap hand tip
            int tipStart = baseIdx + (ringCount - 1) * sides;
            for (int i = 1; i < sides - 1; i++)
            {
                tris.Add(tipStart);
                tris.Add(tipStart + i + 1);
                tris.Add(tipStart + i);
            }
        }

        private static void AddBoxPart(
            List<Vector3> verts,
            List<Vector3> norms,
            List<Color> cols,
            List<int> tris,
            Vector3 center,
            Vector3 size,
            float partId,
            float lookWeight,
            float armCode)
        {
            Vector3 h = size * 0.5f;
            float hNorm = Mathf.Clamp01(center.y / TotalHeight);
            Color c = new Color(partId, lookWeight, armCode, hNorm);

            int b = verts.Count;
            // 8 corners of a compact low-poly box
            verts.Add(center + new Vector3(-h.x, -h.y, -h.z)); norms.Add(new Vector3(-0.5f, -0.5f, -0.7f).normalized); cols.Add(c);
            verts.Add(center + new Vector3( h.x, -h.y, -h.z)); norms.Add(new Vector3( 0.5f, -0.5f, -0.7f).normalized); cols.Add(c);
            verts.Add(center + new Vector3( h.x,  h.y, -h.z)); norms.Add(new Vector3( 0.5f,  0.5f, -0.7f).normalized); cols.Add(c);
            verts.Add(center + new Vector3(-h.x,  h.y, -h.z)); norms.Add(new Vector3(-0.5f,  0.5f, -0.7f).normalized); cols.Add(c);

            verts.Add(center + new Vector3(-h.x, -h.y,  h.z)); norms.Add(new Vector3(-0.5f, -0.5f,  0.7f).normalized); cols.Add(c);
            verts.Add(center + new Vector3( h.x, -h.y,  h.z)); norms.Add(new Vector3( 0.5f, -0.5f,  0.7f).normalized); cols.Add(c);
            verts.Add(center + new Vector3( h.x,  h.y,  h.z)); norms.Add(new Vector3( 0.5f,  0.5f,  0.7f).normalized); cols.Add(c);
            verts.Add(center + new Vector3(-h.x,  h.y,  h.z)); norms.Add(new Vector3(-0.5f,  0.5f,  0.7f).normalized); cols.Add(c);

            int[] boxTris = new int[]
            {
                // Front (+Z)
                4, 5, 6,  4, 6, 7,
                // Back (-Z)
                1, 0, 3,  1, 3, 2,
                // Top (+Y)
                3, 7, 6,  3, 6, 2,
                // Bottom (-Y)
                0, 1, 5,  0, 5, 4,
                // Right (+X)
                5, 1, 2,  5, 2, 6,
                // Left (-X)
                0, 4, 7,  0, 7, 3
            };

            for (int i = 0; i < boxTris.Length; i++)
            {
                tris.Add(b + boxTris[i]);
            }
        }

        /// <summary>
        /// 外部の任意メッシュ（FBX等）を観客システム用の頂点カラー構造に自動変換します。
        /// 後から別の人間モデルに差し替えたい場合でもワンクリックで追従＆アニメーションが機能します。
        /// </summary>
        private static MeshTemplateData ConvertCustomMesh(Mesh src)
        {
            Vector3[] srcVerts = src.vertices;
            Vector3[] srcNorms = src.normals;
            int[] srcTris = src.triangles;
            Bounds bounds = src.bounds;

            float minY = bounds.min.y;
            float height = Mathf.Max(bounds.size.y, 0.01f);
            float scale = TotalHeight / height;
            float centerX = bounds.center.x;
            float centerZ = bounds.center.z;
            float halfWidth = Mathf.Max(bounds.extents.x, 0.01f);

            Vector3[] dstVerts = new Vector3[srcVerts.Length];
            Vector3[] dstNorms = new Vector3[srcVerts.Length];
            Color[] dstCols = new Color[srcVerts.Length];

            for (int i = 0; i < srcVerts.Length; i++)
            {
                Vector3 v = srcVerts[i];
                float normY = Mathf.Clamp01((v.y - minY) / height);
                float relX = (v.x - centerX) / halfWidth;

                dstVerts[i] = new Vector3((v.x - centerX) * scale, (v.y - minY) * scale, (v.z - centerZ) * scale);
                dstNorms[i] = (srcNorms != null && srcNorms.Length == srcVerts.Length) ? srcNorms[i] : Vector3.up;

                // Estimate body part & weights from normalized height and X offset
                float partId = 0.25f;
                if (normY < 0.44f) partId = 0.00f;
                else if (normY > 0.88f) partId = 0.75f;
                else if (normY > 0.78f) partId = 0.50f;

                float lookWeight = Mathf.SmoothStep(0.0f, 0.95f, Mathf.InverseLerp(0.40f, 0.85f, normY));

                float armCode = 0.0f;
                if (normY > 0.45f && normY < 0.82f && Mathf.Abs(relX) > 0.55f)
                {
                    float armW = Mathf.Clamp01((Mathf.Abs(relX) - 0.55f) / 0.45f);
                    armCode = (relX < 0f) ? Mathf.Lerp(0.05f, 0.49f, armW) : Mathf.Lerp(0.55f, 0.99f, armW);
                }

                dstCols[i] = new Color(partId, lookWeight, armCode, normY);
            }

            return new MeshTemplateData
            {
                vertices = dstVerts,
                normals = dstNorms,
                colors = dstCols,
                triangles = srcTris
            };
        }
    }
}

