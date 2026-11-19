using System;
using System.Collections.Generic;
using UnityEngine;

namespace AT_Murayama.StadiumCrowdV2
{
    /// <summary>
    /// 【V2専用・完全独立】スタジアム3Dモデルの床面への垂直スナップ投影・斜面フィルタ・壁/天井回避・ブラシ用レイキャストを担う空間ハッシュサンプラー。
    /// ・MeshCollider 不要で MeshFilter の三角形を直接2D XZ空間グリッド化して高速判定します。
    /// ・インポート設定で Read/Write=OFF のメッシュに対しても、既存Collider または一時MeshColliderプロキシによるフォールバック判定をサポートします（元オブジェクトは一切変更しません）。
    /// ・水平面除外（アリーナ床・柱の天面・外壁の屋根・モニター枠を除外し、傾斜した客席スロープのみに吸着）に対応します。
    /// </summary>
    public sealed class StadiumSurfaceSamplerV2 : IDisposable
    {
        public enum FloorSelectMode
        {
            [InspectorName("予測高さに最も近い床 (Nearest to Expected Y)")]
            NearestToExpectedY = 0,

            [InspectorName("範囲内の最も高い床 (Highest in Range / 1F・2Fスロープ推奨)")]
            HighestInRange = 2,

            [InspectorName("範囲内の最も低い床 (Lowest in Range)")]
            LowestInRange = 1
        }

        private struct SampledTriangle
        {
            public Vector3 v0;
            public Vector3 v1;
            public Vector3 v2;
            public Vector3 normal;
            public float minX, maxX, minY, maxY, minZ, maxZ;
            public float denomXZ;
        }

        private struct FallbackColliderEntry
        {
            public Collider collider;
            public bool isTemporaryProxy;
            public GameObject tempOwner;
        }

        private const float CellSize = 4.0f;

        private readonly List<SampledTriangle> _floorTris = new List<SampledTriangle>(4096);
        private readonly List<SampledTriangle> _blockingTris = new List<SampledTriangle>(4096);
        private readonly List<SampledTriangle> _wallTris = new List<SampledTriangle>(2048);
        private readonly List<SampledTriangle> _allRaycastTris = new List<SampledTriangle>(8192);

        private readonly Dictionary<long, List<int>> _floorGrid = new Dictionary<long, List<int>>(1024);
        private readonly Dictionary<long, List<int>> _blockingGrid = new Dictionary<long, List<int>>(1024);
        private readonly Dictionary<long, List<int>> _wallGrid = new Dictionary<long, List<int>>(512);

        private readonly List<FallbackColliderEntry> _fallbackColliders = new List<FallbackColliderEntry>();

        private Transform _generatorTransform;
        private float _minFloorNormalY = 0.45f;
        private float _maxFloorNormalYForSlope = 1.01f;
        private float _wallMaxAbsNormalY = 0.35f;
        private bool _allowDoubleSidedFloor;
        private bool _excludeFlatSurfaces;
        private bool _requireInwardFacingSlope;
        private Vector3 _allTrisMin;
        private Vector3 _allTrisMax;

        public int IndexedMeshCount { get; private set; }
        public int FloorTriangleCount => _floorTris.Count;
        public int WallTriangleCount => _wallTris.Count;
        public int NonReadableFallbackMeshCount { get; private set; }
        public int SkippedUnreadableMeshCount { get; private set; }
        public bool HasAnySurface => _floorTris.Count > 0 || _fallbackColliders.Count > 0;
        public bool HasTemporaryProxies
        {
            get
            {
                for (int i = 0; i < _fallbackColliders.Count; i++)
                {
                    if (_fallbackColliders[i].isTemporaryProxy) return true;
                }
                return false;
            }
        }
        public float BoundsMinY { get; private set; }
        public float BoundsMaxY { get; private set; }

