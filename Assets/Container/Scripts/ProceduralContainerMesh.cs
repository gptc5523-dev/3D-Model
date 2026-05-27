using System.Collections.Generic;
using UnityEngine;

namespace ContainerProject
{
    /// <summary>
    /// 20ft Dry 컨테이너 절차적 메시 생성기.
    /// 기본 출력은 VR 미니어처 스케일(1/24, 약 0.252 × 0.102 × 0.108 m),
    /// 메시 중심(0,0,0)이 컨테이너의 가운데(잡기 좋은 위치). Forward: +Z = 도어.
    /// 서브메시: 0=Body, 1=Door, 2=Frame, 3=Castings.
    /// </summary>
    public static class ProceduralContainerMesh
    {
        // 기본 출력 스케일: VR 미니어처 (기존 SpawnContainers Std20과 동일 사이즈)
        public const float DefaultMiniatureScale = 1f / 24f;

        // 빌더 내부에서 사용하는 ISO 668 실측 (m). 마지막에 일괄 스케일됨.
        // 기본값 = 20ft Std (22G1). BuildSized() 로 다른 사이즈도 빌드 가능.
        public static float Length = 6.058f;
        public static float Width  = 2.438f;
        public static float Height = 2.591f;

        // 표준 사이즈 프리셋
        public const float Length20ft = 6.058f;
        public const float Length40ft = 12.192f;
        public const float HeightStd  = 2.591f;  // 8'6"
        public const float HeightHC   = 2.896f;  // 9'6" (High Cube)
        public const float StdWidth   = 2.438f;

        // 프레임 / 캐스팅 / 패널 치수
        const float CornerCastW = 0.178f;
        const float CornerCastH = 0.135f;
        const float CornerCastTopH = 0.150f;  // 상단 코너만 +15mm — 크레인 고리 거는 영역 강조
        const float CornerCastD = 0.162f;
        // ISO 1161 코너 캐스팅 구멍 (외측 3면) — 정사각형 75mm × 75mm
        const float CastHoleLong  = 0.075f;
        const float CastHoleShort = 0.075f;
        const float CastWallThick = 0.018f;  // 벽 두께 — 구멍이 안쪽으로 들어가는 recess 깊이
        const float RailH       = 0.092f;
        const float CornerPostW = 0.098f;
        // 패널 base가 컨테이너 외측에서 안쪽으로 들어간 깊이.
        // corrugated 외측 평면(+CorrDepth)이 컨테이너 외측면과 일치하도록 = CorrDepth와 같게.
        // 이러면 corrugated 산이 코너 캐스팅·포스트와 같은 평면 → 외관 시 틈이 사라짐.
        const float PanelInset  = 0.028f;

        // 주름판 (vertical corrugation)
        const float CorrDepth   = 0.028f;
        const float CorrFlatIn  = 0.060f;
        const float CorrFlatOut = 0.060f;
        const float CorrSlope   = 0.040f;

        // 도어
        const float DoorGap          = 0.004f;  // 도어 사이 틈 최소화
        const float LockBarDiameter  = 0.030f;
        const int   LockBarSides     = 8;
        const float HingeBlockW      = 0.055f;
        const float HingeBlockH      = 0.090f;
        const float HingeBlockD      = 0.050f;
        const int   HingesPerDoor    = 4;
        const int   LockBarsPerDoor  = 2;
        // 락바 부속
        const float LockHandleLen    = 0.140f;  // 회전 손잡이 길이
        const float LockHandleThick  = 0.020f;
        const float LockGuardW       = 0.040f;
        const float LockGuardH       = 0.180f;
        const float LockGuardD       = 0.018f;
        const float LockCamSize      = 0.045f;  // 락바 상단/하단 캠 (원기둥 직경/높이)
        // 락바 마운트 브래킷 (도어 표면에 락바를 잡아주는 클램프)
        const int   LockBracketsPerBar = 2;
        const float LockBracketW       = 0.050f;
        const float LockBracketH       = 0.020f;
        const float LockBracketD       = 0.060f;  // Z 깊이 — 도어 외측면부터 락바 너머까지
        // 도어 측면 빔 (도어 외측 모서리의 평평한 세로 띠 — 힌지가 여기 붙음)
        const float SideBeamW          = 0.080f;
        // 도어 헤더 (얇게)
        const float DoorHeaderHeight = 0.025f;
        const float DoorHeaderDepth  = 0.020f;
        // ID/CSC plate
        const float IdPlateW         = 0.300f;
        const float IdPlateH         = 0.180f;
        const float CscPlateW        = 0.140f;
        const float CscPlateH        = 0.100f;
        const float PlateOut         = 0.003f;

        // 지붕 캠버 (현재 corrugated 지붕 사용 — 캠버는 미사용)
        const float RoofCamber = 0.018f;
        const float RoofCorrDepth = 0.020f;  // 지붕 코르게이션 깊이 (산이 캐스팅 top 직전까지 솟음)

