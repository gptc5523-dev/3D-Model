#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Container.Crane.Sts.EditorTools
{
    /// <summary>
    /// 메뉴에서 STS(Ship-To-Shore) Crane GameObject 계층을 자동 생성.
    ///
    /// 생성되는 hierarchy:
    ///     STS_Crane                       (StsCrane) — 다리·포털·A프레임·스테이 케이블(정적)
    ///       └─ Boom                       (붐 거더 + 기계실, 정적)
    ///           └─ Trolley                (TrolleyMover, X 슬라이딩)
    ///           └─ SpreaderRoot           (트롤리 X를 따라감)
    ///               └─ Spreader           (SpreaderHoist, Y 승강)
    ///                   └─ AttachPoint     (SpreaderAttach, 컨테이너 부착)
    ///
    /// 형상은 박스/스트럿 primitive 조합으로 실제 STS 크레인 실루엣(포털 다리 4개,
    /// 격자 붐, A-프레임 정상, 포어/백 스테이, 기계실, 트롤리·스프레더)을 흉내낸다.
    /// 치수는 1/24 미니어처(컨테이너와 비례)에 맞춘 기본값.
    ///
    /// ※ 트롤리/스프레더는 무버 컴포넌트로 분리돼 "움직일 수 있게" 설계만 돼 있고,
    ///   이들을 구동하는 입력/애니메이션 드라이버는 아직 없다(정지 상태로 생성됨).
    /// </summary>
    public static class StsCraneCreator
    {
        const float Scale = 1f / 24f;

        // 트롤리 가동 (붐 로컬 X) — 음수=육지쪽 backreach, 양수=바다쪽 outreach
        const float TrolleyMinX  = -4f  * Scale;   // ≈ -0.167m
        const float TrolleyMaxX  =  38f * Scale;   // ≈ 1.58m — 붐 거의 끝까지 트롤리 주행
        const float TrolleyRestX =  8f  * Scale;   // ≈  0.333m

        // 높이/치수
        const float RailH    = 18f  * Scale;       // 붐(트롤리 레일) 높이 = 다리 높이 ≈ 0.75m
        const float ApexH    = 11f  * Scale;       // A-프레임 정상이 붐 위로 솟은 높이 — 더 높게
        const float GaugeZ   = 12f  * Scale;       // 레일 게이지(좌우 다리 간격, Z) — 6→12(0.5), 컨테이너+트럭 통과 클리어런스
        const float LegSpanX = 9f   * Scale;       // 육지/바다 다리 간격(X) — 6→9(0.375), 옆 프로파일 1:2(Brace·실빔 함께)
        const float LegSec   = 0.6f * Scale;       // 다리 단면 한 변
        const float GirderZ  = 4f   * Scale;       // 붐 거더 폭(Z) — 1.2→4, 넓은 게이지와 균형(트롤리·캣워크 함께 넓어짐)

        // 붐 거더 X 끝점 — 거더/레일/격자/스테이가 공유(한 군데서 길이 관리)
        const float BoomBackX = TrolleyMinX - 0.27f;   // 백리치(육지쪽) 끝 — 0.20→0.27 추가 연장
        const float BoomTipX  = TrolleyMaxX + 0.1f;    // 아웃리치 끝 — 트롤리 끝 + 팁 구조 여유(트롤리가 거의 끝까지)

        // 다리 X 위치(붐 로컬 = 루트 로컬, 붐이 루트 x=0에 있으므로 동일)
        const float LandLegX  = 0f;
        const float WaterLegX = LegSpanX;

        // 스프레더 승강 (spreaderRoot=붐 레벨 기준 로컬 Y, 음수=아래)
        const float SpreaderMaxY  = -3f  * Scale;           // 완전 상승 = 헤드블록이 트롤리 바로 아래 도킹(붐에 안 박히게)
        const float SpreaderMinY  = -(RailH - 0.8f * Scale); // 지면 직전(붐 높이에 연동)
        const float SpreaderRestY = -10f * Scale;

        // 색
        static readonly Color CStruct  = new Color(0.82f, 0.83f, 0.85f); // 다리/포털/거더
        static readonly Color CBoom    = new Color(0.70f, 0.74f, 0.80f); // 붐 거더 본체
        static readonly Color CRail     = new Color(0.55f, 0.57f, 0.62f); // 트롤리 레일
        static readonly Color CMachine = new Color(0.30f, 0.33f, 0.38f); // 기계실
        static readonly Color CTrolley = new Color(0.95f, 0.45f, 0.10f); // 트롤리(안전 주황)
        static readonly Color CSpread  = new Color(0.98f, 0.80f, 0.10f); // 스프레더(안전 노랑)
        static readonly Color CDark    = new Color(0.13f, 0.13f, 0.15f); // 트위스트락/헤드블록
        static readonly Color CCable   = new Color(0.10f, 0.10f, 0.11f); // 케이블/로프
        static readonly Color CGlass   = new Color(0.25f, 0.55f, 0.70f); // 운전실 창
        static readonly Color CLight   = new Color(1.00f, 0.95f, 0.70f); // 작업등 렌즈
        static readonly Color CWarn    = new Color(0.90f, 0.10f, 0.10f); // 항공장애등(적색)

        const string RootName = "STS_Crane";

        // 같은 색은 머티리얼 1개를 재사용(빌드 1회 한정)
        static Dictionary<Color, Material> _matCache;
        // 모든 머티리얼이 공유하는 절차 생성 강철 디테일 텍스처(_BaseColor로 틴트)
        static Texture2D _steelTex;
        // 모든 Box/Strut가 공유하는 모서리 베벨(챔퍼) 큐브 메시
        static Mesh _beveledCube;

        [MenuItem("Container/Create STS Crane")]
        public static void CreateFromMenu()
        {
            // 중복 방지 — 기존 인스턴스 제거(Undo 가능)
            var prev = GameObject.Find(RootName);
            if (prev != null) Undo.DestroyObjectImmediate(prev);

            // 컨테이너가 스프레더 정지 위치 아래에 오도록 배치
            Vector3 anchor = FindContainerAnchor();
            Vector3 pos = anchor - new Vector3(TrolleyRestX, 0f, 0f);

            var root = Create(pos);
            Selection.activeGameObject = root;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.FrameSelected();
        }

        /// <summary>
        /// hierarchy를 생성해서 root GameObject를 반환. 다른 에디터/런타임 코드에서도 호출 가능.
        /// Undo 시스템에 등록 → Ctrl+Z 한 번으로 되돌릴 수 있음.
        /// </summary>
        public static GameObject Create(Vector3 worldPosition)
        {
            _matCache = new Dictionary<Color, Material>();
            _steelTex = null;

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create STS Crane");
            root.transform.position = worldPosition;

            // 정적 구조
            BuildGroundRails(root.transform);
            BuildPortal(root.transform);
            BuildAccessDetails(root.transform);   // 사다리 + 점검 플랫폼

            // 붐 (루트 x=0, y=RailH) — 자식 트롤리/스프레더가 붐 로컬 좌표로 동작
            var boom = new GameObject("Boom");
            boom.transform.SetParent(root.transform, worldPositionStays: false);
            boom.transform.localPosition = new Vector3(0f, RailH, 0f);
            BuildBoomStructure(boom.transform);
            BuildBoomDetails(boom.transform);     // 보도·시브·작업등

            // A-프레임 + 스테이 케이블 (루트 레벨)
            BuildApexAndStays(root.transform);

            // 트롤리 (붐 직속 자식)
            var trolley = new GameObject("Trolley");
            trolley.transform.SetParent(boom.transform, worldPositionStays: false);
            BuildTrolleyVisual(trolley.transform);

            // 스프레더 루트 (트롤리의 형제 — TrolleyMover가 X 동기)
            var spreaderRoot = new GameObject("SpreaderRoot");
            spreaderRoot.transform.SetParent(boom.transform, worldPositionStays: false);

            var spreader = new GameObject("Spreader");
            spreader.transform.SetParent(spreaderRoot.transform, worldPositionStays: false);
            BuildSpreaderVisual(spreader.transform);

            var attachPoint = new GameObject("AttachPoint");
            attachPoint.transform.SetParent(spreader.transform, worldPositionStays: false);
            attachPoint.transform.localPosition = new Vector3(0f, -0.01f, 0f);

            // 호이스트 로프 — HoistRopeRig가 매 프레임 스프레더 Y에 맞춰 신축
            BuildHoistRopes(spreaderRoot.transform, spreader.transform);

            // 컴포넌트 부착 + 설정
            var trolleyMover = trolley.AddComponent<TrolleyMover>();
            var spreaderHoist = spreader.AddComponent<SpreaderHoist>();
            var spreaderAttach = attachPoint.AddComponent<SpreaderAttach>();

            trolleyMover.Configure(TrolleyMinX, TrolleyMaxX, spreaderRoot.transform);
            spreaderHoist.Configure(SpreaderMinY, SpreaderMaxY);
            spreaderAttach.Configure(attachPoint.transform);

            // 초기 위치 (rest pose) — Boom 로컬 좌표계 기준
            trolley.transform.localPosition = new Vector3(TrolleyRestX, 0f, 0f);
            spreaderRoot.transform.localPosition = new Vector3(TrolleyRestX, 0f, 0f);
            spreader.transform.localPosition = new Vector3(0f, SpreaderRestY, 0f);

            // 루트 컴포넌트 — Facade로 묶기
            var stsCrane = root.AddComponent<StsCrane>();
            stsCrane.Configure(boom.transform, trolleyMover, spreaderHoist, spreaderAttach);

            // 자동 구동 드라이버 — Play 시 트롤리 왕복 + 스프레더 승강 사이클 반복
            root.AddComponent<StsCraneOperator>();

            _matCache = null;
            _steelTex = null;   // 텍스처는 머티리얼이 참조 유지 → 캐시 핸들만 해제
            return root;
        }

        // ───────────────────────── 정적 구조 ─────────────────────────

        // 부두 위 주행 레일 — 안벽(quay)을 따라 Z축으로 깔린다(붐이 뻗는 X와 수직).
        // 육지측·바다측 다리행 아래에 1줄씩(두 레일 간격 = 레일 게이지 = LegSpanX).
        static void BuildGroundRails(Transform root)
        {
            float railLen = GaugeZ + 0.30f;
            foreach (float x in new[] { LandLegX, WaterLegX })
            {
                Box(root, "Rail_" + (x == LandLegX ? "Land" : "Water"),
                    new Vector3(x, 0.004f, 0f),
                    new Vector3(0.02f, 0.008f, railLen), CRail);
            }
        }

        // 포털 게이트: 다리 4개 + 실 빔 + 보기 + 상부 크로스 빔 + 대각 브레이스
        static void BuildPortal(Transform root)
        {
            float halfZ = GaugeZ * 0.5f;
            float[] legX = { LandLegX, WaterLegX };

            foreach (float x in legX)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * halfZ;
                    // 다리 — 격자 트러스(코너 포스트 + 가로 rung + 대각 lacing)
                    BuildLatticeLeg(root, x, z, 0f, RailH, LegSec * 1.7f);
                    // 보기(주행 대차) — 이퀄라이저 빔 + 바퀴 4개. Z축으로 주행하므로 보기는 Z로 길다.
                    Box(root, "Bogie", new Vector3(x, 0.05f, z),
                        new Vector3(LegSec * 1.0f, 0.026f, LegSec * 2.6f), CDark);
                    Box(root, "Bogie_Beam", new Vector3(x, 0.032f, z),
                        new Vector3(LegSec * 0.45f, 0.016f, LegSec * 2.2f), CStruct);
                    for (int w = 0; w < 4; w++)
                    {
                        // 바퀴 — 원통(축은 주행레일 직각 = X), Z방향으로 굴러감
                        float wz = z + (w - 1.5f) * LegSec * 0.55f;
                        Rod(root, "Wheel",
                            new Vector3(x - LegSec * 0.65f, 0.014f, wz),
                            new Vector3(x + LegSec * 0.65f, 0.014f, wz),
                            0.013f, new Color(0.05f, 0.05f, 0.06f));
                    }
                }
            }

            // 좌우 다리를 잇는 상부 크로스 빔(포털 상단) — X 위치마다 1개
            foreach (float x in legX)
            {
                Box(root, "Portal_Cross", new Vector3(x, RailH - LegSec * 0.5f, 0f),
                    new Vector3(LegSec * 0.8f, LegSec * 0.8f, GaugeZ + LegSec), CStruct);
            }

            // 측면 대각 브레이스(앞/뒤 다리 사이) — X 패턴
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * halfZ;
                Strut(root, "Brace",
                    new Vector3(LandLegX, RailH * 0.12f, z),
                    new Vector3(WaterLegX, RailH * 0.92f, z), 0.010f, CStruct);
                Strut(root, "Brace",
                    new Vector3(WaterLegX, RailH * 0.12f, z),
                    new Vector3(LandLegX, RailH * 0.92f, z), 0.010f, CStruct);
            }

            // 실 빔 — 같은 쪽 두 다리(육지·바다)를 잇는 하부 종방향 빔
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root, "Sill_Beam",
                    new Vector3((LandLegX + WaterLegX) * 0.5f, 0.075f, s * halfZ),
                    new Vector3(LegSpanX + LegSec, 0.02f, LegSec * 0.9f), CStruct);
            }

            // 포털 횡방향(Z) X-브레이스 — 좌우(port/star) 다리 사이. 사용자 요청으로 보류(주석). 주석만 풀면 복구.
            /*
            foreach (float x in legX)
            {
                Strut(root, "Portal_Brace",
                    new Vector3(x, RailH * 0.12f, -halfZ),
                    new Vector3(x, RailH * 0.55f,  halfZ), 0.008f, CStruct);
                Strut(root, "Portal_Brace",
                    new Vector3(x, RailH * 0.12f,  halfZ),
                    new Vector3(x, RailH * 0.55f, -halfZ), 0.008f, CStruct);
            }
            */

            // ── 전체 디테일 보강 (베이스/다리) ──
            foreach (float x in legX)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float lz = s * halfZ;
                    // 다리 베이스 받침 플레이트
                    Box(root, "Leg_BasePlate", new Vector3(x, 0.066f, lz),
                        new Vector3(LegSec * 2.4f, 0.012f, LegSec * 2.0f), CStruct);
                    // 다리 상단 작업등 — 사용자 요청으로 보류(주석). 주석만 풀면 복구.
                    // Box(root, "Leg_Floodlight", new Vector3(x, RailH - 0.04f, lz + s * LegSec * 1.3f),
                    //     new Vector3(0.016f, 0.012f, 0.012f), CDark);
                }
            }
            // 도관/케이블 번들 — 육지쪽 다리 따라 지면→붐, 클램프 고정 + 상·하 정션 박스
            float condX = LandLegX - 0.024f;
            for (int c = -1; c <= 1; c++)   // 3줄 번들
            {
                Rod(root, "Leg_Conduit",
                    new Vector3(condX, 0.08f, halfZ + c * 0.006f),
                    new Vector3(condX, RailH - 0.04f, halfZ + c * 0.006f), 0.0035f, CDark);
            }
            // 클램프(고정 브래킷) 다단 — 다리에 붙임
            for (int i = 0; i <= 6; i++)
            {
                float cy = Mathf.Lerp(0.1f, RailH - 0.06f, i / 6f);
                Box(root, "Conduit_Clamp", new Vector3(condX + 0.008f, cy, halfZ),
                    new Vector3(0.018f, 0.005f, 0.026f), CStruct);
            }
            // 정션 박스(상·하)
            Box(root, "Junction_Box", new Vector3(condX, 0.11f, halfZ),
                new Vector3(0.02f, 0.032f, 0.026f), CMachine);
            Box(root, "Junction_Box", new Vector3(condX, RailH - 0.06f, halfZ),
                new Vector3(0.02f, 0.032f, 0.026f), CMachine);

            // ── 베이스/부두 인터페이스 디테일 ──
            foreach (float x in legX)
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float lz = s * halfZ;
                    // 폭풍 계류 스토우 핀(부두에 박는 핀) + 하우징
                    Box(root, "Stow_Pin_Housing", new Vector3(x, 0.045f, lz + s * LegSec * 1.5f),
                        new Vector3(0.022f, 0.05f, 0.022f), CMachine);
                    Rod(root, "Stow_Pin", new Vector3(x, 0f, lz + s * LegSec * 1.5f),
                        new Vector3(x, 0.05f, lz + s * LegSec * 1.5f), 0.006f, CDark);
                    // 타이다운 러그(부두 고정 고리)
                    Box(root, "Tiedown_Lug", new Vector3(x + 0.03f, 0.012f, lz),
                        new Vector3(0.012f, 0.018f, 0.01f), CStruct);
                    // 휠 레일 스위퍼(주행방향 Z 앞뒤 청소판 — 판은 레일을 X로 가로지름)
                    for (int e = -1; e <= 1; e += 2)
                        Box(root, "Rail_Sweeper", new Vector3(x, 0.006f, lz + e * LegSec * 1.4f),
                            new Vector3(LegSec * 1.1f, 0.01f, 0.006f), CDark);
                }
            }
            // 레일 끝 완충 버퍼(각 주행레일 Z 양 끝, 적색) — 사용자 요청으로 보류(주석). 주석만 풀면 복구.
            /*
            float railLenB = GaugeZ + 0.30f;
            foreach (float x in legX)
            for (int e = -1; e <= 1; e += 2)
            {
                Box(root, "Rail_Buffer",
                    new Vector3(x, 0.02f, e * railLenB * 0.5f),
                    new Vector3(0.03f, 0.03f, 0.018f), CWarn);
            }
            */
            // 충돌방지 센서 — 주행방향(Z) 양끝을 향함(옆 크레인 감지)
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root, "AntiCollision_Sensor",
                    new Vector3(WaterLegX, RailH * 0.5f, s * (halfZ + 0.02f)),
                    new Vector3(0.014f, 0.02f, 0.014f), CDark);
            }
        }

        // 붐: 메인 거더 + 주행 레일 + 격자 대각 + 기계실 (붐 로컬 좌표)
        static void BuildBoomStructure(Transform boom)
        {
            float x0 = BoomBackX;   // 백리치 끝
            float x1 = BoomTipX;    // 아웃리치 끝(바다쪽)
            float len = x1 - x0;
            float mid = (x0 + x1) * 0.5f;

            // 메인 거더
            Box(boom, "Boom_Girder", new Vector3(mid, 0.04f, 0f),
                new Vector3(len, 0.05f, GirderZ), CBoom);
            // 트롤리 주행 레일(거더 하단)
            Box(boom, "Boom_Rail", new Vector3(mid, 0.006f, 0f),
                new Vector3(len, 0.012f, GirderZ * 0.7f), CRail);

            // 거더 끝단/모서리 마감 — 사용자 요청으로 보류(주석). 주석만 풀면 복구.
            /*
            foreach (float ex in new[] { x0, x1 })
            {
                // 뚫린 듯한 끝을 덮는 플랜지 캡(단면보다 살짝 큼)
                Box(boom, "Boom_EndCap", new Vector3(ex, 0.04f, 0f),
                    new Vector3(0.01f, 0.064f, GirderZ + 0.016f), CStruct);
                // 캡 상·하 보강 립
                for (int sy = -1; sy <= 1; sy += 2)
                    Box(boom, "Boom_EndRib", new Vector3(ex, 0.04f + sy * 0.026f, 0f),
                        new Vector3(0.014f, 0.008f, GirderZ + 0.02f), CStruct);
            }
            // 상단 양 모서리 엣지 트림(날카로운 박스 모서리 마감)
            for (int s = -1; s <= 1; s += 2)
                Box(boom, "Girder_EdgeTrim", new Vector3(mid, 0.065f, s * GirderZ * 0.5f),
                    new Vector3(len, 0.006f, 0.006f), CStruct);
            */

            // 격자 대각 — 양옆(±Z) 대칭 지그재그
            int seg = 10;
            float step = len / seg;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * GirderZ * 0.48f;
                for (int i = 0; i < seg; i++)
                {
                    float xa = x0 + step * i;
                    float xb = x0 + step * (i + 1);
                    bool up = (i % 2) == 0;
                    Strut(boom, "Boom_Lattice",
                        new Vector3(xa, up ? 0.005f : 0.075f, z),
                        new Vector3(xb, up ? 0.075f : 0.005f, z),
                        0.005f, CStruct);
                }
                // 하부 종방향 코드(바닥 격자 느낌)
                Box(boom, "Boom_Chord", new Vector3(mid, 0.012f, z),
                    new Vector3(len, 0.008f, 0.008f), CStruct);
            }

            // 횡방향 프레임 — 상하 코드를 잇는 수직재 + 바닥 횡재(박스 트러스 느낌)
            int frames = 9;
            for (int i = 0; i <= frames; i++)
            {
                float fx = Mathf.Lerp(x0, x1, i / (float)frames);
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(boom, "Boom_Vertical", new Vector3(fx, 0.035f, s * GirderZ * 0.48f),
                        new Vector3(0.006f, 0.055f, 0.006f), CStruct);
                }
                Box(boom, "Boom_Cross", new Vector3(fx, 0.009f, 0f),
                    new Vector3(0.006f, 0.006f, GirderZ), CStruct);
            }

            // 기계실(육지쪽 위) + 디테일 — 붐 가로(Z)로 넓혀 육중하게(거더보다 양옆 돌출)
            float mhx = x0 + 0.11f;
            float mhZ = GirderZ * 1.5f;   // Z 폭 — 붐 기준(붐보다 약간 넓게 오버행), 게이지 변화에 안 휩쓸리게
            float mhHZ = mhZ * 0.5f;       // Z 반폭
            float mhHX = 0.085f;           // X 반폭(0.17의 절반)
            Box(boom, "Machinery_House", new Vector3(mhx, 0.105f, 0f),
                new Vector3(0.17f, 0.11f, mhZ), CMachine);
            Box(boom, "MH_Roof", new Vector3(mhx, 0.165f, 0f),
                new Vector3(0.185f, 0.012f, mhZ + 0.01f), CStruct);
            // 출입문(바다쪽 +X면) — 프레임 + 문짝 + 손잡이 + 하부 랜딩 그레이팅
            Box(boom, "MH_DoorFrame", new Vector3(mhx + 0.085f, 0.086f, 0f),
                new Vector3(0.004f, 0.066f, 0.05f), CStruct);
            Box(boom, "MH_Door", new Vector3(mhx + 0.087f, 0.085f, 0f),
                new Vector3(0.005f, 0.058f, 0.042f), CDark);
            Box(boom, "MH_DoorHandle", new Vector3(mhx + 0.0905f, 0.085f, 0.014f),
                new Vector3(0.004f, 0.012f, 0.004f), CStruct);
            Box(boom, "MH_DoorLanding", new Vector3(mhx + 0.1f, 0.056f, 0f),
                new Vector3(0.03f, 0.004f, 0.05f), CMachine);
            // 루버 환기 패널(양 ±Z면 육지쪽) — 프레임 + 가로 슬랫 다단, 상·하 2뱅크
            for (int s = -1; s <= 1; s += 2)
            {
                float fz = s * mhHZ;
                foreach (float vy in new[] { 0.118f, 0.078f })
                {
                    Box(boom, "MH_VentFrame", new Vector3(mhx - 0.05f, vy, fz - s * 0.001f),
                        new Vector3(0.042f, 0.034f, 0.005f), CDark);
                    for (int l = -2; l <= 2; l++)
                        Box(boom, "MH_Louver", new Vector3(mhx - 0.05f, vy + l * 0.007f, fz),
                            new Vector3(0.038f, 0.004f, 0.004f), CStruct);
                }
            }
            Box(boom, "MH_AC_Unit", new Vector3(mhx - 0.055f, 0.18f, 0f),
                new Vector3(0.05f, 0.028f, 0.05f), CDark);
            // 기계실 추가 디테일 — 배기구·창·보조 E-house·케이블 트레이·붐호이스트 윈치
            Rod(boom, "MH_Exhaust", new Vector3(mhx + 0.035f, 0.175f, 0.02f),
                new Vector3(mhx + 0.035f, 0.215f, 0.02f), 0.006f, CDark);
            // 창 — 큰 단일 유리 → 리세스 프레임 + 분할 유리 3칸 + 세로 멀리언(양 ±Z면)
            for (int s = -1; s <= 1; s += 2)
            {
                float fz = s * mhHZ;
                Box(boom, "MH_WinFrame", new Vector3(mhx + 0.01f, 0.118f, fz - s * 0.002f),
                    new Vector3(0.094f, 0.032f, 0.005f), CDark);
                for (int g = -1; g <= 1; g++)
                    Box(boom, "MH_Window", new Vector3(mhx + 0.01f + g * 0.03f, 0.118f, fz),
                        new Vector3(0.024f, 0.024f, 0.004f), CGlass);
                for (int m = -1; m <= 1; m += 2)
                    Box(boom, "MH_Mullion", new Vector3(mhx + 0.01f + m * 0.015f, 0.118f, fz),
                        new Vector3(0.004f, 0.03f, 0.004f), CStruct);
            }
            // 보조 E-house — 사용자 요청으로 보류(주석). 주석만 풀면 복구.
            /*
            Box(boom, "E_House", new Vector3(mhx + 0.12f, 0.085f, 0f),
                new Vector3(0.07f, 0.06f, GirderZ * 1.1f), CMachine);
            */
            Box(boom, "Cable_Tray", new Vector3(mhx + 0.18f, 0.05f, GirderZ * 0.5f),
                new Vector3(0.35f, 0.006f, 0.008f), CDark);
            Rod(boom, "Boom_Hoist_Drum",
                new Vector3(mhx - 0.06f, 0.04f, -0.03f),
                new Vector3(mhx - 0.06f, 0.04f,  0.03f), 0.024f, CDark);
            // 기계실 지붕 난간 — 4변 둘레 레일 + 토보드(킥플레이트) + 둘레 기둥
            float mhRoofY = 0.171f;
            float rlZ = mhHZ + 0.003f, rlX = mhHX + 0.003f, rlH = 0.026f;
            for (int s = -1; s <= 1; s += 2)
            {
                // ±Z 긴 변
                Box(boom, "MH_Roof_Rail", new Vector3(mhx, mhRoofY + rlH, s * rlZ),
                    new Vector3(rlX * 2f, 0.004f, 0.004f), CStruct);
                Box(boom, "MH_Roof_Toe", new Vector3(mhx, mhRoofY + 0.006f, s * rlZ),
                    new Vector3(rlX * 2f, 0.009f, 0.003f), CStruct);
                // ±X 끝 변
                Box(boom, "MH_Roof_Rail", new Vector3(mhx + s * rlX, mhRoofY + rlH, 0f),
                    new Vector3(0.004f, 0.004f, rlZ * 2f), CStruct);
                Box(boom, "MH_Roof_Toe", new Vector3(mhx + s * rlX, mhRoofY + 0.006f, 0f),
                    new Vector3(0.003f, 0.009f, rlZ * 2f), CStruct);
            }
            // 둘레 기둥(코너 4 + 각 변 중간 4) — 3×3 격자에서 내부 1칸만 제외
            for (int ix = -1; ix <= 1; ix++)
            for (int iz = -1; iz <= 1; iz++)
            {
                if (ix == 0 && iz == 0) continue;
                Box(boom, "MH_Roof_Post", new Vector3(mhx + ix * rlX, mhRoofY + rlH * 0.5f, iz * rlZ),
                    new Vector3(0.004f, rlH, 0.004f), CStruct);
            }

            // ── 기계실 표면/장비 디테일(이 박스만 고도화) ──
            float mhFZ = mhHZ;
            // 수직 코너 엣지 포스트 4(상자 모서리 트림)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Box(boom, "MH_Corner", new Vector3(mhx + sx * mhHX, 0.105f, sz * mhFZ),
                    new Vector3(0.006f, 0.112f, 0.006f), CStruct);
            // 패널 이음매(가로 2단 + 세로 2줄, 창 비껴서) — 큰 ±Z면 평탄함 제거
            for (int s = -1; s <= 1; s += 2)
            {
                float fz = s * (mhFZ + 0.001f);
                foreach (float ry in new[] { 0.062f, 0.148f })
                    Box(boom, "MH_PanelSeam", new Vector3(mhx, ry, fz),
                        new Vector3(0.166f, 0.0035f, 0.003f), CStruct);
                for (int c = -1; c <= 1; c += 2)
                    Box(boom, "MH_PanelSeam", new Vector3(mhx + c * 0.055f, 0.105f, fz),
                        new Vector3(0.003f, 0.106f, 0.003f), CStruct);
            }
            // 벽면 정션박스 2 + 수직 도관(육지쪽 +Z면)
            Box(boom, "MH_JBox", new Vector3(mhx - 0.07f, 0.09f, mhFZ),
                new Vector3(0.016f, 0.022f, 0.008f), CDark);
            Box(boom, "MH_JBox", new Vector3(mhx - 0.07f, 0.06f, mhFZ),
                new Vector3(0.012f, 0.016f, 0.007f), CStruct);
            Rod(boom, "MH_Conduit", new Vector3(mhx - 0.07f, 0.055f, mhFZ),
                new Vector3(mhx - 0.07f, 0.155f, mhFZ), 0.003f, CDark);
            // 벽면 작업등 2(아래 향함, 양 ±Z면 바다쪽 상단)
            for (int s = -1; s <= 1; s += 2)
            {
                Box(boom, "MH_WallLight_Housing", new Vector3(mhx + 0.06f, 0.152f, s * mhFZ),
                    new Vector3(0.012f, 0.01f, 0.012f), CDark);
                Ball(boom, "MH_WallLight", new Vector3(mhx + 0.06f, 0.146f, s * mhFZ),
                    new Vector3(0.009f, 0.006f, 0.009f), CLight);
            }
            // 지붕 디테일 — 점검 해치 + 보조 HVAC + 배기 캡
            Box(boom, "MH_RoofHatch", new Vector3(mhx + 0.06f, mhRoofY + 0.006f, -0.012f),
                new Vector3(0.03f, 0.008f, 0.03f), CDark);
            Box(boom, "MH_HVAC2", new Vector3(mhx + 0.03f, mhRoofY + 0.014f, 0.025f),
                new Vector3(0.04f, 0.022f, 0.03f), CDark);
            Ball(boom, "MH_ExhaustCap", new Vector3(mhx + 0.035f, 0.218f, 0.02f),
                new Vector3(0.012f, 0.008f, 0.012f), CDark);
            // 지붕 접근 수직 사다리(바다쪽 +Z면 → 지붕)
            BuildLadder(boom, mhx + 0.07f, mhFZ + 0.008f, 0.06f, mhRoofY, 0.02f);

            // ════════ 기계실 전체 마감/디테일 (넓힌 게이지 폭에 맞춤) ════════
            float mhTopY = 0.16f, mhBotY = 0.05f;

            // 마감: 상부 처마(eave) + 하부 실(sill) 둘레 띠 — 박스 4변 테두리 정리
            for (int s = -1; s <= 1; s += 2)
            {
                Box(boom, "MH_Eave", new Vector3(mhx, mhTopY + 0.003f, s * (mhHZ + 0.004f)),
                    new Vector3(0.176f, 0.006f, 0.006f), CStruct);
                Box(boom, "MH_Sill", new Vector3(mhx, mhBotY + 0.001f, s * (mhHZ + 0.003f)),
                    new Vector3(0.176f, 0.008f, 0.007f), CStruct);
                Box(boom, "MH_Eave", new Vector3(mhx + s * (mhHX + 0.004f), mhTopY + 0.003f, 0f),
                    new Vector3(0.006f, 0.006f, mhZ + 0.008f), CStruct);
                Box(boom, "MH_Sill", new Vector3(mhx + s * (mhHX + 0.003f), mhBotY + 0.001f, 0f),
                    new Vector3(0.007f, 0.008f, mhZ + 0.006f), CStruct);
            }

            // 육지쪽(-X) 끝면: 대형 루버 흡기 뱅크 3(프레임 + 가로 슬랫)
            float mhEndX = mhx - mhHX - 0.001f;
            for (int iz = -1; iz <= 1; iz++)
            {
                float lz = iz * 0.072f;
                Box(boom, "MH_EndLouverFrame", new Vector3(mhEndX, 0.103f, lz),
                    new Vector3(0.005f, 0.062f, 0.052f), CDark);
                for (int l = -3; l <= 3; l++)
                    Box(boom, "MH_EndLouver", new Vector3(mhEndX - 0.001f, 0.103f + l * 0.008f, lz),
                        new Vector3(0.004f, 0.004f, 0.048f), CStruct);
            }

            // 바다쪽(+X) 끝면: 문 양옆 작은 창 2(프레임 + 유리)
            float mhEndX2 = mhx + mhHX + 0.001f;
            for (int s = -1; s <= 1; s += 2)
            {
                Box(boom, "MH_EndWinFrame", new Vector3(mhEndX2, 0.114f, s * 0.078f),
                    new Vector3(0.005f, 0.03f, 0.03f), CDark);
                Box(boom, "MH_EndWindow", new Vector3(mhEndX2 + 0.001f, 0.114f, s * 0.078f),
                    new Vector3(0.004f, 0.024f, 0.024f), CGlass);
            }

            // 넓어진 지붕 양 끝(Z flank) 장비 — 콘덴서 2 + 팬그릴, 버섯 벤트 2, 케이블 트레이 횡단
            for (int s = -1; s <= 1; s += 2)
            {
                Box(boom, "MH_Condenser", new Vector3(mhx - 0.02f, mhRoofY + 0.016f, s * 0.085f),
                    new Vector3(0.06f, 0.03f, 0.05f), CDark);
                Box(boom, "MH_Condenser_Fan", new Vector3(mhx - 0.02f, mhRoofY + 0.032f, s * 0.085f),
                    new Vector3(0.05f, 0.004f, 0.04f), CStruct);
                Rod(boom, "MH_RoofVent", new Vector3(mhx + 0.055f, mhRoofY + 0.004f, s * 0.08f),
                    new Vector3(mhx + 0.055f, mhRoofY + 0.022f, s * 0.08f), 0.005f, CStruct);
                Ball(boom, "MH_RoofVent_Cap", new Vector3(mhx + 0.055f, mhRoofY + 0.024f, s * 0.08f),
                    new Vector3(0.014f, 0.009f, 0.014f), CStruct);
            }
            Box(boom, "MH_RoofTray", new Vector3(mhx + 0.02f, mhRoofY + 0.008f, 0f),
                new Vector3(0.012f, 0.005f, mhZ * 0.85f), CDark);

            // 지붕 네 모서리 적색 마커등(항공/안전)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Ball(boom, "MH_CornerLight", new Vector3(mhx + sx * mhHX, mhRoofY + 0.012f, sz * mhHZ),
                    new Vector3(0.007f, 0.007f, 0.007f), CWarn);

            // 넓은 ±Z면 보강 — 수직 도관 + 벽 작업등(육지쪽 분산)
            for (int s = -1; s <= 1; s += 2)
            {
                Rod(boom, "MH_WallConduit", new Vector3(mhx + 0.05f, 0.055f, s * (mhHZ + 0.0015f)),
                    new Vector3(mhx + 0.05f, 0.15f, s * (mhHZ + 0.0015f)), 0.003f, CDark);
                Box(boom, "MH_WallLight_Housing", new Vector3(mhx - 0.02f, 0.152f, s * (mhHZ + 0.001f)),
                    new Vector3(0.012f, 0.01f, 0.012f), CDark);
                Ball(boom, "MH_WallLight", new Vector3(mhx - 0.02f, 0.146f, s * (mhHZ + 0.001f)),
                    new Vector3(0.009f, 0.006f, 0.009f), CLight);
            }

            // 붐 상단 양옆 난간(walkway railing) — 거더 윗면 가장자리
            float girderTop = 0.065f;   // 거더 윗면
            float railTop   = 0.105f;   // 상단 가로 레일 높이
            int posts = 7;
            for (int s = -1; s <= 1; s += 2)
            {
                float z = s * GirderZ * 0.5f;
                Box(boom, "Boom_Railing", new Vector3(mid, railTop, z),
                    new Vector3(len, 0.006f, 0.006f), CStruct);
                // 중간 가로대
                Box(boom, "Boom_Railing_Mid", new Vector3(mid, (railTop + girderTop) * 0.5f, z),
                    new Vector3(len, 0.004f, 0.004f), CStruct);
                for (int i = 0; i <= posts; i++)
                {
                    float px = Mathf.Lerp(x0, x1, i / (float)posts);
                    Box(boom, "Railing_Post",
                        new Vector3(px, (railTop + girderTop) * 0.5f, z),
                        new Vector3(0.005f, railTop - girderTop, 0.005f), CStruct);
                }
            }
        }

        // A-프레임 정상 + 포어/백 스테이 케이블 (루트 로컬)
        static void BuildApexAndStays(Transform root)
        {
            float halfZ = GaugeZ * 0.5f;
            Vector3 apex = new Vector3(WaterLegX, RailH + ApexH, 0f);

            // 정상 가로보
            Box(root, "Apex", new Vector3(apex.x, apex.y, 0f),
                new Vector3(0.04f, 0.04f, GaugeZ * 0.7f), CStruct);

            // 정상 점검 플랫폼(데크 + 난간 기둥/상·중단 가로대 + 제어 캐비닛)
            float platY = apex.y + 0.03f;
            float halfX = 0.035f;
            float halfPZ = GaugeZ * 0.275f;
            float railH2 = 0.032f;

            Box(root, "Apex_Platform", new Vector3(apex.x, platY, 0f),
                new Vector3(halfX * 2f, 0.008f, halfPZ * 2f), CStruct);

            // 난간 기둥(둘레 8개)
            float[] gx = { -halfX, 0f, halfX };
            float[] gz = { -halfPZ, 0f, halfPZ };
            foreach (float fx in gx)
            foreach (float fz in gz)
            {
                if (Mathf.Abs(fx) >= halfX - 1e-4f || Mathf.Abs(fz) >= halfPZ - 1e-4f)
                    Box(root, "Apex_Post", new Vector3(apex.x + fx, platY + railH2 * 0.5f, fz),
                        new Vector3(0.004f, railH2, 0.004f), CStruct);
            }
            // 상단·중간 가로 레일(4변 × 2단)
            foreach (float h in new[] { railH2, railH2 * 0.55f })
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(root, "Apex_Rail", new Vector3(apex.x, platY + h, s * halfPZ),
                        new Vector3(halfX * 2f, 0.004f, 0.004f), CStruct);
                    Box(root, "Apex_Rail", new Vector3(apex.x + s * halfX, platY + h, 0f),
                        new Vector3(0.004f, 0.004f, halfPZ * 2f), CStruct);
                }
            }
            // 제어 캐비닛
            Box(root, "Apex_Cabinet", new Vector3(apex.x - halfX * 0.4f, platY + 0.02f, halfPZ * 0.45f),
                new Vector3(0.025f, 0.03f, 0.02f), CMachine);
            // 토보드(난간 하부 킥플레이트, 4변) — 공구 낙하 방지 + 디테일
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root, "Apex_ToeBoard", new Vector3(apex.x, platY + 0.007f, s * (halfPZ - 0.001f)),
                    new Vector3(halfX * 2f, 0.01f, 0.003f), CStruct);
                Box(root, "Apex_ToeBoard", new Vector3(apex.x + s * (halfX - 0.001f), platY + 0.007f, 0f),
                    new Vector3(0.003f, 0.01f, halfPZ * 2f), CStruct);
            }
            // 정션 박스 2(반대편 데크) — 장비 배치 균형
            Box(root, "Apex_JBox", new Vector3(apex.x + halfX * 0.45f, platY + 0.016f, -halfPZ * 0.5f),
                new Vector3(0.016f, 0.022f, 0.014f), CDark);
            Box(root, "Apex_JBox", new Vector3(apex.x - halfX * 0.5f, platY + 0.012f, -halfPZ * 0.2f),
                new Vector3(0.012f, 0.016f, 0.012f), CMachine);

            // 비콘 마스트 — 단단한 마스트 + 하우징 달린 적색 항공장애등 2단 + 풍속계 + 피뢰침
            float mb = platY + 0.012f;
            float mastTop = mb + 0.10f;
            // 받침 플랜지 2단(원형)
            Rod(root, "Mast_Base", new Vector3(apex.x, mb - 0.006f, 0f),
                new Vector3(apex.x, mb + 0.004f, 0f), 0.012f, CMachine);
            Rod(root, "Mast_Base", new Vector3(apex.x, mb + 0.004f, 0f),
                new Vector3(apex.x, mb + 0.012f, 0f), 0.0075f, CStruct);
            // 마스트 기둥(약간 굵게)
            Rod(root, "Apex_Mast", new Vector3(apex.x, mb + 0.012f, 0f),
                new Vector3(apex.x, mastTop, 0f), 0.0035f, CStruct);
            // 적색 항공장애등 2단 — 검은 하우징(짧은 원통) + 적색 돔(작게)
            foreach (float ly in new[] { mb + 0.045f, mb + 0.085f })
            {
                Rod(root, "AviLight_Housing", new Vector3(apex.x, ly - 0.005f, 0f),
                    new Vector3(apex.x, ly + 0.003f, 0f), 0.0085f, CDark);
                Ball(root, "Aviation_Light", new Vector3(apex.x, ly + 0.008f, 0f),
                    new Vector3(0.011f, 0.008f, 0.011f), CWarn);
            }
            // 피뢰침(마스트 꼭대기 → 뾰족)
            Cone(root, "Lightning_Rod", new Vector3(apex.x, mastTop, 0f),
                new Vector3(apex.x, mastTop + 0.028f, 0f), 0.002f, 0f, CStruct, 12);
            // 풍속계 — 측면 브래킷 + 수직 스핀들 + 수평 3컵(맞바람에 도는 컵)
            Vector3 anBrkt = new Vector3(apex.x, mb + 0.07f, 0.022f);
            Rod(root, "Anemo_Arm", new Vector3(apex.x, mb + 0.07f, 0.004f), anBrkt, 0.0015f, CStruct);
            Vector3 anHub = anBrkt + new Vector3(0f, 0.012f, 0f);
            Rod(root, "Anemo_Spindle", anBrkt, anHub, 0.0014f, CDark);
            Ball(root, "Anemo_Hub", anHub, new Vector3(0.006f, 0.006f, 0.006f), CDark);
            for (int k = 0; k < 3; k++)
            {
                float a3 = k * Mathf.PI * 2f / 3f;
                Vector3 cupP = anHub + new Vector3(Mathf.Cos(a3) * 0.011f, 0.001f, Mathf.Sin(a3) * 0.011f);
                Rod(root, "Anemo_CupArm", anHub, cupP, 0.0008f, CStruct);
                Ball(root, "Anemo_Cup", cupP, new Vector3(0.005f, 0.005f, 0.005f), CStruct);
            }

            // A-프레임 다리 4개 (정상 → 다리 상단)
            foreach (float lx in new[] { LandLegX, WaterLegX })
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Strut(root, "Aframe",
                        apex, new Vector3(lx, RailH, s * halfZ), 0.012f, CStruct);
                }
            }

            // A-프레임 면을 격자 트러스로 — 다단 가로 브레이스 + 대각 지그재그(앞·뒤 면)
            float[] lv = { 0.20f, 0.40f, 0.60f, 0.80f };
            foreach (float legX in new[] { LandLegX, WaterLegX })
            {
                foreach (float t in lv)
                {
                    float bx = Mathf.Lerp(WaterLegX, legX, t);
                    float by = Mathf.Lerp(apex.y, RailH, t);
                    Box(root, "Aframe_Brace", new Vector3(bx, by, 0f),
                        new Vector3(0.007f, 0.007f, GaugeZ * t), CStruct);
                }
                for (int i = 0; i < lv.Length - 1; i++)
                {
                    float t0 = lv[i], t1 = lv[i + 1];
                    float x0 = Mathf.Lerp(WaterLegX, legX, t0), x1 = Mathf.Lerp(WaterLegX, legX, t1);
                    float y0 = Mathf.Lerp(apex.y, RailH, t0), y1 = Mathf.Lerp(apex.y, RailH, t1);
                    Strut(root, "Aframe_Lace",
                        new Vector3(x0, y0, -halfZ * t0), new Vector3(x1, y1, halfZ * t1), 0.006f, CStruct);
                    Strut(root, "Aframe_Lace",
                        new Vector3(x0, y0, halfZ * t0), new Vector3(x1, y1, -halfZ * t1), 0.006f, CStruct);
                }
            }

            // A-프레임 측면(port/star) 가로 브레이스 — 바다·육지 다리쌍 연결
            foreach (float t in new[] { 0.38f, 0.66f })
            {
                float lx = Mathf.Lerp(WaterLegX, LandLegX, t);
                float yy = Mathf.Lerp(apex.y, RailH, t);
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(root, "Aframe_SideBrace",
                        new Vector3((WaterLegX + lx) * 0.5f, yy, s * halfZ * t),
                        new Vector3(Mathf.Abs(WaterLegX - lx), 0.006f, 0.006f), CStruct);
                }
            }

            // 정상 정비용 데빗(소형 지브) — 튀어나와 보여 일단 보류(주석). 나중에 끝단 후크 등 달아 복구.
            // Box(root, "Davit_Mast", new Vector3(apex.x - 0.03f, apex.y + 0.025f, halfZ * 0.4f),
            //     new Vector3(0.005f, 0.05f, 0.005f), CStruct);
            // Box(root, "Davit_Arm", new Vector3(apex.x - 0.05f, apex.y + 0.045f, halfZ * 0.4f),
            //     new Vector3(0.05f, 0.005f, 0.005f), CStruct);

            // 정상 시브 네스트(스테이 도르래) + 장비 하우징
            Box(root, "Apex_SheaveHouse", new Vector3(apex.x, apex.y - 0.025f, 0f),
                new Vector3(0.03f, 0.05f, GaugeZ * 0.5f), CMachine);
            for (int w = -1; w <= 1; w += 2)
            {
                Rod(root, "Apex_Sheave",
                    new Vector3(apex.x + w * 0.012f, apex.y - 0.01f, -0.016f),
                    new Vector3(apex.x + w * 0.012f, apex.y - 0.01f,  0.016f), 0.016f, CDark);
            }
            // 시브 하우스 디테일 — 측면 리브 + 점검 해치 + 리프팅 러그
            for (int s = -1; s <= 1; s += 2)
            {
                Box(root, "SheaveHouse_Rib", new Vector3(apex.x, apex.y - 0.025f, s * GaugeZ * 0.24f),
                    new Vector3(0.034f, 0.05f, 0.004f), CStruct);
            }
            Box(root, "SheaveHouse_Hatch", new Vector3(apex.x + 0.016f, apex.y - 0.015f, 0f),
                new Vector3(0.004f, 0.02f, 0.02f), CDark);
            Box(root, "Lifting_Lug", new Vector3(apex.x, apex.y + 0.008f, 0f),
                new Vector3(0.006f, 0.014f, 0.006f), CStruct);

            // 정상 작업조명 갤러리 — 플랫폼 바다쪽 가장자리에서 받침대로 뻗은 프레임 +
            // 하우징 달린 플러드라이트(아래·바다쪽 작업면을 비춤). 떠 있지 않게 받침으로 고정.
            float galX = apex.x + 0.042f;
            float galY = platY - 0.004f;
            Box(root, "FloodBar", new Vector3(galX, galY, 0f),
                new Vector3(0.006f, 0.008f, halfPZ * 1.7f), CStruct);
            for (int s = -1; s <= 1; s += 2)
                Strut(root, "FloodBar_Brace",
                    new Vector3(apex.x, platY + 0.004f, s * halfPZ * 0.7f),
                    new Vector3(galX, galY, s * halfPZ * 0.7f), 0.004f, CStruct);
            for (int i = -2; i <= 2; i++)
            {
                float fz = i * halfPZ * 0.4f;
                Box(root, "Floodlight_Housing", new Vector3(galX + 0.006f, galY, fz),
                    new Vector3(0.012f, 0.014f, 0.014f), CDark);
                Ball(root, "Apex_Floodlight", new Vector3(galX + 0.013f, galY - 0.006f, fz),
                    new Vector3(0.011f, 0.009f, 0.011f), CLight);
            }

            // 정상 접근 사다리 — 육지측 A-프레임 다리(+Z)를 따라 경사지게, 구조물 바깥으로 오프셋(박힘 방지)
            Vector3 aTop = Vector3.Lerp(new Vector3(LandLegX, RailH, halfZ), apex, 0.9f);
            Vector3 aBot = new Vector3(LandLegX, RailH, halfZ);
            BuildInclinedLadder(root, aTop, aBot, 0.028f, new Vector3(0f, 0f, 0.022f));

            // 스테이 케이블 — 부채꼴 + 앵커 플레이트·턴버클
            float boomTopY = RailH + 0.07f;
            foreach (float f in new[] { 0.35f, 0.55f, 0.78f, 1.0f })   // 바다쪽 4줄
            {
                BuildStay(root, apex,
                    new Vector3(Mathf.Lerp(WaterLegX, BoomTipX, f), boomTopY, 0f), "Forestay");
            }
            // 백스테이 — 기계실에 박히던 문제 수정: 앵커를 기계실 지붕 뒤쪽-상단으로 올려
            //   케이블이 기계실 위로 지나가 지붕 뒤에 고정되게 함(실제 크레인도 후방 상단에 물림).
            float bsX = BoomBackX + 0.05f;             // 기계실 지붕 뒤쪽(앵커 마스트 X)
            float mhRoofTopY = RailH + 0.171f;         // 기계실 지붕 높이(boom 로컬 0.171 + RailH)
            float bsTopY = RailH + 0.205f;             // 지붕·지붕장비 위로 올린 앵커 높이
            // 앵커 마스트(지붕 → 위) + 이퀄라이저 빔
            Box(root, "Backstay_AnchorMast", new Vector3(bsX, (mhRoofTopY + bsTopY) * 0.5f, 0f),
                new Vector3(0.012f, bsTopY - mhRoofTopY, 0.012f), CStruct);
            Box(root, "Backstay_Anchor", new Vector3(bsX, bsTopY, 0f),
                new Vector3(0.02f, 0.014f, GaugeZ * 0.42f), CMachine);
            // 좌우 대칭 2줄(port/star) — 정상에서 기계실 위를 넘어 지붕 뒤 앵커로
            for (int s = -1; s <= 1; s += 2)
            {
                BuildStay(root, apex,
                    new Vector3(bsX, bsTopY, s * GaugeZ * 0.18f), "Backstay");
            }
        }

        // ───────────────────────── 가동부 시각화 ─────────────────────────

        static void BuildTrolleyVisual(Transform trolley)
        {
            // 트롤리는 붐 레일(붐 로컬 y=0)에 위치. 본체는 그 아래로.
            Box(trolley, "Trolley_Body", new Vector3(0f, -0.025f, 0f),
                new Vector3(0.11f, 0.05f, GirderZ * 0.9f), CTrolley);
            Box(trolley, "Trolley_Head", new Vector3(0f, -0.06f, 0f),
                new Vector3(0.07f, 0.025f, GirderZ * 0.85f), CDark);
            // 호이스트 시브(도르래) — 로프가 도는 휠, 축은 Z. 넓어진 트롤리/로프(z±0.05)에 맞춤
            for (int w = -1; w <= 1; w += 2)
            {
                Rod(trolley, "Trolley_Sheave",
                    new Vector3(w * 0.03f, -0.005f, -0.05f),
                    new Vector3(w * 0.03f, -0.005f,  0.05f), 0.013f, CDark);
            }
            // 운전실(트롤리에 매달려 함께 이동) — 프레임 + 유리, 중앙 배치(좌우 대칭)
            Box(trolley, "Cab_Frame", new Vector3(0.052f, -0.06f, 0f),
                new Vector3(0.054f, 0.054f, 0.064f), CDark);
            Box(trolley, "Operator_Cab", new Vector3(0.06f, -0.06f, 0f),
                new Vector3(0.042f, 0.042f, 0.05f), CGlass);
            // 창틀(멀리언) — 유리 분할
            for (int i = -1; i <= 1; i += 2)
            {
                Box(trolley, "Cab_Mullion", new Vector3(0.06f, -0.06f, i * 0.018f),
                    new Vector3(0.044f, 0.044f, 0.003f), CDark);
            }
            Box(trolley, "Cab_Mullion_H", new Vector3(0.06f, -0.06f, 0f),
                new Vector3(0.044f, 0.003f, 0.05f), CDark);

            // ── 운전실/트롤리 디테일 보강 ──
            // 차양(선바이저)
            Box(trolley, "Cab_Visor", new Vector3(0.088f, -0.04f, 0f),
                new Vector3(0.035f, 0.004f, 0.06f), CDark);
            // 운전실 하부 작업등 클러스터(작업면 조명)
            for (int i = -1; i <= 1; i++)
            {
                Ball(trolley, "Cab_Floodlight", new Vector3(0.06f + i * 0.016f, -0.084f, 0.02f),
                    new Vector3(0.012f, 0.007f, 0.012f), CLight);
            }
            // 운전실 안테나
            Rod(trolley, "Cab_Antenna", new Vector3(0.045f, -0.033f, 0.02f),
                new Vector3(0.045f, 0.0f, 0.02f), 0.0015f, CDark);
            // 운전실 접근 플랫폼 + 난간(뒤쪽)
            Box(trolley, "Cab_Platform", new Vector3(0.025f, -0.086f, 0f),
                new Vector3(0.03f, 0.004f, 0.075f), CMachine);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(trolley, "Cab_Plat_Rail", new Vector3(0.025f, -0.07f, s * 0.036f),
                    new Vector3(0.03f, 0.003f, 0.003f), CStruct);
            }
            // 트롤리 구동 모터 2 + 페스툰 연결 박스(육지쪽 뒤)
            for (int s = -1; s <= 1; s += 2)
            {
                Box(trolley, "Trolley_Motor", new Vector3(-0.03f, -0.02f, s * 0.03f),
                    new Vector3(0.025f, 0.03f, 0.022f), CMachine);
            }
            Box(trolley, "Trolley_FestoonBox", new Vector3(-0.05f, -0.02f, 0f),
                new Vector3(0.018f, 0.025f, 0.03f), CDark);
            // 트롤리 양끝 완충 버퍼(적색)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                Box(trolley, "Trolley_Bumper", new Vector3(sx * 0.058f, -0.01f, 0f),
                    new Vector3(0.008f, 0.014f, 0.04f), CWarn);
            }
        }

        static void BuildSpreaderVisual(Transform spreader)
        {
            // 컨테이너 긴 축이 안벽/주행 방향(Z)을 향하도록 스프레더 전체를 90° 회전
            // (부속은 긴 축=로컬 X로 배치 → Y축 90° 회전으로 월드 Z가 긴 축이 됨)
            spreader.localRotation = Quaternion.Euler(0f, 90f, 0f);

            float hl = 0.126f;       // 반길이(로컬 X) — 20ft 컨테이너
            float hw = 0.05f;        // 반폭(로컬 Z) — 컨테이너 폭 비례
            Color CMetal = CRail;    // 트위스트락/플리퍼 회색

            // ── 메인 스프레더 빔(박스 거더) + 상·하 플랜지 ──
            Box(spreader, "Spreader_Bar", Vector3.zero,
                new Vector3(hl * 2f, 0.028f, hw * 2f), CSpread);
            for (int sy = -1; sy <= 1; sy += 2)
                Box(spreader, "Beam_Flange", new Vector3(0f, sy * 0.016f, 0f),
                    new Vector3(hl * 2f, 0.006f, hw * 2f + 0.008f), CSpread);
            // 끝단 크로스 빔(컨테이너 단부)
            for (int sx = -1; sx <= 1; sx += 2)
                Box(spreader, "End_Beam", new Vector3(sx * (hl - 0.008f), 0f, 0f),
                    new Vector3(0.016f, 0.03f, hw * 2f + 0.012f), CSpread);
            // 텔레스코핑 내부 빔(가변 흉내)
            for (int sx = -1; sx <= 1; sx += 2)
                Box(spreader, "Tele_Beam", new Vector3(sx * hl * 0.55f, 0f, 0f),
                    new Vector3(hl, 0.018f, hw * 1.2f), CDark);

            // ── 헤드블록 — 크로스 프레임 + 시브 4개(2쌍) + 치크 플레이트 ──
            float hbY = 0.058f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                Strut(spreader, "Head_Frame",
                    new Vector3(sx * 0.055f, 0.016f, sz * 0.028f),
                    new Vector3(sx * 0.035f, hbY - 0.012f, sz * 0.02f), 0.006f, CSpread);
            Box(spreader, "Spreader_Head", new Vector3(0f, hbY, 0f),
                new Vector3(0.11f, 0.026f, hw * 1.5f), CSpread);
            // 시브 4개(2쌍) — 축 Z, X로 4열
            foreach (float hx in new[] { -0.042f, -0.018f, 0.018f, 0.042f })
                Rod(spreader, "Head_Sheave",
                    new Vector3(hx, hbY + 0.013f, -0.026f),
                    new Vector3(hx, hbY + 0.013f,  0.026f), 0.013f, CDark);
            // 치크 플레이트(시브 양 측면 판)
            for (int sz = -1; sz <= 1; sz += 2)
                Box(spreader, "Head_Cheek", new Vector3(0f, hbY + 0.013f, sz * 0.03f),
                    new Vector3(0.1f, 0.028f, 0.004f), CSpread);

            // ── 트위스트락 4(코너) — 회색 락헤드 + 락 콘 + 플리퍼 판 ──
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 c = new Vector3(sx * (hl - 0.006f), 0f, sz * (hw - 0.006f));
                // 코너 캐스팅 락헤드(회색 박스)
                Box(spreader, "Twistlock_Head", c + new Vector3(0f, -0.014f, 0f),
                    new Vector3(0.02f, 0.024f, 0.02f), CMetal);
                // 락 콘(아래로 뾰족)
                Cone(spreader, "Twistlock_Cone",
                    c + new Vector3(0f, -0.036f, 0f), c + new Vector3(0f, -0.024f, 0f),
                    0.004f, 0.009f, CMetal, 16);
                // 플리퍼 가이드 판(코너 바깥·아래로)
                Strut(spreader, "Flipper",
                    c + new Vector3(sx * 0.004f, -0.006f, sz * 0.004f),
                    c + new Vector3(sx * 0.026f, -0.05f, sz * 0.026f), 0.016f, CMetal);
            }

            // ── 부속(파워팩/정션박스/작업등) ──
            Box(spreader, "Spreader_PowerPack", new Vector3(0.07f, 0.022f, 0f),
                new Vector3(0.04f, 0.022f, hw * 1.2f), CMachine);
            Box(spreader, "Spreader_JBox", new Vector3(-0.07f, 0.02f, 0f),
                new Vector3(0.03f, 0.018f, 0.03f), CDark);
            for (int sx = -1; sx <= 1; sx += 2)
                Ball(spreader, "Spreader_Floodlight", new Vector3(sx * 0.085f, 0.005f, 0f),
                    new Vector3(0.013f, 0.008f, 0.013f), CLight);
        }

        // 호이스트 로프 4줄 — spreaderRoot(붐 레벨 y=0) → 스프레더 헤드.
        // 정적 생성 후 HoistRopeRig가 매 프레임 스프레더 Y에 맞춰 신축(게임 런타임).
        static void BuildHoistRopes(Transform spreaderRoot, Transform spreader)
        {
            float topY = -0.02f;               // 로프 상단(트롤리 헤드 아래)
            float attachOffsetY = 0.05f;       // 스프레더 원점 → 헤드블록 상단(로프 하단)
            float radius = 0.0035f;
            float restBotY = SpreaderRestY + attachOffsetY;

            var ropes = new List<Transform>();
            var anchors = new List<Vector2>();
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                float x = sx * 0.03f;
                float z = sz * 0.05f;   // 넓어진 트롤리/스프레더(Z 긴 축)에 맞춤
                var rope = Rod(spreaderRoot, "Hoist_Rope",
                    new Vector3(x, topY, z), new Vector3(x, restBotY, z), radius, CCable);
                ropes.Add(rope.transform);
                anchors.Add(new Vector2(x, z));
            }
            // 매 프레임 트롤리↔스프레더 사이로 로프 신축
            var rig = spreaderRoot.gameObject.AddComponent<HoistRopeRig>();
            rig.Configure(spreader, topY, attachOffsetY, ropes.ToArray(), anchors.ToArray(), radius);
        }

        // ───────────────────────── 디테일 지오메트리 (D) ─────────────────────────

        // 격자 트러스 다리 — 4 코너 포스트 + 가로 rung + 4면 대각 lacing
        static void BuildLatticeLeg(Transform root, float cx, float cz, float y0, float y1, float foot)
        {
            float h = y1 - y0;
            float half = foot * 0.5f;
            float postT = foot * 0.28f;

            // 4 코너 포스트
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Box(root, "Leg_Post", new Vector3(cx + sx * half, (y0 + y1) * 0.5f, cz + sz * half),
                    new Vector3(postT, h, postT), CStruct);
            }

            int segs = Mathf.Max(4, Mathf.RoundToInt(h / 0.10f));
            float step = h / segs;

            // 가로 rung — 매 노드, 4면
            for (int i = 0; i <= segs; i++)
            {
                float y = y0 + step * i;
                for (int sz = -1; sz <= 1; sz += 2)
                    Box(root, "Leg_Rung", new Vector3(cx, y, cz + sz * half),
                        new Vector3(foot, postT * 0.7f, postT * 0.7f), CStruct);
                for (int sx = -1; sx <= 1; sx += 2)
                    Box(root, "Leg_Rung", new Vector3(cx + sx * half, y, cz),
                        new Vector3(postT * 0.7f, postT * 0.7f, foot), CStruct);
            }

            // 대각 lacing — 4면 지그재그
            for (int i = 0; i < segs; i++)
            {
                float ya = y0 + step * i;
                float yb = y0 + step * (i + 1);
                bool up = (i % 2) == 0;
                for (int sz = -1; sz <= 1; sz += 2)
                    Strut(root, "Leg_Lace",
                        new Vector3(cx - half, up ? ya : yb, cz + sz * half),
                        new Vector3(cx + half, up ? yb : ya, cz + sz * half), postT * 0.55f, CStruct);
                for (int sx = -1; sx <= 1; sx += 2)
                    Strut(root, "Leg_Lace",
                        new Vector3(cx + sx * half, up ? ya : yb, cz - half),
                        new Vector3(cx + sx * half, up ? yb : ya, cz + half), postT * 0.55f, CStruct);
            }
        }

        // 붐 디테일: 보도 그레이팅 + 끝단 시브(도르래) + 작업등
        static void BuildBoomDetails(Transform boom)
        {
            float x0 = BoomBackX, x1 = BoomTipX;
            float len = x1 - x0, mid = (x0 + x1) * 0.5f;

            // 보도 그레이팅(난간 사이 바닥)
            Box(boom, "Walkway_Deck", new Vector3(mid, 0.067f, 0f),
                new Vector3(len, 0.004f, GirderZ), CMachine);

            // 끝단 시브(도르래) — 로프가 도는 휠, 축은 Z
            Rod(boom, "Sheave_Tip",
                new Vector3(x1 - 0.02f, 0.03f, -0.04f),
                new Vector3(x1 - 0.02f, 0.03f,  0.04f), 0.02f, CDark);

            // 작업등(floodlight) — 붐 하부, 아래를 비춤
            foreach (float lx in new[] { mid + 0.10f, x1 - 0.12f, x1 - 0.30f })
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    float z = s * GirderZ * 0.45f;
                    Box(boom, "Floodlight_Housing", new Vector3(lx, -0.008f, z),
                        new Vector3(0.018f, 0.014f, 0.018f), CDark);
                    Ball(boom, "Floodlight_Lens", new Vector3(lx, -0.018f, z),
                        new Vector3(0.015f, 0.007f, 0.015f), CLight);
                }
            }

            // 끝단 플랫폼 + 추가 시브 + 항해등
            Box(boom, "Tip_Platform", new Vector3(x1 - 0.04f, 0.066f, 0f),
                new Vector3(0.08f, 0.004f, GirderZ * 1.0f), CMachine);
            Rod(boom, "Sheave_Tip2",
                new Vector3(x1 - 0.05f, 0.03f, -0.04f),
                new Vector3(x1 - 0.05f, 0.03f,  0.04f), 0.018f, CDark);
            Ball(boom, "Nav_Light", new Vector3(x1 - 0.003f, 0.05f, 0f),
                new Vector3(0.012f, 0.016f, 0.012f), CWarn);

            // 하부 점검 캣워크(양옆) + 난간(상단·중간대 + 기둥)
            for (int s = -1; s <= 1; s += 2)
            {
                float cz = s * (GirderZ * 0.5f + 0.014f);
                float railZ = cz + s * 0.009f;
                Box(boom, "Catwalk", new Vector3(mid, 0f, cz),
                    new Vector3(len, 0.004f, 0.022f), CMachine);
                Box(boom, "Catwalk_Rail", new Vector3(mid, 0.026f, railZ),
                    new Vector3(len, 0.004f, 0.004f), CStruct);
                Box(boom, "Catwalk_RailMid", new Vector3(mid, 0.013f, railZ),
                    new Vector3(len, 0.003f, 0.003f), CStruct);
                int cposts = 14;
                for (int i = 0; i <= cposts; i++)
                {
                    float px = Mathf.Lerp(x0, x1, i / (float)cposts);
                    Box(boom, "Catwalk_Post", new Vector3(px, 0.013f, railZ),
                        new Vector3(0.003f, 0.026f, 0.003f), CStruct);
                }
            }

            // 페스툰(전력·제어 케이블) — 붐 하부 트랙 + 늘어진 케이블 다발
            float festZ = GirderZ * 0.5f + 0.006f;
            Box(boom, "Festoon_Track", new Vector3(mid, -0.006f, festZ),
                new Vector3(len, 0.004f, 0.004f), CDark);
            int loops = 12;
            for (int i = 0; i < loops; i++)
            {
                float fx = Mathf.Lerp(x0 + 0.05f, x1 - 0.05f, i / (float)(loops - 1));
                Box(boom, "Festoon_Cable", new Vector3(fx, -0.017f, festZ),
                    new Vector3(0.006f, 0.022f, 0.006f), CDark);
            }

            // ── 뒷부분(육지측 백리치) 디테일 ──
            // 평형추(counterweight) — 적층 무게 블록(긴 아웃리치 균형용)
            for (int i = 0; i < 3; i++)
            {
                Box(boom, "Counterweight", new Vector3(x0 + 0.05f, -0.05f + i * 0.022f, 0f),
                    new Vector3(0.09f, 0.02f, GirderZ * 1.3f), CDark);
            }
            // 평형추 하부 받침 격자
            for (int s = -1; s <= 1; s += 2)
            {
                Strut(boom, "CW_Brace",
                    new Vector3(x0 + 0.01f, 0.0f, s * GirderZ * 0.5f),
                    new Vector3(x0 + 0.09f, -0.04f, s * GirderZ * 0.5f), 0.006f, CStruct);
            }
            // 백스테이 앵커 브래킷
            Box(boom, "Stay_Anchor", new Vector3(x0 + 0.02f, 0.075f, 0f),
                new Vector3(0.02f, 0.035f, GirderZ * 0.6f), CMachine);
            // 백리치 끝 플랫폼 + 경고등
            Box(boom, "Back_Platform", new Vector3(x0 + 0.02f, 0.066f, 0f),
                new Vector3(0.06f, 0.004f, GirderZ * 1.0f), CMachine);
            Ball(boom, "Back_Light", new Vector3(x0 + 0.004f, 0.05f, 0f),
                new Vector3(0.012f, 0.016f, 0.012f), CWarn);
            // 변압기 + 냉각 유닛(기계 데크)
            Box(boom, "Transformer", new Vector3(x0 + 0.15f, 0.085f, GirderZ * 0.72f),
                new Vector3(0.05f, 0.05f, 0.04f), CMachine);
            Box(boom, "Cooling_Unit", new Vector3(x0 + 0.15f, 0.082f, -GirderZ * 0.72f),
                new Vector3(0.05f, 0.044f, 0.04f), CDark);

            // ── 전체 디테일 보강 (붐) ──
            // 전력/제어 도관(conduit) — 붐 하부 전장 양옆
            for (int s = -1; s <= 1; s += 2)
            {
                Rod(boom, "Conduit",
                    new Vector3(x0, -0.012f, s * GirderZ * 0.5f),
                    new Vector3(x1, -0.012f, s * GirderZ * 0.5f), 0.004f, CDark);
            }
            // 붐 코너 작업등 추가
            foreach (float lx in new[] { x0 + 0.06f, x1 - 0.05f })
            {
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(boom, "Floodlight_Housing", new Vector3(lx, -0.006f, s * GirderZ * 0.5f),
                        new Vector3(0.014f, 0.012f, 0.014f), CDark);
                    Ball(boom, "Floodlight_Lens", new Vector3(lx, -0.014f, s * GirderZ * 0.5f),
                        new Vector3(0.011f, 0.006f, 0.011f), CLight);
                }
            }
        }

        // 접근 디테일: 다리 수직 사다리 + 포털 상단 점검 플랫폼 + 난간
        static void BuildAccessDetails(Transform root)
        {
            // 다리 오른쪽(star측) 케이지 수직 사다리 — 격자 다리 외곽 밖으로 띄워 겹침 방지
            float legOuter = GaugeZ * 0.5f + LegSec * 1.7f * 0.5f;   // 격자 다리 외곽 z
            float ladderZ  = legOuter + 0.022f;                     // 사다리 위치
            float ly0 = 0.05f, ly1 = RailH - 0.02f;
            BuildLadder(root, LandLegX, ladderZ, ly0, ly1, 0.03f);

            // 다리에 고정하는 standoff 브래킷
            for (int i = 0; i <= 5; i++)
            {
                float by = Mathf.Lerp(ly0, ly1, i / 5f);
                Box(root, "Ladder_Bracket",
                    new Vector3(LandLegX, by, (legOuter + ladderZ) * 0.5f),
                    new Vector3(0.005f, 0.005f, ladderZ - legOuter), CStruct);
            }

            // 안전 케이지 — 외측 세로 가드바 3 + 후프(ㄷ자) 다단 (하부는 승하강 위해 생략)
            float cageZ = ladderZ + 0.03f;
            float cy0 = ly0 + 0.12f;
            foreach (float gx2 in new[] { -0.022f, 0f, 0.022f })
            {
                Box(root, "Cage_Bar", new Vector3(LandLegX + gx2, (cy0 + ly1) * 0.5f, cageZ),
                    new Vector3(0.004f, ly1 - cy0, 0.004f), CStruct);
            }
            int hoops = 6;
            for (int i = 0; i <= hoops; i++)
            {
                float hy = Mathf.Lerp(cy0, ly1, i / (float)hoops);
                Box(root, "Cage_Hoop", new Vector3(LandLegX, hy, cageZ),
                    new Vector3(0.05f, 0.004f, 0.004f), CStruct);
                for (int s = -1; s <= 1; s += 2)
                {
                    Box(root, "Cage_Hoop", new Vector3(LandLegX + s * 0.025f, hy, (cageZ + ladderZ) * 0.5f),
                        new Vector3(0.004f, 0.004f, cageZ - ladderZ), CStruct);
                }
            }

            // 중간 휴식 랜딩 + 난간 — 사다리 정면을 막아서 일단 보류(주석). 나중에 옆으로 빼서 복구.
            // float landY = Mathf.Lerp(ly0, ly1, 0.55f);
            // Box(root, "Ladder_Landing", new Vector3(LandLegX, landY, ladderZ + 0.02f),
            //     new Vector3(0.05f, 0.004f, 0.045f), CMachine);
            // Box(root, "Ladder_Landing_Rail", new Vector3(LandLegX, landY + 0.03f, ladderZ + 0.042f),
            //     new Vector3(0.05f, 0.004f, 0.004f), CStruct);

            // 육지쪽 접근 계단(port측) — 어둡고 급경사라 사다리처럼 보여 일단 보류(주석). 나중에 완경사로 복구.
            /*
            float stairZ = -(GaugeZ * 0.5f + LegSec * 1.7f * 0.5f + 0.02f);
            BuildStair(root, LandLegX - 0.04f, stairZ, 0f, RailH * 0.55f, 0.10f, 9, CMachine);
            // 계단 상단 랜딩 + 난간
            Box(root, "Stair_Landing", new Vector3(LandLegX + 0.065f, RailH * 0.55f, stairZ),
                new Vector3(0.05f, 0.004f, 0.05f), CMachine);
            Box(root, "Landing_Rail", new Vector3(LandLegX + 0.065f, RailH * 0.55f + 0.03f, stairZ - 0.022f),
                new Vector3(0.05f, 0.004f, 0.004f), CStruct);
            */

            // 전력 케이블 릴(드럼) — 육지쪽 베이스. 사용자 요청으로 보류(주석). 주석만 풀면 복구.
            /*
            float drumX = LandLegX - 0.07f;
            Box(root, "Reel_Frame", new Vector3(drumX, 0.03f, 0f),
                new Vector3(0.05f, 0.06f, 0.07f), CStruct);
            Rod(root, "Cable_Reel",
                new Vector3(drumX, 0.05f, -0.025f),
                new Vector3(drumX, 0.05f, 0.025f), 0.032f, CDark);
            */

            // 갠트리 페스툰(주행 급전) — 실 빔 따라 트랙 + 늘어진 케이블 루프(port측)
            float gfZ = -(GaugeZ * 0.5f);
            Box(root, "Gantry_Festoon_Track",
                new Vector3((LandLegX + WaterLegX) * 0.5f, 0.088f, gfZ),
                new Vector3(LegSpanX, 0.004f, 0.004f), CDark);
            for (int i = 0; i < 6; i++)
            {
                float fx = Mathf.Lerp(LandLegX + 0.03f, WaterLegX - 0.03f, i / 5f);
                Box(root, "Gantry_Festoon_Cable", new Vector3(fx, 0.073f, gfZ),
                    new Vector3(0.005f, 0.028f, 0.005f), CDark);
            }

            // 포털 상단 점검 캣워크 — 육지측 두 다리(port/star) 사이를 잇는 통로(그레이팅 + 난간 + 받침)
            float apY = RailH - 0.03f;          // 데크 높이(붐 거더 아래)
            float apHZ = GaugeZ * 0.5f;         // 다리 위치(±)까지
            float apW = 0.05f;                  // 통로 폭(X)
            Box(root, "Access_Platform", new Vector3(LandLegX, apY, 0f),
                new Vector3(apW, 0.004f, apHZ * 2f), CMachine);
            // 다리에 받침 브래킷(떠 있지 않게) — 양 끝
            for (int s = -1; s <= 1; s += 2)
                Strut(root, "Platform_Bracket",
                    new Vector3(LandLegX, apY - 0.045f, s * apHZ),
                    new Vector3(LandLegX, apY - 0.002f, s * apHZ * 0.55f), 0.006f, CStruct);
            // 난간 — 긴 옆면(±X) 양쪽: 상단+중간 레일 + 토보드 + 기둥
            for (int sx = -1; sx <= 1; sx += 2)
            {
                float rx = LandLegX + sx * apW * 0.5f;
                Box(root, "Platform_Rail", new Vector3(rx, apY + 0.032f, 0f),
                    new Vector3(0.004f, 0.004f, apHZ * 2f), CStruct);
                Box(root, "Platform_RailMid", new Vector3(rx, apY + 0.017f, 0f),
                    new Vector3(0.003f, 0.003f, apHZ * 2f), CStruct);
                Box(root, "Platform_Toe", new Vector3(rx, apY + 0.006f, 0f),
                    new Vector3(0.003f, 0.008f, apHZ * 2f), CStruct);
                int np = 6;
                for (int i = 0; i <= np; i++)
                {
                    float pz = Mathf.Lerp(-apHZ, apHZ, i / (float)np);
                    Box(root, "Platform_Post", new Vector3(rx, apY + 0.017f, pz),
                        new Vector3(0.004f, 0.034f, 0.004f), CStruct);
                }
            }
        }

        // 스테이 케이블 + 앵커 플레이트(양끝) + 턴버클(붐 쪽 하단)
        static void BuildStay(Transform root, Vector3 a, Vector3 b, string name)
        {
            Rod(root, name, a, b, 0.004f, CCable);
            Box(root, "Stay_Plate", a, new Vector3(0.012f, 0.012f, 0.012f), CMachine);
            Box(root, "Stay_Plate", b, new Vector3(0.012f, 0.012f, 0.012f), CMachine);
            Vector3 dir = a - b;
            float len = dir.magnitude;
            if (len > 1e-4f)
            {
                dir /= len;
                Rod(root, "Turnbuckle", b + dir * 0.03f, b + dir * 0.06f, 0.008f, CDark);
            }
        }

        // 접근 계단: 발판 + 양옆 경사 스트링거 + 난간 (x방향으로 전진하며 상승)
        static void BuildStair(Transform parent, float x, float z, float y0, float y1,
                               float runX, int steps, Color c)
        {
            float dy = (y1 - y0) / steps;
            float dx = runX / steps;
            for (int i = 0; i < steps; i++)
            {
                float sx = x + dx * (i + 0.5f);
                float sy = y0 + dy * (i + 0.5f);
                Box(parent, "Stair_Step", new Vector3(sx, sy, z),
                    new Vector3(dx * 1.1f, 0.004f, 0.034f), c);
            }
            for (int s = -1; s <= 1; s += 2)
            {
                float sz = z + s * 0.018f;
                Strut(parent, "Stair_Stringer",
                    new Vector3(x, y0, sz), new Vector3(x + runX, y1, sz), 0.004f, c);
                Strut(parent, "Stair_Rail",
                    new Vector3(x, y0 + 0.03f, sz), new Vector3(x + runX, y1 + 0.03f, sz), 0.003f, c);
            }
        }

        // 경사 사다리: a→b 축을 따라 2 stile + 가로 rung. offset 만큼 바깥으로 띄워 구조물 박힘 방지.
        static void BuildInclinedLadder(Transform root, Vector3 a, Vector3 b, float width, Vector3 offset)
        {
            a += offset; b += offset;
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-5f) return;
            d /= len;
            Vector3 side = Vector3.Cross(d, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side = side.normalized;
            for (int s = -1; s <= 1; s += 2)
            {
                Strut(root, "Ladder_Stile",
                    a + side * (s * width * 0.5f), b + side * (s * width * 0.5f), 0.004f, CStruct);
            }
            int rungs = Mathf.Max(3, Mathf.RoundToInt(len / 0.04f));
            for (int i = 0; i <= rungs; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, i / (float)rungs);
                Rod(root, "Ladder_Rung",
                    p - side * (width * 0.5f), p + side * (width * 0.5f), 0.0025f, CStruct);
            }
        }

        // 수직 사다리: 양옆 stile + 가로 rung(원통)
        static void BuildLadder(Transform parent, float x, float z, float y0, float y1, float widthX)
        {
            float midY = (y0 + y1) * 0.5f;
            float h = y1 - y0;
            for (int s = -1; s <= 1; s += 2)
            {
                Box(parent, "Ladder_Stile", new Vector3(x + s * widthX * 0.5f, midY, z),
                    new Vector3(0.004f, h, 0.004f), CStruct);
            }
            int rungs = Mathf.Max(2, Mathf.RoundToInt(h / 0.04f));
            for (int i = 0; i <= rungs; i++)
            {
                float ry = Mathf.Lerp(y0, y1, i / (float)rungs);
                Rod(parent, "Ladder_Rung",
                    new Vector3(x - widthX * 0.5f, ry, z),
                    new Vector3(x + widthX * 0.5f, ry, z), 0.0022f, CStruct);
            }
        }

        // ───────────────────────── primitive 헬퍼 ─────────────────────────

        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = NewCube(name, parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Colorize(go, color);
            return go;
        }

        // a→b 를 잇는 가는 막대(다리/케이블/대각). 부모는 회전·스케일 없는 노드여야 정확.
        static GameObject Strut(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Color color)
        {
            var go = NewCube(name, parent);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            go.transform.localPosition = (a + b) * 0.5f;
            if (len > 1e-5f)
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            go.transform.localScale = new Vector3(thickness, len, thickness);
            Colorize(go, color);
            return go;
        }

        // a→b 를 잇는 원통(케이블/로프/바퀴 등 둥근 부재). 기본 실린더 높이 2 기준.
        static GameObject Rod(Transform parent, string name, Vector3 a, Vector3 b, float radius, Color color)
        {
            var go = NewPrimitive(PrimitiveType.Cylinder, name, parent);
            Vector3 dir = b - a;
            float len = dir.magnitude;
            go.transform.localPosition = (a + b) * 0.5f;
            if (len > 1e-5f)
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            go.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            Colorize(go, color);
            return go;
        }

        // 구체(램프·돔·풍속계 컵 등 둥근 부재). scale로 눌러 돔/타원도 표현.
        static GameObject Ball(Transform parent, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = NewPrimitive(PrimitiveType.Sphere, name, parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Colorize(go, color);
            return go;
        }

        // 원뿔/절두원뿔 — Unity 기본 프리미티브에 없어 메시를 절차 생성.
        // a=밑면 중심, b=꼭대기 중심. rBottom=밑면 반지름, rTop=윗면 반지름(0이면 뾰족한 콘).
        static GameObject Cone(Transform parent, string name, Vector3 a, Vector3 b,
                               float rBottom, float rTop, Color color, int seg = 24)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = BuildFrustumMesh(rBottom, rTop, seg);   // 단위 높이(y=0→1), 반지름은 메시에 반영

            Vector3 dir = b - a;
            float len = dir.magnitude;
            go.transform.localPosition = a;
            if (len > 1e-5f)
                go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir / len);
            go.transform.localScale = new Vector3(1f, len, 1f);     // 높이만 늘림(반지름 유지)
            mr.sharedMaterial = GetMaterial(color);
            return go;
        }

        // 단위 높이(y=0→1) 절두원뿔 메시 — 옆면 + 위/아래 캡.
        static Mesh BuildFrustumMesh(float rBottom, float rTop, int seg)
        {
            seg = Mathf.Max(8, seg);
            var verts = new List<Vector3>(seg * 2 + 2);
            var tris  = new List<int>(seg * 12);
            for (int i = 0; i < seg; i++)
            {
                float ang = (i / (float)seg) * Mathf.PI * 2f;
                float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
                verts.Add(new Vector3(cx * rBottom, 0f, cz * rBottom));  // 밑면 i → 2i
                verts.Add(new Vector3(cx * rTop,    1f, cz * rTop));     // 윗면 i → 2i+1
            }
            for (int i = 0; i < seg; i++)
            {
                int b0 = 2 * i, t0 = 2 * i + 1;
                int j = (i + 1) % seg;
                int b1 = 2 * j, t1 = 2 * j + 1;
                tris.Add(b0); tris.Add(t0); tris.Add(b1);   // 옆면(바깥 향함)
                tris.Add(b1); tris.Add(t0); tris.Add(t1);
            }
            int cBot = verts.Count; verts.Add(new Vector3(0f, 0f, 0f));
            int cTop = verts.Count; verts.Add(new Vector3(0f, 1f, 0f));
            for (int i = 0; i < seg; i++)
            {
                int j = (i + 1) % seg;
                tris.Add(cBot); tris.Add(2 * i);     tris.Add(2 * j);       // 아래 캡(-Y)
                tris.Add(cTop); tris.Add(2 * j + 1); tris.Add(2 * i + 1);   // 위 캡(+Y)
            }
            var m = new Mesh { name = "STS_Cone" };
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // 모서리 베벨(챔퍼) 큐브를 사용 — 모든 Box/Strut가 공유 메시로 "마감된 느낌"
        static GameObject NewCube(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = GetBeveledCube();
            go.AddComponent<MeshRenderer>();
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        // 12 모서리를 살짝 깎은 단위 큐브(챔퍼) 메시. 면 방향은 outward로 자동 보정, 면당 UV 0~1 유지.
        static Mesh GetBeveledCube()
        {
            if (_beveledCube != null) return _beveledCube;
            const float h = 0.5f, c = 0.06f;   // c=베벨 폭(살짝)
            float b = h - c;
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            Vector3[] ax = { Vector3.right, Vector3.up, Vector3.forward };

            // 6 메인 면(축소된 사각형)
            for (int a = 0; a < 3; a++)
            for (int s = -1; s <= 1; s += 2)
            {
                int a1 = (a + 1) % 3, a2 = (a + 2) % 3;
                AddBevQuad(v, t, uv,
                    AxV(a, s * h, a1, -b, a2, -b), AxV(a, s * h, a1, b, a2, -b),
                    AxV(a, s * h, a1, b, a2, b),   AxV(a, s * h, a1, -b, a2, b), ax[a] * s);
            }
            // 12 모서리 챔퍼 면
            for (int a = 0; a < 3; a++)
            for (int s1 = -1; s1 <= 1; s1 += 2)
            for (int s2 = -1; s2 <= 1; s2 += 2)
            {
                int a1 = (a + 1) % 3, a2 = (a + 2) % 3;
                AddBevQuad(v, t, uv,
                    AxV(a1, s1 * h, a2, s2 * b, a, -b), AxV(a1, s1 * h, a2, s2 * b, a, b),
                    AxV(a1, s1 * b, a2, s2 * h, a, b),  AxV(a1, s1 * b, a2, s2 * h, a, -b),
                    ax[a1] * s1 + ax[a2] * s2);
            }
            // 8 코너 삼각형
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AddBevTri(v, t, uv,
                    new Vector3(sx * h, sy * b, sz * b), new Vector3(sx * b, sy * h, sz * b),
                    new Vector3(sx * b, sy * b, sz * h), new Vector3(sx, sy, sz));

            var m = new Mesh { name = "STS_BeveledCube" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return _beveledCube = m;
        }

        // 축 인덱스로 Vector3 구성
        static Vector3 AxV(int a, float va, int a1, float va1, int a2, float va2)
        { var p = Vector3.zero; p[a] = va; p[a1] = va1; p[a2] = va2; return p; }

        // 사각/삼각 추가 — outward 기준으로 와인딩 자동 보정(뒤집힘 방지) + UV 0~1
        static void AddBevQuad(List<Vector3> v, List<int> t, List<Vector2> uv,
                               Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(0, 1));
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            { t.Add(i); t.Add(i + 2); t.Add(i + 1); t.Add(i); t.Add(i + 3); t.Add(i + 2); }
            else
            { t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3); }
        }

        static void AddBevTri(List<Vector3> v, List<int> t, List<Vector2> uv,
                              Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c);
            uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(1, 0)); uv.Add(new Vector2(0.5f, 1));
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0f)
            { t.Add(i); t.Add(i + 2); t.Add(i + 1); }
            else
            { t.Add(i); t.Add(i + 1); t.Add(i + 2); }
        }

        static GameObject NewPrimitive(PrimitiveType type, string name, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            // 에디터 클릭 방해 줄이려 collider 제거(시각화 전용).
            var col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            go.transform.SetParent(parent, worldPositionStays: false);
            return go;
        }

        static void Colorize(GameObject go, Color c)
        {
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
            r.sharedMaterial = GetMaterial(c);
        }

        // 같은 색 머티리얼 재사용 — 프로젝트 머티리얼 오염 방지 위해 인스턴스(에셋 미저장).
        // 단색 평면 → 절차 강철 텍스처(albedo) + 카테고리별 PBR + 일부 에미시브.
        static Material GetMaterial(Color c)
        {
            if (_matCache != null && _matCache.TryGetValue(c, out var cached)) return cached;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = "STS_Mat" };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", c);

            // 카테고리별 PBR + 텍스처 적용 여부
            float metallic = 0.30f, smooth = 0.35f;
            bool  useTex = true, emissive = false;
            float emi = 0f;

            if (Same(c, CGlass))       { metallic = 0.0f;  smooth = 0.92f; useTex = false; }              // 유리: 매끈
            else if (Same(c, CCable))  { metallic = 0.0f;  smooth = 0.15f; useTex = false; }              // 로프: 매트
            else if (Same(c, CLight))  { metallic = 0.0f;  smooth = 0.60f; useTex = false; emissive = true; emi = 1.8f; } // 작업등 렌즈: 발광
            else if (Same(c, CWarn))   { metallic = 0.0f;  smooth = 0.55f; emissive = true; emi = 1.1f; } // 경고/항공등: 약발광
            else if (Same(c, CDark))   { metallic = 0.20f; smooth = 0.25f; }                              // 다크 강철/고무
            else if (Same(c, CRail))   { metallic = 0.65f; smooth = 0.55f; }                              // 마모된 레일: 금속 광택
            else if (Same(c, CMachine)){ metallic = 0.35f; smooth = 0.30f; }                              // 기계실 도장
            // 그 외(CStruct/CBoom/CTrolley/CSpread): 칠한 구조 강철 기본값

            if (useTex)
            {
                var tex = GetSteelTexture();
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smooth);
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c * emi);
            }

            _matCache?.Add(c, mat);
            return mat;
        }

        // 색 근사 비교(머티리얼 카테고리 분류용)
        static bool Same(Color a, Color b)
            => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.01f;

        // 절차 생성 강철 디테일 텍스처 — 그레이스케일(평균≈0.9), 다중 옥타브 노이즈 + 세로 때 스트릭 + 미세 그레인.
        // _BaseColor가 곱해져 각 색의 "칠한 강철" 질감이 됨. 에셋으로 저장하지 않음.
        static Texture2D GetSteelTexture()
        {
            if (_steelTex != null) return _steelTex;

            const int N = 256;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, true) { name = "STS_SteelDetail", wrapMode = TextureWrapMode.Repeat };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                // 다중 옥타브 노이즈(얼룩/때)
                float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.55f
                        + Mathf.PerlinNoise(x * 0.13f + 100f, y * 0.13f) * 0.30f
                        + Mathf.PerlinNoise(x * 0.40f + 50f,  y * 0.40f) * 0.15f;
                // 세로로 흘러내린 때 스트릭(상단일수록 옅게)
                float streak = Mathf.PerlinNoise(x * 0.6f, y * 0.015f);
                streak = Mathf.SmoothStep(0.62f, 1f, streak) * (y / (float)N) * 0.18f;
                // 미세 그레인
                float grain = (Hash(x, y) - 0.5f) * 0.05f;

                float v = Mathf.Clamp01(0.93f + (n - 0.5f) * 0.22f + grain - streak);
                byte b = (byte)(v * 255f);
                px[y * N + x] = new Color32(b, b, b, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(true);
            return _steelTex = tex;
        }

        // 결정적 정수 해시 → [0,1) (텍스처 그레인용)
        static float Hash(int x, int y)
        {
            int h = x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0x7fffffff) / 2147483647f;
        }

        static Vector3 FindContainerAnchor()
        {
            var spawnerType = System.Type.GetType("ContainerProject.ContainerSpawner, Assembly-CSharp");
            if (spawnerType != null)
            {
                var spawner = Object.FindFirstObjectByType(spawnerType) as Component;
                if (spawner != null) return spawner.transform.position;
            }
            var named = GameObject.Find("Container_Procedural");
            if (named != null) return named.transform.position;
            return Vector3.zero;
        }
    }
}
#endif