        public void Build(
            Transform generatorTransform,
            Transform surfaceRoot,
            List<MeshFilter> extraMeshFilters,
            float minFloorNormalY,
            float wallMaxAbsNormalY,
            bool allowDoubleSidedFloor,
            bool excludeFlatSurfaces = true,
            float minFloorSlopeDeg = 3.5f,
            bool requireInwardFacingSlope = true)
        {
            DisposeTemporaryColliders();

            _floorTris.Clear();
            _blockingTris.Clear();
            _wallTris.Clear();
            _allRaycastTris.Clear();
            _floorGrid.Clear();
            _blockingGrid.Clear();
            _wallGrid.Clear();
            _fallbackColliders.Clear();

            _generatorTransform = generatorTransform;
            _minFloorNormalY = Mathf.Clamp(minFloorNormalY, 0.05f, 0.99f);
            _wallMaxAbsNormalY = Mathf.Clamp(wallMaxAbsNormalY, 0.05f, 0.85f);
            _allowDoubleSidedFloor = allowDoubleSidedFloor;
            _excludeFlatSurfaces = excludeFlatSurfaces;
            _requireInwardFacingSlope = requireInwardFacingSlope;
            float clampedSlopeDeg = Mathf.Clamp(minFloorSlopeDeg, 0f, 45f);
            _maxFloorNormalYForSlope = (excludeFlatSurfaces && clampedSlopeDeg > 0.05f)
                ? Mathf.Cos(clampedSlopeDeg * Mathf.Deg2Rad)
                : 1.01f;

            IndexedMeshCount = 0;
            NonReadableFallbackMeshCount = 0;
            SkippedUnreadableMeshCount = 0;
            BoundsMinY = float.MaxValue;
            BoundsMaxY = float.MinValue;
            _allTrisMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            _allTrisMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            if (generatorTransform == null) return;

            List<MeshFilter> filters = new List<MeshFilter>(32);
            if (surfaceRoot != null)
            {
                surfaceRoot.GetComponentsInChildren(false, filters);
            }
            if (extraMeshFilters != null)
            {
                for (int i = 0; i < extraMeshFilters.Count; i++)
                {
                    MeshFilter mf = extraMeshFilters[i];
                    if (mf != null && !filters.Contains(mf))
                    {
                        filters.Add(mf);
                    }
                }
            }

            Matrix4x4 worldToGen = generatorTransform.worldToLocalMatrix;
            bool createdAnyTempCollider = false;

            for (int i = 0; i < filters.Count; i++)
            {
                MeshFilter mf = filters[i];
                if (mf == null) continue;
                if (mf.transform.IsChildOf(generatorTransform)) continue;

                MeshRenderer mr = mf.GetComponent<MeshRenderer>();
                if (mr != null && !mr.enabled) continue;

                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;

                if (mesh.isReadable)
                {
                    IndexReadableMesh(mf.transform, mesh, worldToGen);
                    IndexedMeshCount++;
                }
                else
                {
                    Collider existingCol = mf.GetComponent<Collider>();
                    if (existingCol != null && existingCol.enabled)
                    {
                        _fallbackColliders.Add(new FallbackColliderEntry
                        {
                            collider = existingCol,
                            isTemporaryProxy = false,
                            tempOwner = null
                        });
                        ExpandBoundsFromWorld(existingCol.bounds, worldToGen);
                        NonReadableFallbackMeshCount++;
                        IndexedMeshCount++;
                    }
                    else
                    {
                        if (TryCreateTemporaryMeshCollider(mf.transform, mesh, out FallbackColliderEntry entry))
                        {
                            _fallbackColliders.Add(entry);
                            createdAnyTempCollider = true;
                            ExpandBoundsFromMesh(mf.transform, mesh.bounds, worldToGen);
                            NonReadableFallbackMeshCount++;
                            IndexedMeshCount++;
                        }
                        else
                        {
                            SkippedUnreadableMeshCount++;
                        }
                    }
                }
            }

            if (createdAnyTempCollider)
            {
                Physics.SyncTransforms();
            }

            if (BoundsMinY > BoundsMaxY)
            {
                BoundsMinY = 0f;
                BoundsMaxY = 30f;
            }
        }

        private bool IsValidSeatingNormal(Vector3 normal, Vector3 expectedInwardDirXZ)
        {
            float absY = Mathf.Abs(normal.y);
            float signedY = _allowDoubleSidedFloor ? absY : normal.y;
            if (signedY < _minFloorNormalY) return false;

            // 水平面除外（アリーナ床・柱の天面・外壁の上端・モニター下枠など傾斜0°の面を除外）
            if (_excludeFlatSurfaces && absY > _maxFloorNormalYForSlope)
            {
                return false;
            }

            // スタジアム中心側を向く客席スロープのみ許可（外周壁の外向き斜面を除外）
            if (_requireInwardFacingSlope && expectedInwardDirXZ.sqrMagnitude > 0.001f)
            {
                Vector3 effectiveNrm = (_allowDoubleSidedFloor && normal.y < 0f) ? -normal : normal;
                float inwardDot = effectiveNrm.x * expectedInwardDirXZ.x + effectiveNrm.z * expectedInwardDirXZ.z;
                if (inwardDot <= 0.02f)
                {
                    return false;
                }
            }

            return true;
        }