        /// <summary>
        /// 절차적 메시 생성.
        /// 기본 출력: 미니어처 스케일(1/24) + 중심 피봇 + X축이 긴 방향(도어=+X).
        /// 이는 기존 SpawnContainers Std20과 동일 좌표계.
        /// 현재 Length/Width/Height 상수 기반 — 다른 사이즈는 BuildSized() 사용.
        /// </summary>
        public static Mesh Build(
            string meshName = "Container_20ft_Procedural",
            float scale = DefaultMiniatureScale,
            bool centerPivot = true,
            bool xIsLength = true)
        {
            var b = new MeshBuilder();

            BuildCornerCastings(b);
            BuildFrame(b);
            BuildBodyPanels(b);
            BuildRoof(b);
            BuildFloor(b);
            BuildDoors(b);

            var mesh = b.ToMesh(meshName);
            ApplyTransform(mesh, scale, centerPivot, xIsLength);
            return mesh;
        }

        /// <summary>
        /// 임의 사이즈로 컨테이너 빌드. 표준 사이즈는 Length20ft/Length40ft, HeightStd/HeightHC 상수 사용.
        /// 내부적으로 정적 Length/Width/Height 를 잠시 바꿔서 Build() 호출 후 복구.
        /// </summary>
        public static Mesh BuildSized(
            float length, float width, float height,
            string meshName = "Container_Procedural",
            float scale = DefaultMiniatureScale,
            bool centerPivot = true,
            bool xIsLength = true)
        {
            float savedL = Length, savedW = Width, savedH = Height;
            Length = length; Width = width; Height = height;
            try
            {
                return Build(meshName, scale, centerPivot, xIsLength);
            }
            finally
            {
                Length = savedL; Width = savedW; Height = savedH;
            }
        }

        static void ApplyTransform(Mesh mesh, float scale, bool centerPivot, bool xIsLength)
        {
            if (scale == 1f && !centerPivot && !xIsLength) return;

            var verts = mesh.vertices;
            float yShift = centerPivot ? -Height * 0.5f : 0f;
            for (int i = 0; i < verts.Length; i++)
            {
                var v = verts[i];
                v.y += yShift;
                if (xIsLength)
                {
                    // Y축 -90도 회전: (x, y, z) → (z, y, -x). 도어=+Z였던 게 도어=+X가 됨.
                    v = new Vector3(v.z, v.y, -v.x);
                }
                v *= scale;
                verts[i] = v;
            }
            mesh.vertices = verts;

            if (xIsLength)
            {
                var normals = mesh.normals;
                for (int i = 0; i < normals.Length; i++)
                {
                    var n = normals[i];
                    normals[i] = new Vector3(n.z, n.y, -n.x);
                }
                mesh.normals = normals;
                mesh.RecalculateTangents();
            }
            mesh.RecalculateBounds();
        }

        // ───────────────────────────── 코너 캐스팅 ─────────────────────────────
        static void BuildCornerCastings(MeshBuilder b)
        {
            float hx = Width  * 0.5f;
            float hz = Length * 0.5f;
            // 8개 캐스팅: 위(+Y top) / 아래(0 bottom), 4 모서리. 외측 3면에 ISO 1161 구멍.
            // 상단은 CornerCastTopH(0.150) — 아래 면은 Height-CornerCastH에 유지, 위로 (TopH-H)만큼 더 솟음.
            // 하단은 CornerCastH(0.135) 유지.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            for (int sy = 0; sy <= 1; sy++)
            {
                float x = sx * (hx - CornerCastW * 0.5f);
                float z = sz * (hz - CornerCastD * 0.5f);
                bool isTop = sy == 1;
                float castH = isTop ? CornerCastTopH : CornerCastH;
                float y = isTop
                    ? Height - CornerCastH + castH * 0.5f  // 상단: 바닥 Y=Height-CornerCastH 고정, 위로 castH만큼
                    : castH * 0.5f;                        // 하단: Y=0 부터 castH 만큼
                AddCornerCastingWithHoles(b, submesh: 3,
                    center: new Vector3(x, y, z),
                    size:   new Vector3(CornerCastW, castH, CornerCastD),
                    outX: sx, outZ: sz, outYUp: isTop);
            }
        }