        private void IndexReadableMesh(Transform meshTr, Mesh mesh, Matrix4x4 worldToGen)
        {
            Vector3[] verts = mesh.vertices;
            int[] tris = mesh.triangles;
            if (verts == null || tris == null || tris.Length < 3) return;

            Matrix4x4 localToGen = worldToGen * meshTr.localToWorldMatrix;
            bool flipWinding = localToGen.determinant < 0f;

            Vector3[] genVerts = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 p = localToGen.MultiplyPoint3x4(verts[i]);
                genVerts[i] = p;
                if (p.y < BoundsMinY) BoundsMinY = p.y;
                if (p.y > BoundsMaxY) BoundsMaxY = p.y;
                _allTrisMin = Vector3.Min(_allTrisMin, p);
                _allTrisMax = Vector3.Max(_allTrisMax, p);
            }

            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vector3 v0 = genVerts[tris[t]];
                Vector3 v1 = genVerts[tris[t + 1]];
                Vector3 v2 = genVerts[tris[t + 2]];

                Vector3 cross = Vector3.Cross(v1 - v0, v2 - v0);
                float sqrMag = cross.sqrMagnitude;
                if (sqrMag < 1e-8f) continue;

                Vector3 nrm = cross / Mathf.Sqrt(sqrMag);
                if (flipWinding) nrm = -nrm;