        // 외측 3면 (컨테이너 바깥쪽 X/Z/Y) 에 사각 구멍이 있는 코너 캐스팅.
        // 내측 3면은 솔리드. outX/outZ: ±1, outYUp: true이면 상단 캐스팅 (+Y 외측).
        static void AddCornerCastingWithHoles(MeshBuilder b, int submesh,
            Vector3 center, Vector3 size, int outX, int outZ, bool outYUp)
        {
            Vector3 h = size * 0.5f;
            Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
            Vector3 p100 = center + new Vector3( h.x, -h.y, -h.z);
            Vector3 p110 = center + new Vector3( h.x,  h.y, -h.z);
            Vector3 p010 = center + new Vector3(-h.x,  h.y, -h.z);
            Vector3 p001 = center + new Vector3(-h.x, -h.y,  h.z);
            Vector3 p101 = center + new Vector3( h.x, -h.y,  h.z);
            Vector3 p111 = center + new Vector3( h.x,  h.y,  h.z);
            Vector3 p011 = center + new Vector3(-h.x,  h.y,  h.z);

            // 내측 3면 (컨테이너 내부 향함 — 솔리드)
            if (outX > 0) AddFlatQuad(b, submesh, p000, p001, p011, p010, Vector3.left);
            else          AddFlatQuad(b, submesh, p101, p100, p110, p111, Vector3.right);

            if (outZ > 0) AddFlatQuad(b, submesh, p100, p000, p010, p110, Vector3.back);
            else          AddFlatQuad(b, submesh, p001, p101, p111, p011, Vector3.forward);

            if (outYUp)   AddFlatQuad(b, submesh, p000, p100, p101, p001, Vector3.down);
            else          AddFlatQuad(b, submesh, p011, p111, p110, p010, Vector3.up);

            // 외측 X 면 — 구멍 long axis 은 face-local right (= 컨테이너 Z = 길이)
            if (outX > 0)
                AddFaceWithRectHole(b, submesh, p101, p100, p110, p111, Vector3.right,
                    CastHoleLong / size.z, CastHoleShort / size.y, CastWallThick);
            else
                AddFaceWithRectHole(b, submesh, p000, p001, p011, p010, Vector3.left,
                    CastHoleLong / size.z, CastHoleShort / size.y, CastWallThick);

            // 외측 Z 면 — 구멍 long axis 은 face-local right (= 컨테이너 X = 폭)
            if (outZ > 0)
                AddFaceWithRectHole(b, submesh, p001, p101, p111, p011, Vector3.forward,
                    CastHoleLong / size.x, CastHoleShort / size.y, CastWallThick);
            else
                AddFaceWithRectHole(b, submesh, p100, p000, p010, p110, Vector3.back,
                    CastHoleLong / size.x, CastHoleShort / size.y, CastWallThick);

            // 외측 Y 면 — 구멍 long axis 은 face-local up (= 컨테이너 Z = 길이), short = X
            if (outYUp)
                AddFaceWithRectHole(b, submesh, p011, p111, p110, p010, Vector3.up,
                    CastHoleShort / size.x, CastHoleLong / size.z, CastWallThick);
            else
                AddFaceWithRectHole(b, submesh, p000, p100, p101, p001, Vector3.down,
                    CastHoleShort / size.x, CastHoleLong / size.z, CastWallThick);
        }

        // 사각형 면 (c00→c10→c11→c01 CCW from +normal) 에 사각 구멍을 뚫고,
        // holeDepth 만큼 안쪽으로 들어간 뒤 닫는 recess 생성.
        // holeRightFrac/holeUpFrac: 구멍 크기 (face dimension 대비 0~1, 중앙 정렬)
        static void AddFaceWithRectHole(MeshBuilder b, int submesh,
            Vector3 c00, Vector3 c10, Vector3 c11, Vector3 c01, Vector3 normal,
            float holeRightFrac, float holeUpFrac, float holeDepth)
        {
            float u0 = (1f - holeRightFrac) * 0.5f;
            float u1 = 1f - u0;
            float v0 = (1f - holeUpFrac)    * 0.5f;
            float v1 = 1f - v0;

            Vector3 right = c10 - c00;
            Vector3 up    = c01 - c00;

            Vector3 onLeftTop  = c00 +              up * v1;
            Vector3 onLeftBot  = c00 +              up * v0;
            Vector3 onRightTop = c00 + right +      up * v1;
            Vector3 onRightBot = c00 + right +      up * v0;
            Vector3 hBL = c00 + right * u0 + up * v0;
            Vector3 hBR = c00 + right * u1 + up * v0;
            Vector3 hTR = c00 + right * u1 + up * v1;
            Vector3 hTL = c00 + right * u0 + up * v1;

            // 외측 면 — 구멍 주위 4 strip (모서리 중복 없음)
            AddFlatQuad(b, submesh, onLeftTop, onRightTop, c11, c01, normal);    // 상단 (full width)
            AddFlatQuad(b, submesh, c00, c10, onRightBot, onLeftBot, normal);    // 하단 (full width)
            AddFlatQuad(b, submesh, onLeftBot, hBL, hTL, onLeftTop, normal);     // 좌측 (구멍 사이만)
            AddFlatQuad(b, submesh, hBR, onRightBot, onRightTop, hTR, normal);   // 우측 (구멍 사이만)

            // 구멍 안쪽 4 벽 + 뒷면
            Vector3 backOffset = -normal * holeDepth;
            Vector3 bhBL = hBL + backOffset;
            Vector3 bhBR = hBR + backOffset;
            Vector3 bhTR = hTR + backOffset;
            Vector3 bhTL = hTL + backOffset;
            Vector3 upN    = up.normalized;
            Vector3 rightN = right.normalized;

            AddFlatQuad(b, submesh, hTR, hTL, bhTL, bhTR, -upN);      // 위쪽 벽 (구멍 안에서 보면 천장)
            AddFlatQuad(b, submesh, hBL, hBR, bhBR, bhBL,  upN);      // 아래쪽 벽
            AddFlatQuad(b, submesh, hTL, hBL, bhBL, bhTL,  rightN);   // 좌측 벽
            AddFlatQuad(b, submesh, hBR, hTR, bhTR, bhBR, -rightN);   // 우측 벽
            AddFlatQuad(b, submesh, bhBL, bhBR, bhTR, bhTL, normal);  // 뒷면 (recess 바닥)
        }

        // 4-vertex flat quad (CCW from +normal). 기존 MeshBuilder.AddFace 와 동일 기능을 외부에서 호출 가능하게 노출.
        static void AddFlatQuad(MeshBuilder mb, int submesh,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int ia = mb.AddVertex(a, normal, new Vector2(0f, 0f));
            int ib = mb.AddVertex(b, normal, new Vector2(1f, 0f));
            int ic = mb.AddVertex(c, normal, new Vector2(1f, 1f));
            int id = mb.AddVertex(d, normal, new Vector2(0f, 1f));
            mb.AddQuad(submesh, ia, ib, ic, id);
        }

        // ───────────────────────────── 프레임 ─────────────────────────────
        static void BuildFrame(MeshBuilder b)
        {
            float hx = Width  * 0.5f;
            float hz = Length * 0.5f;

            // Bottom side rails (좌/우 길이 방향)
            float bottomRailY = CornerCastH * 0.5f;
            float railZSpan   = Length - CornerCastD * 2f;
            float endRailXSpan= Width  - CornerCastW * 2f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(2,
                    center: new Vector3(sx * (hx - CornerPostW * 0.5f), bottomRailY, 0f),
                    size:   new Vector3(CornerPostW, RailH, railZSpan));
            }
            // Top side rails
            float topRailY = Height - CornerCastH * 0.5f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(2,
                    center: new Vector3(sx * (hx - CornerPostW * 0.5f), topRailY, 0f),
                    size:   new Vector3(CornerPostW, RailH, railZSpan));
            }
            // Bottom end rails (전/후 폭 방향)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(2,
                    center: new Vector3(0f, bottomRailY, sz * (hz - CornerPostW * 0.5f)),
                    size:   new Vector3(endRailXSpan, RailH, CornerPostW));
            }
            // Top end rails (header)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(2,
                    center: new Vector3(0f, topRailY, sz * (hz - CornerPostW * 0.5f)),
                    size:   new Vector3(endRailXSpan, RailH, CornerPostW));
            }
            // Corner posts (4개, 수직). 두 코너 캐스팅 사이를 정확히 채우도록 컨테이너 정중앙에 배치.
            float postY = Height * 0.5f;
            float postHeight = Height - CornerCastH * 2f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(2,
                    center: new Vector3(sx * (hx - CornerPostW * 0.5f), postY, sz * (hz - CornerPostW * 0.5f)),
                    size:   new Vector3(CornerPostW, postHeight, CornerPostW));
            }
        }

        // ───────────────────────────── 본체 패널 (좌/우/전) ─────────────────────────────
        static void BuildBodyPanels(MeshBuilder b)
        {
            // 레일 중심 Y = CornerCastH * 0.5f, 높이 = RailH.
            // 패널이 상/하 레일 안쪽 면과 맞닿게 — 틈 제거.
            float panelTop    = Height - CornerCastH * 0.5f - RailH * 0.5f;
            float panelBottom = CornerCastH * 0.5f + RailH * 0.5f;
            float panelHeight = panelTop - panelBottom;

            // PanelInset은 깊이 방향(외측면에서 안쪽)에만; 길이/폭 방향은 전체 dimension 사용해야
            // 코너 포스트 안쪽 면까지 패널이 닿음.
            float depthInsetX = Width  * 0.5f - PanelInset;  // 좌/우 패널 base X (절대값)
            float depthInsetZ = Length * 0.5f - PanelInset;  // 전면 패널 base Z (절대값)
            float halfX = Width  * 0.5f;
            float halfZ = Length * 0.5f;

            // 코너 포스트 안쪽 면에 패널이 거의 맞닿게 (틈 최소화)
            const float postInset = 0.002f;

            // 좌측면 (x = -depthInsetX, 법선 -X). right=+Z, up=+Y → Cross=-X = outward
            BuildCorrugatedPanel(b, submesh: 0,
                origin: new Vector3(-depthInsetX, panelBottom, -halfZ + CornerPostW + postInset),
                right:  new Vector3(0f, 0f, 1f),
                up:     new Vector3(0f, 1f, 0f),
                width:  Length - (CornerPostW + postInset) * 2f,
                height: panelHeight,
                depth:  CorrDepth);

            // 우측면 (x = +depthInsetX, 법선 +X). right=-Z, up=+Y → Cross=+X = outward
            BuildCorrugatedPanel(b, submesh: 0,
                origin: new Vector3(depthInsetX, panelBottom, halfZ - CornerPostW - postInset),
                right:  new Vector3(0f, 0f, -1f),
                up:     new Vector3(0f, 1f, 0f),
                width:  Length - (CornerPostW + postInset) * 2f,
                height: panelHeight,
                depth:  CorrDepth);

            // 전면 고정벽 (z = -depthInsetZ, 법선 -Z). right=-X, up=+Y → Cross=-Z = outward
            BuildCorrugatedPanel(b, submesh: 0,
                origin: new Vector3(halfX - CornerPostW - postInset, panelBottom, -depthInsetZ),
                right:  new Vector3(-1f, 0f, 0f),
                up:     new Vector3(0f, 1f, 0f),
                width:  Width - (CornerPostW + postInset) * 2f,
                height: panelHeight,
                depth:  CorrDepth);
        }

        /// <summary>
        /// 수직 주름판(corrugated panel) 한 장을 생성.
        /// origin = 좌하단 모서리, right = 폭 방향, up = 높이 방향.
        /// outward 법선은 Cross(right, up) 방향이어야 하므로 호출자가 그렇게 right/up을 선택해야 함.
        /// </summary>
        static void BuildCorrugatedPanel(MeshBuilder b, int submesh,
            Vector3 origin, Vector3 right, Vector3 up,
            float width, float height, float depth)
        {
            Vector3 outDir = Vector3.Cross(right, up).normalized;

            // 한 주기 = flatIn + slope + flatOut + slope
            float period = CorrFlatIn + CorrSlope + CorrFlatOut + CorrSlope;
            int periods = Mathf.Max(1, Mathf.RoundToInt(width / period));
            float actualPeriod = width / periods;
            // 비율 유지하면서 폭에 맞춤
            float scale = actualPeriod / period;
            float fIn  = CorrFlatIn  * scale;
            float fOut = CorrFlatOut * scale;
            float slp  = CorrSlope   * scale;

            // 단면을 따라 (x = 진행 거리, d = 깊이) 노드 생성. 각 노드는 (offset, outwardOffset, normal).
            // 4 segments per period:
            //   [0] flatIn   (d=0,     normal=outDir)
            //   [1] slope ↑  (d=depth, normal=outDir tilted)
            //   [2] flatOut  (d=depth, normal=outDir)
            //   [3] slope ↓  (d=0,     normal=outDir tilted)
            // 마지막 폐쇄 노드 추가 (안쪽으로 복귀)

            var profile = new List<(float along, float outOff, Vector3 normal)>();
            float along = 0f;
            // slope 노드의 normal 기울기
            Vector3 slopeUpNormal   = (outDir * slp + right * depth).normalized;
            Vector3 slopeDownNormal = (outDir * slp - right * depth).normalized;

            for (int p = 0; p < periods; p++)
            {
                // flatIn start
                profile.Add((along, 0f, outDir));
                along += fIn;
                profile.Add((along, 0f, outDir));
                // slope up
                profile.Add((along, 0f, slopeUpNormal));
                along += slp;
                profile.Add((along, depth, slopeUpNormal));
                // flatOut
                profile.Add((along, depth, outDir));
                along += fOut;
                profile.Add((along, depth, outDir));
                // slope down
                profile.Add((along, depth, slopeDownNormal));
                along += slp;
                profile.Add((along, 0f, slopeDownNormal));
            }
            // 마지막 폐쇄 (다음 주기 시작점이 안쪽 평면이므로 자연스럽게 종료)

            // 위/아래 두 줄의 vertex 생성, segment마다 quad 1개
            // along 정규화 → U, height 정규화 → V
            int[] bottomIdx = new int[profile.Count];
            int[] topIdx    = new int[profile.Count];
            for (int i = 0; i < profile.Count; i++)
            {
                var (al, oo, nrm) = profile[i];
                Vector3 basePos = origin + right * al;
                Vector3 outOff  = outDir * oo;
                float u = al / width;
                bottomIdx[i] = b.AddVertex(basePos + outOff,          nrm, new Vector2(u, 0f));
                topIdx[i]    = b.AddVertex(basePos + outOff + up * height, nrm, new Vector2(u, 1f));
            }
            // segment 단위로 quad. (bottom_i, bottom_i+1, top_i+1, top_i) 순서로 winding하면
            // normal이 Cross(right, up) 방향 = outDir로 자동 정렬됨.
            for (int i = 0; i < profile.Count - 1; i++)
            {
                b.AddQuad(submesh, bottomIdx[i], bottomIdx[i + 1], topIdx[i + 1], topIdx[i]);
            }
        }

        // ───────────────────────────── 지붕 (corrugated) ─────────────────────────────
        static void BuildRoof(MeshBuilder b)
        {
            // ISO 컨테이너 지붕: 산/골이 폭(X) 방향으로 길게 흘러가고, 길이(Z) 방향으로 산/골 반복 (가로 줄무늬).
            // 천장 산이 rail top 보다 5mm 아래에 위치 — 끝 레일이 천장 끝을 덮어줘서 앞뒤 마감이 깔끔.
            float hx = Width  * 0.5f;
            float hz = Length * 0.5f;
            float railTopY = Height - CornerCastH * 0.5f + RailH * 0.5f;
            float baseY = railTopY - RoofCorrDepth - 0.005f;  // 골 + 깊이 + 5mm 여유 = 산이 rail top 보다 5mm 낮음

            // 산/골이 폭(X) 방향으로 길게 흐름 — 문에서 봤을 때 가로 줄무늬로 보임.
            // right=+Z (코르게이션 프로파일이 길이 방향으로 진행), up=+X (각 산이 -X→+X로 길게 흐름)
            // outDir = Cross(+Z, +X) = +Y (지붕은 위로 향함)
            BuildCorrugatedPanel(b, submesh: 0,
                origin: new Vector3(-hx, baseY, -hz),
                right:  new Vector3(0f, 0f, 1f),
                up:     new Vector3(1f, 0f, 0f),
                width:  Length,
                height: Width,
                depth:  RoofCorrDepth);
        }

        // ───────────────────────────── 바닥 ─────────────────────────────
        static void BuildFloor(MeshBuilder b)
        {
            // 외측 경계를 코너 캐스팅 외측면까지 확장 (이전: -0.002 → 2mm 갭)
            float hx = Width  * 0.5f;
            float hz = Length * 0.5f;

            // 외측 바닥 (normal -Y, 컨테이너 아래에서 보임)
            // 천장이 rail top 5mm 아래로 들어간 것과 대칭 — 바닥도 rail bottom 5mm 위로 올림.
            // 끝 레일/사이드 레일이 외측에서 바닥 가장자리를 덮어줌 (앞뒤/좌우 마감).
            float yOut = CornerCastH * 0.5f - RailH * 0.5f + 0.005f;  // = railBottomY + 5mm
            int a = b.AddVertex(new Vector3(-hx, yOut, -hz), Vector3.down, new Vector2(0f, 0f));
            int b1 = b.AddVertex(new Vector3( hx, yOut, -hz), Vector3.down, new Vector2(1f, 0f));
            int c1 = b.AddVertex(new Vector3( hx, yOut,  hz), Vector3.down, new Vector2(1f, 1f));
            int d1 = b.AddVertex(new Vector3(-hx, yOut,  hz), Vector3.down, new Vector2(0f, 1f));
            // (a, b, c, d) winding → normal = -Y
            b.AddQuad(0, a, b1, c1, d1);

            // 내측 바닥 (normal +Y, 도어 열렸을 때 내부에서 보임)
            // panelBottom과 동일한 높이로 측면 패널 하단과 일치 (이전: CornerCastH=0.118 → 측면 패널 0.105와 13mm 단차)
            float yIn = CornerCastH * 0.5f + RailH * 0.5f;
            int e = b.AddVertex(new Vector3(-hx, yIn, -hz), Vector3.up, new Vector2(0f, 0f));
            int f = b.AddVertex(new Vector3( hx, yIn, -hz), Vector3.up, new Vector2(1f, 0f));
            int g = b.AddVertex(new Vector3( hx, yIn,  hz), Vector3.up, new Vector2(1f, 1f));
            int h = b.AddVertex(new Vector3(-hx, yIn,  hz), Vector3.up, new Vector2(0f, 1f));
            // 역 winding → normal = +Y
            b.AddQuad(0, e, h, g, f);
        }

        // ───────────────────────────── 도어 (후면) ─────────────────────────────
        static void BuildDoors(MeshBuilder b)
        {
            // BuildBodyPanels와 동일 — 상/하 레일 안쪽 면에 맞춤.
            float panelTop    = Height - CornerCastH * 0.5f - RailH * 0.5f;
            float panelBottom = CornerCastH * 0.5f + RailH * 0.5f;
            float panelHeight = panelTop - panelBottom;
            float panelMidY   = (panelTop + panelBottom) * 0.5f;

            float halfX = Width * 0.5f;
            float doorZ = Length * 0.5f; // 후면 outer face (+Z); 락바/힌지/플레이트 기준점
            float doorPanelZ = doorZ - PanelInset; // 패널 base — corrugation peak이 doorZ(외측면)에 닿음

            const float postInset = 0.002f;
            float fullWidth = Width - (CornerPostW + postInset) * 2f;
            float doorWidth = (fullWidth - DoorGap) * 0.5f;
            float doorStartLeft = -halfX + CornerPostW + postInset;
            // 코르게이션 폭 = 도어 폭 - 측면 빔 폭 (각 도어 외측에 빔 1개)
            float corrWidth = doorWidth - SideBeamW;

            // ─── 도어 측면 빔 (각 도어 외측 모서리의 평평한 세로 띠) ───
            float leftBeamX  = doorStartLeft + SideBeamW * 0.5f;
            float rightBeamX = doorStartLeft + fullWidth - SideBeamW * 0.5f;
            b.AddBox(1,
                center: new Vector3(leftBeamX, panelMidY, doorZ - PanelInset * 0.5f),
                size:   new Vector3(SideBeamW, panelHeight, PanelInset));
            b.AddBox(1,
                center: new Vector3(rightBeamX, panelMidY, doorZ - PanelInset * 0.5f),
                size:   new Vector3(SideBeamW, panelHeight, PanelInset));

            // ─── 코르게이션 패널 (각 도어 빔 옆 corrWidth 만큼) ───
            // 좌측 도어 코르게이션 (빔 다음, 도어 내측 끝까지)
            BuildCorrugatedPanel(b, submesh: 1,
                origin: new Vector3(doorStartLeft + SideBeamW, panelBottom, doorPanelZ),
                right:  new Vector3(1f, 0f, 0f),
                up:     new Vector3(0f, 1f, 0f),
                width:  corrWidth,
                height: panelHeight,
                depth:  CorrDepth);
            // 우측 도어 코르게이션 (도어 내측 시작 → 빔 직전)
            BuildCorrugatedPanel(b, submesh: 1,
                origin: new Vector3(doorStartLeft + doorWidth + DoorGap, panelBottom, doorPanelZ),
                right:  new Vector3(1f, 0f, 0f),
                up:     new Vector3(0f, 1f, 0f),
                width:  corrWidth,
                height: panelHeight,
                depth:  CorrDepth);

            // ─── 락바 + 캠(원기둥) + 손잡이 + 가드 + 마운트 브래킷 ───
            float lockBarZ = doorZ + 0.040f;
            float camRadius = LockCamSize * 0.5f;
            for (int doorSide = 0; doorSide < 2; doorSide++)
            {
                // 락바는 코르게이션 영역에 분산 — 빔 위가 아니라 코르게이션 위에 위치
                float corrStartX = (doorSide == 0)
                    ? doorStartLeft + SideBeamW                    // 좌측: 빔 뒤
                    : doorStartLeft + doorWidth + DoorGap;         // 우측: 내측 시작
                for (int bar = 0; bar < LockBarsPerDoor; bar++)
                {
                    float t = (bar + 1f) / (LockBarsPerDoor + 1f);
                    float x = corrStartX + corrWidth * t;

                    // 수직 락바
                    AddVerticalCylinder(b, submesh: 2,
                        bottom: new Vector3(x, panelBottom, lockBarZ),
                        height: panelHeight,
                        radius: LockBarDiameter * 0.5f);

                    // 상/하 캠 (원기둥 — 락바보다 굵음)
                    AddVerticalCylinder(b, submesh: 2,
                        bottom: new Vector3(x, panelBottom, lockBarZ),
                        height: LockCamSize,
                        radius: camRadius);
                    AddVerticalCylinder(b, submesh: 2,
                        bottom: new Vector3(x, panelTop - LockCamSize, lockBarZ),
                        height: LockCamSize,
                        radius: camRadius);

                    // 회전 손잡이 (락바 중앙 — 한 도어의 두 바는 같은 외측 방향)
                    float handleSide = (doorSide == 0) ? -1f : 1f;
                    b.AddBox(2,
                        center: new Vector3(x + handleSide * LockHandleLen * 0.5f,
                                            panelMidY, lockBarZ + LockHandleThick * 0.5f),
                        size:   new Vector3(LockHandleLen, LockHandleThick, LockHandleThick));

                    // 손잡이 보호대
                    b.AddBox(2,
                        center: new Vector3(x + handleSide * LockHandleLen * 0.5f,
                                            panelMidY, lockBarZ + LockGuardD),
                        size:   new Vector3(LockGuardW, LockGuardH, LockGuardD));

                    // 마운트 브래킷 (락바를 도어 표면에 잡아주는 클램프) — 캠과 손잡이 사이에 2개
                    float bracketCenterZ = doorZ + LockBracketD * 0.5f;
                    for (int br = 0; br < LockBracketsPerBar; br++)
                    {
                        float bt = (br + 1f) / (LockBracketsPerBar + 1f);
                        float by = panelBottom + panelHeight * bt;
                        b.AddBox(2,
                            center: new Vector3(x, by, bracketCenterZ),
                            size:   new Vector3(LockBracketW, LockBracketH, LockBracketD));
                    }
                }
            }

            // ─── 힌지 (측면 빔 중심에 부착) ───
            for (int doorSide = 0; doorSide < 2; doorSide++)
            {
                float hingeX = (doorSide == 0) ? leftBeamX : rightBeamX;
                for (int h = 0; h < HingesPerDoor; h++)
                {
                    float t = (h + 1f) / (HingesPerDoor + 1f);
                    float y = panelBottom + panelHeight * t;
                    // 힌지 본체
                    b.AddBox(2,
                        center: new Vector3(hingeX, y, doorZ + HingeBlockD * 0.5f),
                        size:   new Vector3(HingeBlockW, HingeBlockH, HingeBlockD));
                    // 힌지 핀 (외측)
                    AddVerticalCylinder(b, submesh: 2,
                        bottom: new Vector3(hingeX, y - HingeBlockH * 0.6f,
                                            doorZ + HingeBlockD + LockBarDiameter * 0.3f),
                        height: HingeBlockH * 1.2f,
                        radius: LockBarDiameter * 0.4f);
                }
            }

            // 도어 헤더 (얇게). X는 두 코너 캐스팅 사이 (≠ 도어 폭 fullWidth — 그러면 캐스팅 안으로 파고듦).
            // Z는 캐스팅 외측면(doorZ) 안쪽에 배치 — 외측면 밖으로 튀어나오지 않도록.
            float headerWidth = Width - CornerCastW * 2f;
            b.AddBox(2,
                center: new Vector3(0f, panelTop + DoorHeaderHeight * 0.5f, doorZ - DoorHeaderDepth * 0.5f),
                size:   new Vector3(headerWidth, DoorHeaderHeight, DoorHeaderDepth));

            // ID Plate (우측 도어에 큰 사각 패널 — 컨테이너 번호용)
            float idPlateX = doorStartLeft + doorWidth + DoorGap + doorWidth * 0.5f;
            float idPlateY = panelTop - IdPlateH * 0.5f - 0.06f;
            b.AddBox(0,
                center: new Vector3(idPlateX, idPlateY, doorZ + PlateOut * 0.5f),
                size:   new Vector3(IdPlateW, IdPlateH, PlateOut));

            // CSC Plate (좌측 도어 하단 — 안전 인증판)
            float cscX = doorStartLeft + doorWidth * 0.5f;
            float cscY = panelBottom + CscPlateH * 0.5f + 0.04f;
            b.AddBox(0,
                center: new Vector3(cscX, cscY, doorZ + PlateOut * 0.5f),
                size:   new Vector3(CscPlateW, CscPlateH, PlateOut));
        }

        // ───────────────────────────── 수직 원기둥 (락바) ─────────────────────────────
        static void AddVerticalCylinder(MeshBuilder b, int submesh, Vector3 bottom, float height, float radius)
        {
            int sides = LockBarSides;
            int[] bot = new int[sides];
            int[] top = new int[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (float)i / sides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 pb = bottom + dir * radius;
                Vector3 pt = pb + Vector3.up * height;
                Vector2 uv = new Vector2((float)i / sides, 0f);
                bot[i] = b.AddVertex(pb, dir, uv);
                top[i] = b.AddVertex(pt, dir, new Vector2((float)i / sides, 1f));
            }
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.AddQuad(submesh, bot[i], bot[j], top[j], top[i]);
            }
            // 캡 (단순 fan). dir_i = (cos(i), 0, sin(i)), i가 증가하면 시계반대로 도는데,
            // 아래 캡은 normal=-Y여야 하므로 (center, i, j) 순서, 위 캡은 +Y이므로 (center, j, i) 순서.
            int botCenter = b.AddVertex(bottom, Vector3.down, new Vector2(0.5f, 0.5f));
            int topCenter = b.AddVertex(bottom + Vector3.up * height, Vector3.up, new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                b.AddTriangle(submesh, botCenter, bot[i], bot[j]); // 아래 캡 (-Y)
                b.AddTriangle(submesh, topCenter, top[j], top[i]); // 위 캡 (+Y)
            }
        }

        // ───────────────────────────── MeshBuilder ─────────────────────────────
        sealed class MeshBuilder
        {
            readonly List<Vector3> _verts   = new List<Vector3>(8192);
            readonly List<Vector3> _normals = new List<Vector3>(8192);
            readonly List<Vector2> _uvs     = new List<Vector2>(8192);
            readonly Dictionary<int, List<int>> _tris = new Dictionary<int, List<int>>();

            public int AddVertex(Vector3 p, Vector3 n, Vector2 uv)
            {
                _verts.Add(p);
                _normals.Add(n.sqrMagnitude > 0f ? n.normalized : Vector3.up);
                _uvs.Add(uv);
                return _verts.Count - 1;
            }

            public void AddTriangle(int submesh, int a, int b, int c)
            {
                if (!_tris.TryGetValue(submesh, out var list))
                {
                    list = new List<int>(4096);
                    _tris[submesh] = list;
                }
                list.Add(a); list.Add(b); list.Add(c);
            }

            public void AddQuad(int submesh, int a, int b, int c, int d)
            {
                AddTriangle(submesh, a, b, c);
                AddTriangle(submesh, a, c, d);
            }

            public void AddBox(int submesh, Vector3 center, Vector3 size)
            {
                Vector3 h = size * 0.5f;
                // 8 corners
                Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
                Vector3 p100 = center + new Vector3( h.x, -h.y, -h.z);
                Vector3 p110 = center + new Vector3( h.x,  h.y, -h.z);
                Vector3 p010 = center + new Vector3(-h.x,  h.y, -h.z);
                Vector3 p001 = center + new Vector3(-h.x, -h.y,  h.z);
                Vector3 p101 = center + new Vector3( h.x, -h.y,  h.z);
                Vector3 p111 = center + new Vector3( h.x,  h.y,  h.z);
                Vector3 p011 = center + new Vector3(-h.x,  h.y,  h.z);

                // 6 faces, 면마다 4 vertex (flat shading)
                AddFace(submesh, p001, p101, p111, p011, Vector3.forward);
                AddFace(submesh, p100, p000, p010, p110, Vector3.back);
                AddFace(submesh, p101, p100, p110, p111, Vector3.right);
                AddFace(submesh, p000, p001, p011, p010, Vector3.left);
                AddFace(submesh, p011, p111, p110, p010, Vector3.up);
                AddFace(submesh, p000, p100, p101, p001, Vector3.down);
            }

            void AddFace(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                int ia = AddVertex(a, normal, new Vector2(0f, 0f));
                int ib = AddVertex(b, normal, new Vector2(1f, 0f));
                int ic = AddVertex(c, normal, new Vector2(1f, 1f));
                int id = AddVertex(d, normal, new Vector2(0f, 1f));
                AddQuad(submesh, ia, ib, ic, id);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                if (_verts.Count > 65535)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.SetVertices(_verts);
                mesh.SetNormals(_normals);
                mesh.SetUVs(0, _uvs);

                int maxSub = 0;
                foreach (var k in _tris.Keys) if (k > maxSub) maxSub = k;
                mesh.subMeshCount = maxSub + 1;
                for (int s = 0; s <= maxSub; s++)
                {
                    if (_tris.TryGetValue(s, out var list))
                        mesh.SetTriangles(list, s);
                    else
                        mesh.SetTriangles(System.Array.Empty<int>(), s);
                }

                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