                SampledTriangle st = new SampledTriangle
                {
                    v0 = v0,
                    v1 = v1,
                    v2 = v2,
                    normal = nrm,
                    minX = Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x)),
                    maxX = Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x)),
                    minY = Mathf.Min(v0.y, Mathf.Min(v1.y, v2.y)),
                    maxY = Mathf.Max(v0.y, Mathf.Max(v1.y, v2.y)),
                    minZ = Mathf.Min(v0.z, Mathf.Min(v1.z, v2.z)),
                    maxZ = Mathf.Max(v0.z, Mathf.Max(v1.z, v2.z)),
                    denomXZ = (v1.z - v2.z) * (v0.x - v2.x) + (v2.x - v1.x) * (v0.z - v2.z)
                };

                _allRaycastTris.Add(st);

                bool isFloor = _allowDoubleSidedFloor
                    ? (Mathf.Abs(nrm.y) >= _minFloorNormalY)
                    : (nrm.y >= _minFloorNormalY);

                if (isFloor && Mathf.Abs(st.denomXZ) > 1e-6f)
                {
                    int idx = _floorTris.Count;
                    _floorTris.Add(st);
                    InsertIntoGrid(_floorGrid, idx, st.minX, st.maxX, st.minZ, st.maxZ);
                }

                if (Mathf.Abs(st.denomXZ) > 1e-6f)
                {
                    int bIdx = _blockingTris.Count;
                    _blockingTris.Add(st);
                    InsertIntoGrid(_blockingGrid, bIdx, st.minX, st.maxX, st.minZ, st.maxZ);
                }

                if (Mathf.Abs(nrm.y) <= _wallMaxAbsNormalY)
                {
                    int wIdx = _wallTris.Count;
                    _wallTris.Add(st);
                    InsertIntoGrid(_wallGrid, wIdx, st.minX, st.maxX, st.minZ, st.maxZ);
                }
            }
        }

        private static bool TryCreateTemporaryMeshCollider(Transform srcTr, Mesh mesh, out FallbackColliderEntry entry)
        {
            entry = default;
            try
            {
                if (mesh.vertexCount < 3 || mesh.subMeshCount == 0) return false;

                GameObject proxy = new GameObject("__TempCrowdV2SnapProxy__");
                proxy.hideFlags = HideFlags.HideAndDontSave;
                proxy.transform.SetPositionAndRotation(srcTr.position, srcTr.rotation);
                proxy.transform.localScale = srcTr.lossyScale;

                MeshCollider mc = proxy.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                if (mc.sharedMesh == null)
                {
                    DestroyImmediateSafe(proxy);
                    return false;
                }

                entry = new FallbackColliderEntry
                {
                    collider = mc,
                    isTemporaryProxy = true,
                    tempOwner = proxy
                };
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void ExpandBoundsFromWorld(Bounds worldBounds, Matrix4x4 worldToGen)
        {
            Vector3 c = worldBounds.center;
            Vector3 e = worldBounds.extents;
            for (int dx = -1; dx <= 1; dx += 2)
            {
                for (int dy = -1; dy <= 1; dy += 2)
                {
                    for (int dz = -1; dz <= 1; dz += 2)
                    {
                        Vector3 wp = new Vector3(c.x + dx * e.x, c.y + dy * e.y, c.z + dz * e.z);
                        float gy = worldToGen.MultiplyPoint3x4(wp).y;
                        if (gy < BoundsMinY) BoundsMinY = gy;
                        if (gy > BoundsMaxY) BoundsMaxY = gy;
                    }
                }
            }
        }

        private void ExpandBoundsFromMesh(Transform meshTr, Bounds localBounds, Matrix4x4 worldToGen)
        {
            Matrix4x4 m = worldToGen * meshTr.localToWorldMatrix;
            Vector3 c = localBounds.center;
            Vector3 e = localBounds.extents;
            for (int dx = -1; dx <= 1; dx += 2)
            {
                for (int dy = -1; dy <= 1; dy += 2)
                {
                    for (int dz = -1; dz <= 1; dz += 2)
                    {
                        Vector3 lp = new Vector3(c.x + dx * e.x, c.y + dy * e.y, c.z + dz * e.z);
                        float gy = m.MultiplyPoint3x4(lp).y;
                        if (gy < BoundsMinY) BoundsMinY = gy;
                        if (gy > BoundsMaxY) BoundsMaxY = gy;
                    }
                }
            }
        }

        /// <summary>
        /// 指定したジェネレーターローカル座標 (x, z) の垂直ライン上から、斜面フィルタ条件を満たす有効な客席床Yを検索します。
        /// </summary>
        public bool TrySampleFloorY(
            float px,
            float pz,
            float expectedY,
            float minY,
            float maxY,
            FloorSelectMode selectMode,
            Vector3 expectedInwardDirXZ,
            out float outHitY,
            out Vector3 outHitNormal,
            bool ignoreSlopeFilter = false)
        {
            outHitY = expectedY;
            outHitNormal = Vector3.up;

            bool found = false;
            float bestY = 0f;
            float bestScore = float.MaxValue;
            Vector3 bestNormal = Vector3.up;

            // 1. Readable Mesh Spatial Grid Path
            if (_floorTris.Count > 0)
            {
                int cx = Mathf.FloorToInt(px / CellSize);
                int cz = Mathf.FloorToInt(pz / CellSize);
                long key = PackCellKey(cx, cz);

                if (_floorGrid.TryGetValue(key, out List<int> list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        SampledTriangle st = _floorTris[list[i]];
                        if (st.maxY < minY || st.minY > maxY) continue;
                        if (px < st.minX || px > st.maxX || pz < st.minZ || pz > st.maxZ) continue;
                        if (!ignoreSlopeFilter && !IsValidSeatingNormal(st.normal, expectedInwardDirXZ)) continue;

                        if (TryIntersectVerticalXZ(st, px, pz, out float hitY))
                        {
                            if (hitY < minY || hitY > maxY) continue;
                            ConsiderCandidate(hitY, st.normal, expectedY, selectMode, ref found, ref bestY, ref bestScore, ref bestNormal);
                        }
                    }
                }
            }

            // 2. Fallback Collider Path (Read/Write OFF meshes)
            if (_fallbackColliders.Count > 0 && _generatorTransform != null)
            {
                float castTopY = maxY + 1.5f;
                float castBottomY = minY - 1.0f;
                Vector3 rayOriginWS = _generatorTransform.TransformPoint(new Vector3(px, castTopY, pz));
                Vector3 rayEndWS = _generatorTransform.TransformPoint(new Vector3(px, castBottomY, pz));
                Vector3 rayVecWS = rayEndWS - rayOriginWS;
                float totalDistWS = rayVecWS.magnitude;
                if (totalDistWS > 0.01f)
                {
                    Vector3 rayDirWS = rayVecWS / totalDistWS;

                    for (int c = 0; c < _fallbackColliders.Count; c++)
                    {
                        Collider col = _fallbackColliders[c].collider;
                        if (col == null) continue;

                        float traveled = 0f;
                        int safetySteps = 0;
                        while (traveled < totalDistWS && safetySteps < 14)
                        {
                            safetySteps++;
                            Ray ray = new Ray(rayOriginWS + rayDirWS * traveled, rayDirWS);
                            if (!col.Raycast(ray, out RaycastHit hit, totalDistWS - traveled))
                            {
                                break;
                            }

                            Vector3 hitLocal = _generatorTransform.InverseTransformPoint(hit.point);
                            Vector3 nrmLocal = _generatorTransform.InverseTransformDirection(hit.normal).normalized;

                            bool validNormal = ignoreSlopeFilter
                                ? (_allowDoubleSidedFloor ? Mathf.Abs(nrmLocal.y) >= _minFloorNormalY : nrmLocal.y >= _minFloorNormalY)
                                : IsValidSeatingNormal(nrmLocal, expectedInwardDirXZ);

                            if (hitLocal.y >= minY && hitLocal.y <= maxY && validNormal)
                            {
                                ConsiderCandidate(hitLocal.y, nrmLocal, expectedY, selectMode, ref found, ref bestY, ref bestScore, ref bestNormal);
                            }

                            traveled += hit.distance + 0.08f;
                        }
                    }
                }
            }

            if (found)
            {
                outHitY = bestY;
                outHitNormal = bestNormal;
            }
            return found;
        }

        /// <summary>
        /// Sceneビューの消しゴム／追加ブラシ用の3D任意方向レイキャスト（Read/Write ON・OFF 両対応）。
        /// </summary>
        public bool TryRaycastSurface(Ray worldRay, out Vector3 outLocalPos, out Vector3 outLocalNormal)
        {
            outLocalPos = Vector3.zero;
            outLocalNormal = Vector3.up;
            if (_generatorTransform == null) return false;

            bool hitAny = false;
            float bestT = float.MaxValue;

            // 1. Readable Mesh Path (Fast AABB Slab Culling + Möller-Trumbore in generator local space)
            if (_allRaycastTris.Count > 0)
            {
                Vector3 originL = _generatorTransform.InverseTransformPoint(worldRay.origin);
                Vector3 dirL = _generatorTransform.InverseTransformDirection(worldRay.direction);
                float dirLen = dirL.magnitude;
                if (dirLen > 1e-6f)
                {
                    dirL /= dirLen;
                    Vector3 invDir = new Vector3(
                        Mathf.Abs(dirL.x) > 1e-7f ? 1f / dirL.x : 1e7f,
                        Mathf.Abs(dirL.y) > 1e-7f ? 1f / dirL.y : 1e7f,
                        Mathf.Abs(dirL.z) > 1e-7f ? 1f / dirL.z : 1e7f);

                    if (TryIntersectRayAABB(originL, invDir, _allTrisMin.x, _allTrisMin.y, _allTrisMin.z, _allTrisMax.x, _allTrisMax.y, _allTrisMax.z, bestT))
                    {
                        for (int i = 0; i < _allRaycastTris.Count; i++)
                        {
                            SampledTriangle st = _allRaycastTris[i];
                            if (!TryIntersectRayAABB(originL, invDir, st.minX, st.minY, st.minZ, st.maxX, st.maxY, st.maxZ, bestT))
                            {
                                continue;
                            }
                            if (TryIntersectRayTriangle(originL, dirL, st.v0, st.v1, st.v2, out float t) && t > 0.05f && t < bestT)
                            {
                                bestT = t;
                                outLocalPos = originL + dirL * t;
                                outLocalNormal = st.normal;
                                hitAny = true;
                            }
                        }
                    }
                }
            }

            // 2. Fallback Collider Path
            if (_fallbackColliders.Count > 0)
            {
                float bestWorldDist = hitAny
                    ? Vector3.Distance(worldRay.origin, _generatorTransform.TransformPoint(outLocalPos))
                    : 5000f;

                for (int c = 0; c < _fallbackColliders.Count; c++)
                {
                    Collider col = _fallbackColliders[c].collider;
                    if (col == null) continue;

                    if (col.Raycast(worldRay, out RaycastHit hit, bestWorldDist))
                    {
                        bestWorldDist = hit.distance;
                        outLocalPos = _generatorTransform.InverseTransformPoint(hit.point);
                        outLocalNormal = _generatorTransform.InverseTransformDirection(hit.normal).normalized;
                        hitAny = true;
                    }
                }
            }

            return hitAny;
        }

        /// <summary>
        /// 座席の頭上クリアランス（2階床の裏側や天井が頭を貫通していないか）を判定します。
        /// 足元近傍の薄い床段差による誤爆を防ぐため floorY + 0.55m 以上のみをチェックします。
        /// </summary>
        public bool HasOverheadObstruction(float px, float floorY, float pz, float minHeadroom)
        {
            if (minHeadroom <= 0.6f) return false;
            float lowY = floorY + 0.55f;
            float highY = floorY + minHeadroom;

            if (_blockingTris.Count > 0)
            {
                int cx = Mathf.FloorToInt(px / CellSize);
                int cz = Mathf.FloorToInt(pz / CellSize);
                if (_blockingGrid.TryGetValue(PackCellKey(cx, cz), out List<int> list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        SampledTriangle st = _blockingTris[list[i]];
                        if (st.maxY < lowY || st.minY > highY) continue;
                        if (px < st.minX || px > st.maxX || pz < st.minZ || pz > st.maxZ) continue;

                        if (TryIntersectVerticalXZ(st, px, pz, out float hitY))
                        {
                            if (hitY >= lowY && hitY <= highY)
                            {
                                // 下向きの天井裏、または頭上0.75m以上にある覆い面のみを障害物として認定
                                if (st.normal.y < -0.15f || hitY >= floorY + 0.75f)
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }

            if (_fallbackColliders.Count > 0 && _generatorTransform != null)
            {
                Vector3 pLowWS = _generatorTransform.TransformPoint(new Vector3(px, lowY, pz));
                Vector3 pHighWS = _generatorTransform.TransformPoint(new Vector3(px, highY, pz));
                Vector3 dirWS = pHighWS - pLowWS;
                float dist = dirWS.magnitude;
                if (dist > 0.05f)
                {
                    Ray upRay = new Ray(pLowWS, dirWS / dist);
                    Ray downRay = new Ray(pHighWS, -dirWS / dist);
                    for (int c = 0; c < _fallbackColliders.Count; c++)
                    {
                        Collider col = _fallbackColliders[c].collider;
                        if (col == null) continue;
                        if (col.Raycast(upRay, out _, dist) || col.Raycast(downRay, out _, dist))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 座席の周囲 (clearanceRadius) に背の高い仕切り壁・縦壁が存在するか判定します。
        /// </summary>
        public bool IsNearTallWall(
            float px,
            float floorY,
            float pz,
            float clearanceRadius,
            float minWallHeightAboveFloor = 0.95f)
        {
            if (clearanceRadius <= 0.01f) return false;

            float wallRequireTopY = floorY + Mathf.Max(0.4f, minWallHeightAboveFloor);
            float wallAllowBottomY = floorY + 1.55f;
            float rSqr = clearanceRadius * clearanceRadius;

            if (_wallTris.Count > 0)
            {
                int minCX = Mathf.FloorToInt((px - clearanceRadius) / CellSize);
                int maxCX = Mathf.FloorToInt((px + clearanceRadius) / CellSize);
                int minCZ = Mathf.FloorToInt((pz - clearanceRadius) / CellSize);
                int maxCZ = Mathf.FloorToInt((pz + clearanceRadius) / CellSize);

                for (int cx = minCX; cx <= maxCX; cx++)
                {
                    for (int cz = minCZ; cz <= maxCZ; cz++)
                    {
                        if (!_wallGrid.TryGetValue(PackCellKey(cx, cz), out List<int> list)) continue;

                        for (int i = 0; i < list.Count; i++)
                        {
                            SampledTriangle st = _wallTris[list[i]];
                            if (st.maxY < wallRequireTopY || st.minY > wallAllowBottomY) continue;
                            if (px < st.minX - clearanceRadius || px > st.maxX + clearanceRadius) continue;
                            if (pz < st.minZ - clearanceRadius || pz > st.maxZ + clearanceRadius) continue;

                            float distSqrXZ = DistancePointToTriangleXZSquared(
                                px, pz,
                                st.v0.x, st.v0.z,
                                st.v1.x, st.v1.z,
                                st.v2.x, st.v2.z);

                            if (distSqrXZ <= rSqr)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            if (_fallbackColliders.Count > 0 && _generatorTransform != null)
            {
                float probeY = floorY + Mathf.Max(0.95f, minWallHeightAboveFloor);
                Vector3 originWS = _generatorTransform.TransformPoint(new Vector3(px, probeY, pz));
                Vector3 scale = _generatorTransform.lossyScale;
                float worldRadius = clearanceRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

                Vector3[] dirs =
                {
                    _generatorTransform.right,
                    -_generatorTransform.right,
                    _generatorTransform.forward,
                    -_generatorTransform.forward
                };

                for (int c = 0; c < _fallbackColliders.Count; c++)
                {
                    Collider col = _fallbackColliders[c].collider;
                    if (col == null) continue;

                    for (int d = 0; d < 4; d++)
                    {
                        Ray outRay = new Ray(originWS, dirs[d]);
                        if (col.Raycast(outRay, out RaycastHit hitOut, worldRadius))
                        {
                            Vector3 nrmLocal = _generatorTransform.InverseTransformDirection(hitOut.normal).normalized;
                            if (Mathf.Abs(nrmLocal.y) <= _wallMaxAbsNormalY)
                            {
                                return true;
                            }
                        }

                        Ray inRay = new Ray(originWS + dirs[d] * worldRadius, -dirs[d]);
                        if (col.Raycast(inRay, out RaycastHit hitIn, worldRadius))
                        {
                            Vector3 nrmLocal = _generatorTransform.InverseTransformDirection(hitIn.normal).normalized;
                            if (Mathf.Abs(nrmLocal.y) <= _wallMaxAbsNormalY)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool TryIntersectRayAABB(
            Vector3 origin,
            Vector3 invDir,
            float minX, float minY, float minZ,
            float maxX, float maxY, float maxZ,
            float maxT)
        {
            float tx1 = (minX - origin.x) * invDir.x;
            float tx2 = (maxX - origin.x) * invDir.x;
            float tmin = tx1 < tx2 ? tx1 : tx2;
            float tmax = tx1 > tx2 ? tx1 : tx2;

            float ty1 = (minY - origin.y) * invDir.y;
            float ty2 = (maxY - origin.y) * invDir.y;
            float tyMin = ty1 < ty2 ? ty1 : ty2;
            float tyMax = ty1 > ty2 ? ty1 : ty2;
            if (tyMin > tmin) tmin = tyMin;
            if (tyMax < tmax) tmax = tyMax;
            if (tmax < tmin) return false;

            float tz1 = (minZ - origin.z) * invDir.z;
            float tz2 = (maxZ - origin.z) * invDir.z;
            float tzMin = tz1 < tz2 ? tz1 : tz2;
            float tzMax = tz1 > tz2 ? tz1 : tz2;
            if (tzMin > tmin) tmin = tzMin;
            if (tzMax < tmax) tmax = tzMax;

            return tmax >= 0f && tmin <= tmax && tmin <= maxT;
        }

        private static bool TryIntersectRayTriangle(
            Vector3 origin,
            Vector3 dir,
            Vector3 v0,
            Vector3 v1,
            Vector3 v2,
            out float t)
        {
            t = 0f;
            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;
            Vector3 h = Vector3.Cross(dir, e2);
            float a = Vector3.Dot(e1, h);
            if (Mathf.Abs(a) < 1e-6f) return false;

            float f = 1f / a;
            Vector3 s = origin - v0;
            float u = f * Vector3.Dot(s, h);
            if (u < 0f || u > 1f) return false;

            Vector3 q = Vector3.Cross(s, e1);
            float v = f * Vector3.Dot(dir, q);
            if (v < 0f || u + v > 1f) return false;

            t = f * Vector3.Dot(e2, q);
            return t > 0f;
        }

        private static void ConsiderCandidate(
            float hitY,
            Vector3 hitNormal,
            float expectedY,
            FloorSelectMode mode,
            ref bool found,
            ref float bestY,
            ref float bestScore,
            ref Vector3 bestNormal)
        {
            float score;
            switch (mode)
            {
                case FloorSelectMode.LowestInRange:
                    score = hitY;
                    break;
                case FloorSelectMode.HighestInRange:
                    score = -hitY;
                    break;
                default:
                    score = Mathf.Abs(hitY - expectedY);
                    break;
            }

            if (!found || score < bestScore)
            {
                found = true;
                bestY = hitY;
                bestScore = score;
                bestNormal = hitNormal;
            }
        }

        private static bool TryIntersectVerticalXZ(in SampledTriangle st, float px, float pz, out float hitY)
        {
            float invDenom = 1f / st.denomXZ;
            float w0 = ((st.v1.z - st.v2.z) * (px - st.v2.x) + (st.v2.x - st.v1.x) * (pz - st.v2.z)) * invDenom;
            float w1 = ((st.v2.z - st.v0.z) * (px - st.v2.x) + (st.v0.x - st.v2.x) * (pz - st.v2.z)) * invDenom;
            float w2 = 1f - w0 - w1;

            if (w0 >= -0.0005f && w1 >= -0.0005f && w2 >= -0.0005f)
            {
                hitY = w0 * st.v0.y + w1 * st.v1.y + w2 * st.v2.y;
                return true;
            }

            hitY = 0f;
            return false;
        }

        private static float DistancePointToTriangleXZSquared(
            float px, float pz,
            float ax, float az,
            float bx, float bz,
            float cx, float cz)
        {
            float d0 = DistancePointToSegment2DSquared(px, pz, ax, az, bx, bz);
            float d1 = DistancePointToSegment2DSquared(px, pz, bx, bz, cx, cz);
            float d2 = DistancePointToSegment2DSquared(px, pz, cx, cz, ax, az);
            return Mathf.Min(d0, Mathf.Min(d1, d2));
        }

        private static float DistancePointToSegment2DSquared(
            float px, float pz,
            float ax, float az,
            float bx, float bz)
        {
            float abx = bx - ax;
            float abz = bz - az;
            float apx = px - ax;
            float apz = pz - az;
            float lenSq = abx * abx + abz * abz;
            if (lenSq < 1e-8f)
            {
                return apx * apx + apz * apz;
            }

            float t = Mathf.Clamp01((apx * abx + apz * abz) / lenSq);
            float dx = px - (ax + abx * t);
            float dz = pz - (az + abz * t);
            return dx * dx + dz * dz;
        }

        private static void InsertIntoGrid(
            Dictionary<long, List<int>> grid,
            int triIndex,
            float minX, float maxX,
            float minZ, float maxZ)
        {
            int cMinX = Mathf.FloorToInt(minX / CellSize);
            int cMaxX = Mathf.FloorToInt(maxX / CellSize);
            int cMinZ = Mathf.FloorToInt(minZ / CellSize);
            int cMaxZ = Mathf.FloorToInt(maxZ / CellSize);

            if ((cMaxX - cMinX) > 128)
            {
                int midX = (cMinX + cMaxX) / 2;
                cMinX = midX - 64;
                cMaxX = midX + 64;
            }
            if ((cMaxZ - cMinZ) > 128)
            {
                int midZ = (cMinZ + cMaxZ) / 2;
                cMinZ = midZ - 64;
                cMaxZ = midZ + 64;
            }

            for (int x = cMinX; x <= cMaxX; x++)
            {
                for (int z = cMinZ; z <= cMaxZ; z++)
                {
                    long key = PackCellKey(x, z);
                    if (!grid.TryGetValue(key, out List<int> list))
                    {
                        list = new List<int>(16);
                        grid[key] = list;
                    }
                    list.Add(triIndex);
                }
            }
        }

        private static long PackCellKey(int cx, int cz)
        {
            return ((long)cx << 32) | (uint)cz;
        }

        private void DisposeTemporaryColliders()
        {
            for (int i = 0; i < _fallbackColliders.Count; i++)
            {
                if (_fallbackColliders[i].isTemporaryProxy && _fallbackColliders[i].tempOwner != null)
                {
                    DestroyImmediateSafe(_fallbackColliders[i].tempOwner);
                }
            }
            _fallbackColliders.Clear();
        }

        private static void DestroyImmediateSafe(UnityEngine.Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(obj);
                return;
            }
#endif
            UnityEngine.Object.Destroy(obj);
        }

        public void Dispose()
        {
            DisposeTemporaryColliders();
            _floorTris.Clear();
            _blockingTris.Clear();
            _wallTris.Clear();
            _allRaycastTris.Clear();
            _floorGrid.Clear();
            _blockingGrid.Clear();
            _wallGrid.Clear();
        }
    }
}
