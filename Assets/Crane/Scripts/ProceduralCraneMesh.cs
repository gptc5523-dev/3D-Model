using System.Collections.Generic;
using UnityEngine;

namespace CraneProject
{
    /// <summary>
    /// 항만 크레인 절차적 메시 생성기.
    /// 두 가지 기종 지원:
    ///   - STS Quay Crane (Ship-to-Shore, 안벽)  : BuildSts(...)
    ///   - RTG Container Crane (야드)             : BuildRtg(...)
    /// 모두 ISO 668 22G1 20ft 컨테이너 (6.058 × 2.438 × 2.591 m) 실측 기준으로 비례.
    /// 기본 출력은 컨테이너와 동일한 1/24 미니어처 스케일.
    ///
    /// 서브메시 (컨테이너 4 슬롯 컨벤션 확장):
    ///   0 = Body       (도장 — 런타임 상태 색상 변경 대상, 회청색)
    ///   1 = Frame      (다리·빔 — 다크 그레이 고정)
    ///   2 = Mechanism  (트롤리·스프레더·헤드블록 — 노란색 고정)
    ///   3 = Cabin      (운전실 박스 — 진회색 고정)
    ///   4 = Stripes    (항만 안전 빗금·경고색 — 노검 또는 적백)
    ///   5 = Glass      (운전실 창문·등기구 — 짙은 유리색)
    /// </summary>
    public static class ProceduralCraneMesh
    {
        public const float DefaultMiniatureScale = 1f / 24f;
        public const int SubmeshCount = 6;

        // 20ft ISO 668 22G1 실측 (컨테이너와 정합 — m)
        public const float ContainerLength = 6.058f;
        public const float ContainerWidth  = 2.438f;
        public const float ContainerHeight = 2.591f;

        // ───────── STS Quay Crane 치수 (게임 비례 — 컨테이너 길이 L=6.058m 기준) ─────────
        // 실측은 Apex 75m / Outreach 65m로 거대하지만, 컨테이너(L=6m) 옆에 둘 때
        // 게임 비례로 축소. 1/24 적용 시 화면상 약 0.46 × 0.29 m 풋프린트.
        const float StsRailGauge      = 11.0f;  // 다리 간 폭 (Z축) — 약 1.8L
        const float StsPortalHeight   = 8.0f;   // 다리 클리어런스 — 컨테이너 트리플 스택(7.77m) 통과 가능
        const float StsApexHeight     = 22.0f;  // A-frame 정상 (마스트 높이 약 10m 유지)
        const float StsOutreach       = 15.0f;  // 붐 바다쪽 돌출 — 약 2.5L
        const float StsBackreach      = 5.0f;   // 붐 육지쪽 돌출 — 약 0.83L
        const float StsPortalLength   = 7.0f;   // 다리 풋프린트 길이 (X축) — 약 1.15L
        const float StsLegSection     = 0.7f;   // 다리 트러스 외접 단면
        const float StsLegPillar      = 0.18f;
        const float StsBraceThick     = 0.12f;
        const float StsBoomDepth      = 1.4f;   // 붐 단면 높이
        const float StsBoomWidth      = 0.8f;   // 한 붐 빔 단면 폭
        const float StsBoomSpacing    = 3.0f;   // 두 붐 빔 사이 간격 (트롤리가 들어감 — 트롤리 폭 + 여유)
        const float StsMachineHouseL  = 4.5f;
        const float StsMachineHouseW  = 3.0f;
        const float StsMachineHouseH  = 2.2f;
        const float StsCabinSize      = 1.6f;

        // 부속물
        const float StsBogieLength    = 2.5f;
        const float StsBogieHeight    = 0.6f;
        const float StsBogieWidth     = 1.0f;
        const float StsWheelRadius    = 0.25f;
        const float StsStairWidth     = 1.0f;
        const float StsHandrailHeight = 0.6f;
        const float StsHandrailThick  = 0.05f;
        const float StsStripeHeight   = 0.3f;

        // 구조물 두께 (BuildStsGantry/Boom/Apex/MachineHouse에서 공통 참조)
        const float StsSillH          = 0.4f;   // sill 빔 두께
        const float StsPortalBeamH    = 0.6f;   // portal 빔 두께
        const float StsDeckH          = 1.2f;   // 데크 박스 두께
        // Y 좌표 (위 두께로부터 계산)
        const float StsDeckTopY       = StsPortalHeight + StsPortalBeamH + StsDeckH;       // 다리 위 데크 윗면 = 6.8
        const float StsBoomCenterY    = StsDeckTopY + StsBoomDepth * 0.5f;                 // 붐 중심 = 7.5
        const float StsBoomTopY       = StsDeckTopY + StsBoomDepth;                        // 붐 윗면 = 8.2

        // ───────── RTG Container Crane 치수 (6+1 wide, 1-over-5 high 기준) ─────────
        const float RtgRailGauge      = 26.0f;  // 다리 간 폭 (Z)
        const float RtgClearance      = 18.0f;  // 다리 아래 (1-over-5 + spreader 여유)
        const float RtgTopBeamHeight  = 22.0f;  // 빔 윗면 (Y)
        const float RtgBeamDepth      = 2.4f;
        const float RtgGantryLength   = 11.0f;  // 다리 풋프린트 길이 (X)
        const float RtgLegThickness   = 1.2f;

        // ───────── 트롤리·스프레더·헤드블록 (두 기종 공통) ─────────
        // 트롤리: 두 붐 빔 사이를 슬라이딩 — BoomSpacing보다 폭이 작아야 함.
        const float TrolleyLength = 3.0f;
        const float TrolleyWidth  = 2.6f;   // < StsBoomSpacing (3.0)
        const float TrolleyHeight = 1.0f;
        // 스프레더/헤드블록은 컨테이너 잡기 위해 실측 비례 그대로 유지
        const float HeadblockLength = ContainerLength * 0.9f;
        const float HeadblockWidth  = ContainerWidth  * 1.1f;
        const float HeadblockHeight = 0.4f;
        const float SpreaderLength = ContainerLength + 0.1f;
        const float SpreaderWidth  = ContainerWidth  + 0.1f;
        const float SpreaderHeight = 0.3f;
        const float CableThickness = 0.15f;

        // ═════════════════════════════════════════════════════════════════════
        // STS Quay Crane
        // ═════════════════════════════════════════════════════════════════════
        public class StsBuildResult
        {
            public Mesh staticMesh;     // gantry + boom + cables 고정 부분
            public Mesh trolleyMesh;    // 트롤리 (붐 위 X 슬라이딩)
            public Mesh spreaderMesh;   // 스프레더 + 헤드블록 (트롤리 아래 Y 승강)
            public Bounds staticBounds;
            // 트롤리·스프레더의 기본(0) 위치 (스케일 적용 후, 로컬 좌표)
            public Vector3 trolleyRestPos;
            public Vector3 spreaderRestPos;
            // 트롤리가 움직일 수 있는 X 범위 (붐 길이 방향, 미터)
            public float trolleyXMin;
            public float trolleyXMax;
            // 스프레더가 움직일 수 있는 Y 범위
            public float spreaderYMin;
            public float spreaderYMax;
        }

        public static StsBuildResult BuildSts(float scale = DefaultMiniatureScale)
        {
            var staticBuilder   = new MeshBuilder();
            var trolleyBuilder  = new MeshBuilder();
            var spreaderBuilder = new MeshBuilder();

            BuildStsGantry(staticBuilder);
            BuildStsBoom(staticBuilder);
            BuildStsApex(staticBuilder);
            BuildStsMachineHouse(staticBuilder);
            BuildStsCabin(staticBuilder);

            // 트롤리: 자체 메시는 원점 기준 — 인스턴스에서 Transform.position으로 이동
            BuildTrolley(trolleyBuilder);
            // 스프레더(+헤드블록+케이블): 원점 기준
            BuildSpreaderAssembly(spreaderBuilder);

            var staticMesh   = staticBuilder.ToMesh("Crane_STS_Static");
            var trolleyMesh  = trolleyBuilder.ToMesh("Crane_STS_Trolley");
            var spreaderMesh = spreaderBuilder.ToMesh("Crane_STS_Spreader");

            // 기본 자세: 트롤리는 붐 중심 Y(붐 두 빔 사이), 스프레더는 다리 사이 매달림
            float trolleyRailY = StsBoomCenterY;        // 붐 두 빔 사이에 들어감
            float trolleyRestX = StsOutreach * 0.4f;    // 바다쪽 40% 지점
            float spreaderRestY = StsPortalHeight * 0.4f; // 다리 클리어런스 안쪽 (지면 위 2m)

            float trolleyXMin = -StsBackreach + 1.0f;
            float trolleyXMax =  StsOutreach  - 1.0f;
            float spreaderYMin = 0.3f;                  // 지면 근처
            float spreaderYMax = trolleyRailY - 1.5f;   // 트롤리 바로 아래

            ApplyScale(staticMesh, scale);
            ApplyScale(trolleyMesh, scale);
            ApplyScale(spreaderMesh, scale);

            return new StsBuildResult
            {
                staticMesh   = staticMesh,
                trolleyMesh  = trolleyMesh,
                spreaderMesh = spreaderMesh,
                staticBounds = staticMesh.bounds,
                trolleyRestPos  = new Vector3(trolleyRestX, trolleyRailY, 0f) * scale,
                spreaderRestPos = new Vector3(trolleyRestX, spreaderRestY, 0f) * scale,
                trolleyXMin = trolleyXMin * scale,
                trolleyXMax = trolleyXMax * scale,
                spreaderYMin = spreaderYMin * scale,
                spreaderYMax = spreaderYMax * scale,
            };
        }

        static void BuildStsGantry(MeshBuilder b)
        {
            float hz = StsRailGauge * 0.5f;
            float hx = StsPortalLength * 0.5f;
            float legH = StsPortalHeight;
            float section = StsLegSection;

            // 다리 4개 — 4-pillar 트러스 (5단 수평 + 4단 X자 사선)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                BuildLegTruss(b, 1,
                    baseCenter: new Vector3(sx * hx, 0f, sz * hz),
                    height: legH,
                    section: section, pillar: StsLegPillar, brace: StsBraceThick,
                    horizontalRungs: 5);
            }

            // 다리 아래 휠 보기 4개 (각 다리 바닥)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                BuildBogie(b,
                    center: new Vector3(sx * hx, -StsBogieHeight, sz * hz),
                    length: StsBogieLength, height: StsBogieHeight, width: StsBogieWidth,
                    wheelR: StsWheelRadius, wheelCount: 4);
            }

            // 다리 아래쪽 안전 줄무늬 띠 (서브메시 4 = Stripes, 노검 빗금 머티리얼이 칠해줌)
            float stripeY = StsStripeHeight * 0.5f + 0.3f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(4,
                    center: new Vector3(sx * hx, stripeY, sz * hz),
                    size:   new Vector3(section + 0.01f, StsStripeHeight, section + 0.01f));
            }

            // Sill beam (다리 바닥 연결, 좌/우, X 방향 — 다리 사이 가로빔)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(1,
                    center: new Vector3(0f, StsBogieHeight + StsSillH * 0.5f, sz * hz),
                    size:   new Vector3(StsPortalLength + section, StsSillH, section * 1.1f));
            }
            // Sill 좌우 연결 (Z방향)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * hx, StsBogieHeight + StsSillH * 0.5f, 0f),
                    size:   new Vector3(section * 1.1f, StsSillH, StsRailGauge + section));
            }

            // Portal beam (다리 윗쪽 연결, sea/land — 트롤리 레일 바로 아래) — 박스 + 트러스 사선
            float portalBeamY = legH;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * hx, portalBeamY + StsPortalBeamH * 0.5f, 0f),
                    size:   new Vector3(section * 1.2f, StsPortalBeamH, StsRailGauge + section));
                // 포털 빔 아래쪽 X자 사선 보강 (지상에서 잘 보이는 K-bracing)
                AddSlantBeam(b, 1,
                    p0: new Vector3(sx * hx, portalBeamY, -hz),
                    p1: new Vector3(sx * hx, portalBeamY - 1.2f, 0f),
                    thickness: 0.18f);
                AddSlantBeam(b, 1,
                    p0: new Vector3(sx * hx, portalBeamY, hz),
                    p1: new Vector3(sx * hx, portalBeamY - 1.2f, 0f),
                    thickness: 0.18f);
            }

            // 데크 (운영자용 캣워크 박스 — 다리 위쪽 박스 구조)
            float deckY = legH + StsPortalBeamH;
            float deckH = StsDeckH;
            b.AddBox(0,  // 본체 도장 영역 (Body)
                center: new Vector3(0f, deckY + deckH * 0.5f, 0f),
                size:   new Vector3(StsPortalLength + 2f, deckH, StsRailGauge + 1f));

            // 데크 둘레 핸드레일 (Frame)
            BuildHandrailXZ(b, 1,
                center: new Vector3(0f, deckY + deckH, 0f),
                lengthX: StsPortalLength + 2f,
                lengthZ: StsRailGauge + 1f,
                railHeight: StsHandrailHeight,
                thick: StsHandrailThick);

            // 계단 타워는 게임에서 5번째 다리로 오인되어 제거. 실물 STS에는 있지만 게임 비례에선 혼동.
        }

        static void BuildStsBoom(MeshBuilder b)
        {
            // 붐: 두 개의 평행 빔 (트롤리가 들어감). 끝쪽으로 갈수록 살짝 처지지만 단순화.
            float boomY = StsBoomCenterY; // 데크 위에 얹힘 (StsDeckTopY + StsBoomDepth/2)
            float halfSpacing = StsBoomSpacing * 0.5f;

            // X 범위: -Backreach ~ +Outreach
            float boomCenterX = (StsOutreach - StsBackreach) * 0.5f;
            float boomTotalLen = StsOutreach + StsBackreach;

            // 좌/우 빔 — 본체
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(0,
                    center: new Vector3(boomCenterX, boomY, sz * halfSpacing),
                    size:   new Vector3(boomTotalLen, StsBoomDepth, StsBoomWidth));

                // 빔 측면 안전 줄무늬 띠 (상단과 하단 가장자리)
                float stripeH = 0.2f;
                float edgeY = StsBoomDepth * 0.5f - stripeH * 0.5f;
                b.AddBox(4,
                    center: new Vector3(boomCenterX, boomY + edgeY, sz * (halfSpacing + StsBoomWidth * 0.5f + 0.005f)),
                    size:   new Vector3(boomTotalLen, stripeH, 0.02f));
                b.AddBox(4,
                    center: new Vector3(boomCenterX, boomY - edgeY, sz * (halfSpacing + StsBoomWidth * 0.5f + 0.005f)),
                    size:   new Vector3(boomTotalLen, stripeH, 0.02f));
            }

            // 두 빔 위쪽 연결 캣워크 판 (Body)
            float capH = 0.2f;
            float capY = boomY + StsBoomDepth * 0.5f + capH * 0.5f;
            b.AddBox(0,
                center: new Vector3(boomCenterX, capY, 0f),
                size:   new Vector3(boomTotalLen, capH, StsBoomSpacing + StsBoomWidth));

            // 두 빔 사이 트러스 사선 보강 (붐 길이 방향으로 N단)
            int boomBays = 8;
            float bayLen = boomTotalLen / boomBays;
            for (int i = 0; i < boomBays; i++)
            {
                float x0 = -StsBackreach + i * bayLen;
                float x1 = x0 + bayLen;
                // 빔 사이(Z) X자 — 윗면(캡 아래)
                AddSlantBeam(b, 1,
                    p0: new Vector3(x0, boomY + StsBoomDepth * 0.45f, -halfSpacing),
                    p1: new Vector3(x1, boomY + StsBoomDepth * 0.45f,  halfSpacing),
                    thickness: 0.10f);
                AddSlantBeam(b, 1,
                    p0: new Vector3(x0, boomY + StsBoomDepth * 0.45f,  halfSpacing),
                    p1: new Vector3(x1, boomY + StsBoomDepth * 0.45f, -halfSpacing),
                    thickness: 0.10f);
                // 빔 아래쪽 보강 (트롤리 들어가는 공간이지만 빔 사이 폭 끝단만)
                if (i % 2 == 0)
                {
                    b.AddBox(1,
                        center: new Vector3((x0 + x1) * 0.5f, boomY - StsBoomDepth * 0.4f, 0f),
                        size:   new Vector3(0.18f, 0.18f, StsBoomSpacing));
                }
            }

            // 캣워크 핸드레일 (붐 위)
            BuildHandrailXZ(b, 1,
                center: new Vector3(boomCenterX, capY + capH * 0.5f, 0f),
                lengthX: boomTotalLen,
                lengthZ: StsBoomSpacing + StsBoomWidth,
                railHeight: StsHandrailHeight,
                thick: StsHandrailThick);

            // 붐 끝(바다쪽) 접이식 힌지 + 보호 케이지 + 네비게이션 라이트
            // 힌지 박스 (Frame)
            b.AddBox(1,
                center: new Vector3(StsOutreach + 0.4f, boomY, 0f),
                size:   new Vector3(0.8f, StsBoomDepth * 1.2f, StsBoomSpacing + StsBoomWidth + 0.5f));

            // 보호 케이지 (Frame, 얇은 사다리꼴 박스)
            float cageX = StsOutreach + 0.9f;
            b.AddBox(1,
                center: new Vector3(cageX, boomY, 0f),
                size:   new Vector3(0.08f, StsBoomDepth + 0.4f, StsBoomSpacing + StsBoomWidth + 0.2f));
            // 케이지 수직 격자 4줄
            for (int sz = -1; sz <= 1; sz += 2)
            for (int kz = 0; kz < 2; kz++)
            {
                float z = sz * (halfSpacing - 0.15f - kz * 0.3f);
                b.AddBox(1,
                    center: new Vector3(cageX, boomY, z),
                    size:   new Vector3(0.06f, StsBoomDepth + 0.5f, 0.06f));
            }

            // 네비게이션 라이트 2개 (Glass 서브메시 — 발광 유리)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(5,
                    center: new Vector3(cageX + 0.1f, boomY + StsBoomDepth * 0.4f, sz * (halfSpacing - 0.15f)),
                    size:   new Vector3(0.09f, 0.18f, 0.18f));
            }

            // 붐 끝(육지쪽) 균형 박스 (Frame)
            b.AddBox(1,
                center: new Vector3(-StsBackreach - 0.35f, boomY, 0f),
                size:   new Vector3(0.7f, StsBoomDepth * 1.1f, StsBoomSpacing + StsBoomWidth + 0.3f));
        }

        static void BuildStsApex(MeshBuilder b)
        {
            float baseY = StsBoomTopY + 0.3f; // 붐 캡 바로 위 (붐 윗면 8.2m + 약간)
            float topY  = StsApexHeight;
            float mastH = topY - baseY;

            // A-frame 마스트 — 4-pillar 트러스 (Body 도장)
            BuildLegTruss(b, 0,
                baseCenter: new Vector3(0f, baseY, 0f),
                height: mastH,
                section: 1.2f, pillar: 0.2f, brace: 0.12f,
                horizontalRungs: 6);

            // 정상 캡 박스 (Body)
            b.AddBox(0,
                center: new Vector3(0f, topY, 0f),
                size:   new Vector3(1.8f, 0.8f, 1.8f));

            // 정상 안테나 마스트 (Frame, 얇고 길게)
            b.AddBox(1,
                center: new Vector3(0f, topY + 1.25f, 0f),
                size:   new Vector3(0.12f, 2.5f, 0.12f));
            b.AddBox(5, // Glass — 안테나 끝 항공장애등
                center: new Vector3(0f, topY + 2.6f, 0f),
                size:   new Vector3(0.18f, 0.18f, 0.18f));

            // Forestay (정상 → 붐 끝 바다쪽) 3가닥 + Z 방향 좌·우 펴짐
            float[] forestayZ = { -0.6f, 0f, 0.6f };
            foreach (float z in forestayZ)
            {
                AddSlantBeam(b, 1,
                    p0: new Vector3(0f, topY - 0.3f, z * 0.5f),
                    p1: new Vector3(StsOutreach - 0.5f, baseY, z),
                    thickness: 0.08f);
            }
            // Forestay 인장 클램프 (정상측)
            b.AddBox(1,
                center: new Vector3(1.0f, topY - 0.4f, 0f),
                size:   new Vector3(0.8f, 0.3f, 1.6f));

            // Backstay (정상 → 붐 끝 육지쪽) 3가닥
            foreach (float z in forestayZ)
            {
                AddSlantBeam(b, 1,
                    p0: new Vector3(0f, topY - 0.3f, z * 0.5f),
                    p1: new Vector3(-StsBackreach + 0.5f, baseY, z),
                    thickness: 0.08f);
            }
            // Backstay 인장 클램프
            b.AddBox(1,
                center: new Vector3(-1.0f, topY - 0.4f, 0f),
                size:   new Vector3(0.8f, 0.3f, 1.6f));
        }

        static void BuildStsMachineHouse(MeshBuilder b)
        {
            // 데크 위(붐 위, A-frame 옆)에 얹힌 박스
            float houseY = StsBoomTopY + 0.3f;
            float houseCx = -StsBackreach * 0.4f;
            float houseTopY = houseY + StsMachineHouseH;

            // 본체 (Body)
            b.AddBox(0,
                center: new Vector3(houseCx, houseY + StsMachineHouseH * 0.5f, 0f),
                size:   new Vector3(StsMachineHouseL, StsMachineHouseH, StsMachineHouseW));

            // 측면 한쪽 안전 줄무늬 띠 (Stripes 4 — 노검 빗금)
            b.AddBox(4,
                center: new Vector3(houseCx, houseY + 0.3f, StsMachineHouseW * 0.5f + 0.005f),
                size:   new Vector3(StsMachineHouseL * 0.85f, 0.3f, 0.02f));

            // 윗면 환기 루버 패널 3개 (Frame)
            for (int i = -1; i <= 1; i++)
            {
                b.AddBox(1,
                    center: new Vector3(houseCx + i * 1.4f, houseTopY + 0.15f, 0f),
                    size:   new Vector3(0.9f, 0.3f, 1.8f));
            }

            // 윗면 안전 등 2개 (Glass 5 — 발광)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(5,
                    center: new Vector3(houseCx + StsMachineHouseL * 0.4f, houseTopY + 0.1f, sz * (StsMachineHouseW * 0.4f)),
                    size:   new Vector3(0.25f, 0.15f, 0.25f));
            }

            // 측면(육지쪽) 외부 사다리 (Frame) — 수직 두 봉 + 디딤판
            float ladderX = houseCx - StsMachineHouseL * 0.5f - 0.08f;
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(1,
                    center: new Vector3(ladderX, houseY + StsMachineHouseH * 0.5f, sz * 0.18f),
                    size:   new Vector3(0.06f, StsMachineHouseH, 0.06f));
            }
            int rungs = 5;
            for (int i = 0; i < rungs; i++)
            {
                float ry = houseY + 0.15f + i * ((StsMachineHouseH - 0.3f) / (rungs - 1));
                b.AddBox(1,
                    center: new Vector3(ladderX, ry, 0f),
                    size:   new Vector3(0.06f, 0.04f, 0.45f));
            }
        }

        static void BuildStsCabin(MeshBuilder b)
        {
            // 운전실: 데크 아래쪽에 매달림 (바다쪽 다리 안쪽)
            float s = StsCabinSize;
            float cabinY = StsPortalHeight - s * 0.5f - 1.0f;
            float cabinX = StsPortalLength * 0.5f - s * 0.5f - 1.0f; // 바다쪽 다리 안쪽

            // 본체 (Cabin)
            b.AddBox(3,
                center: new Vector3(cabinX, cabinY, 0f),
                size:   new Vector3(s, s, s));

            // 정면(+X) 큰 창문 (Glass 5)
            b.AddBox(5,
                center: new Vector3(cabinX + s * 0.5f + 0.005f, cabinY + 0.15f, 0f),
                size:   new Vector3(0.02f, s * 0.55f, s * 0.85f));
            // 바닥 시야창 (오퍼레이터가 아래 보는 — STS 특유)
            b.AddBox(5,
                center: new Vector3(cabinX + s * 0.35f, cabinY - s * 0.5f - 0.005f, 0f),
                size:   new Vector3(s * 0.55f, 0.02f, s * 0.55f));
            // 좌·우 측면 창문 (Glass 5)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(5,
                    center: new Vector3(cabinX, cabinY + 0.15f, sz * (s * 0.5f + 0.005f)),
                    size:   new Vector3(s * 0.6f, s * 0.5f, 0.02f));
            }
            // 뒷면 도어 (Cabin, 약간 들여)
            b.AddBox(3,
                center: new Vector3(cabinX - s * 0.5f - 0.03f, cabinY - 0.1f, 0f),
                size:   new Vector3(0.06f, s * 0.7f, s * 0.45f));

            // 천장 안테나/회전등 (Glass 5)
            b.AddBox(5,
                center: new Vector3(cabinX, cabinY + s * 0.5f + 0.1f, 0f),
                size:   new Vector3(0.18f, 0.18f, 0.18f));

            // 운전실 데크와 연결되는 지지대 (Frame, 4 코너 위로)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(1,
                    center: new Vector3(cabinX + sx * s * 0.45f, cabinY + s * 0.5f + 0.25f, sz * s * 0.45f),
                    size:   new Vector3(0.08f, 0.5f, 0.08f));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // RTG Container Crane
        // ═════════════════════════════════════════════════════════════════════
        public class RtgBuildResult
        {
            public Mesh staticMesh;
            public Mesh trolleyMesh;
            public Mesh spreaderMesh;
            public Bounds staticBounds;
            public Vector3 trolleyRestPos;
            public Vector3 spreaderRestPos;
            public float trolleyZMin;
            public float trolleyZMax;
            public float spreaderYMin;
            public float spreaderYMax;
        }

        public static RtgBuildResult BuildRtg(float scale = DefaultMiniatureScale)
        {
            var staticBuilder   = new MeshBuilder();
            var trolleyBuilder  = new MeshBuilder();
            var spreaderBuilder = new MeshBuilder();

            BuildRtgGantry(staticBuilder);
            BuildRtgTopBeam(staticBuilder);
            BuildRtgCabin(staticBuilder);

            BuildTrolley(trolleyBuilder);
            BuildSpreaderAssembly(spreaderBuilder);

            var staticMesh   = staticBuilder.ToMesh("Crane_RTG_Static");
            var trolleyMesh  = trolleyBuilder.ToMesh("Crane_RTG_Trolley");
            var spreaderMesh = spreaderBuilder.ToMesh("Crane_RTG_Spreader");

            // 트롤리: top beam 따라 Z축 슬라이딩 (gauge 방향)
            float trolleyRailY = RtgTopBeamHeight - RtgBeamDepth * 0.5f - TrolleyHeight * 0.5f;
            float trolleyRestZ = 0f;
            float spreaderRestY = RtgClearance + 4.0f;

            float trolleyZMin = -(RtgRailGauge * 0.5f) + 2.5f;
            float trolleyZMax =  (RtgRailGauge * 0.5f) - 2.5f;
            float spreaderYMin = 0.5f;
            float spreaderYMax = trolleyRailY - 2.0f;

            ApplyScale(staticMesh, scale);
            ApplyScale(trolleyMesh, scale);
            ApplyScale(spreaderMesh, scale);

            return new RtgBuildResult
            {
                staticMesh = staticMesh,
                trolleyMesh = trolleyMesh,
                spreaderMesh = spreaderMesh,
                staticBounds = staticMesh.bounds,
                trolleyRestPos  = new Vector3(0f, trolleyRailY, trolleyRestZ) * scale,
                spreaderRestPos = new Vector3(0f, spreaderRestY, trolleyRestZ) * scale,
                trolleyZMin = trolleyZMin * scale,
                trolleyZMax = trolleyZMax * scale,
                spreaderYMin = spreaderYMin * scale,
                spreaderYMax = spreaderYMax * scale,
            };
        }

        static void BuildRtgGantry(MeshBuilder b)
        {
            float hz = RtgRailGauge * 0.5f;
            float hx = RtgGantryLength * 0.5f;
            float legH = RtgClearance;
            float t = RtgLegThickness;

            // 다리 4개 (이중 빔 단순화 — 단일 박스)
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * hx, legH * 0.5f, sz * hz),
                    size:   new Vector3(t, legH, t));
            }

            // 사선 보강 (각 측면 X자 보강 단순화 — 좌/우면에 각 1개)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                AddSlantBeam(b, 1,
                    p0: new Vector3(-hx, 1.5f, sz * hz),
                    p1: new Vector3( hx, legH - 1.5f, sz * hz),
                    thickness: 0.5f);
            }

            // 바닥 휠 박스 (각 다리 아래)
            float wheelY = 0.6f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(2,
                    center: new Vector3(sx * hx, wheelY, sz * hz),
                    size:   new Vector3(t * 1.6f, 1.2f, t * 2.0f));
            }
        }

        static void BuildRtgTopBeam(MeshBuilder b)
        {
            float beamY = RtgTopBeamHeight - RtgBeamDepth * 0.5f;
            // 메인 가로빔 (gauge 방향, Z축)
            b.AddBox(0, // Body 도장
                center: new Vector3(0f, beamY, 0f),
                size:   new Vector3(RtgGantryLength * 0.8f, RtgBeamDepth, RtgRailGauge + RtgLegThickness));

            // 트롤리 레일 (얇은 박스 2개, X 방향 한 쌍)
            float railH = 0.4f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * (RtgGantryLength * 0.25f), beamY + RtgBeamDepth * 0.5f + railH * 0.5f, 0f),
                    size:   new Vector3(0.6f, railH, RtgRailGauge));
            }
        }

        static void BuildRtgCabin(MeshBuilder b)
        {
            // 운전실: 트롤리 옆에 매달려 있지만, 단순화로 빔 하단 한쪽 끝에 부착
            float cabinY = RtgTopBeamHeight - RtgBeamDepth - 2.0f;
            b.AddBox(3,
                center: new Vector3(0f, cabinY, RtgRailGauge * 0.5f - 1.5f),
                size:   new Vector3(2.4f, 2.4f, 2.4f));
        }

        // ═════════════════════════════════════════════════════════════════════
        // 트롤리·스프레더·헤드블록 (두 기종 공통, 원점 기준)
        // ═════════════════════════════════════════════════════════════════════
        static void BuildTrolley(MeshBuilder b)
        {
            // 본체 (Mechanism)
            b.AddBox(2,
                center: Vector3.zero,
                size:   new Vector3(TrolleyLength, TrolleyHeight, TrolleyWidth));

            // 윗면 호이스트 드럼 — 좌·우 2개 (Mechanism)
            float drumY = TrolleyHeight * 0.5f + 0.25f;
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(2,
                    center: new Vector3(sx * TrolleyLength * 0.22f, drumY, 0f),
                    size:   new Vector3(TrolleyLength * 0.35f, 0.5f, TrolleyWidth * 0.75f));
                // 드럼 양 끝 캡 (Frame)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    b.AddBox(1,
                        center: new Vector3(sx * TrolleyLength * 0.22f, drumY, sz * (TrolleyWidth * 0.75f * 0.5f + 0.03f)),
                        size:   new Vector3(TrolleyLength * 0.32f, 0.55f, 0.06f));
                }
            }

            // 케이블 가이드 (4 코너 아래 — Frame 작은 풀리 박스)
            float gx = TrolleyLength * 0.5f - 0.2f;
            float gz = TrolleyWidth  * 0.5f - 0.15f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * gx, -TrolleyHeight * 0.5f - 0.1f, sz * gz),
                    size:   new Vector3(0.2f, 0.2f, 0.2f));
            }

            // 측면 안전 줄무늬 (Stripes 4, 좌·우)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(4,
                    center: new Vector3(0f, 0f, sz * (TrolleyWidth * 0.5f + 0.005f)),
                    size:   new Vector3(TrolleyLength * 0.9f, TrolleyHeight * 0.5f, 0.02f));
            }

            // 점검 핸드레일 (Frame, 윗면)
            BuildHandrailXZ(b, 1,
                center: new Vector3(0f, drumY + 0.3f, 0f),
                lengthX: TrolleyLength,
                lengthZ: TrolleyWidth,
                railHeight: 0.4f,
                thick: 0.04f);
        }

        static void BuildSpreaderAssembly(MeshBuilder b)
        {
            // 헤드블록 (Mechanism — 스프레더 위, 케이블 부착점)
            float headblockY = SpreaderHeight + HeadblockHeight * 0.5f + 0.2f;
            b.AddBox(2,
                center: new Vector3(0f, headblockY, 0f),
                size:   new Vector3(HeadblockLength, HeadblockHeight, HeadblockWidth));

            // 헤드블록 윗면 케이블 부착부 4개 (Frame)
            float ax = HeadblockLength * 0.42f;
            float az = HeadblockWidth  * 0.42f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * ax, headblockY + HeadblockHeight * 0.5f + 0.2f, sz * az),
                    size:   new Vector3(0.35f, 0.4f, 0.35f));
            }

            // 스프레더 본체 (Mechanism — 노란색)
            b.AddBox(2,
                center: new Vector3(0f, SpreaderHeight * 0.5f, 0f),
                size:   new Vector3(SpreaderLength, SpreaderHeight, SpreaderWidth));

            // 좌·우 카운터웨이트 박스 (Frame)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * (SpreaderLength * 0.5f - 0.4f), SpreaderHeight * 0.5f + 0.05f, 0f),
                    size:   new Vector3(0.8f, SpreaderHeight + 0.1f, SpreaderWidth * 0.9f));
            }

            // 측면 안전 줄무늬 (Stripes 4)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(4,
                    center: new Vector3(0f, SpreaderHeight * 0.5f, sz * (SpreaderWidth * 0.5f + 0.005f)),
                    size:   new Vector3(SpreaderLength * 0.95f, SpreaderHeight * 0.6f, 0.02f));
            }

            // 트위스트락 4개 (코너, 아래로 돌출 — Mechanism)
            float lockH = 0.25f;
            float hx = SpreaderLength * 0.5f - 0.2f;
            float hz = SpreaderWidth  * 0.5f - 0.2f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(2,
                    center: new Vector3(sx * hx, -lockH * 0.5f, sz * hz),
                    size:   new Vector3(0.3f, lockH, 0.3f));
            }

            // 가이드 핀 4개 (4 코너 아래로 더 길게 — Frame)
            float pinH = 0.6f;
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(1,
                    center: new Vector3(sx * (hx + 0.05f), -lockH - pinH * 0.5f, sz * (hz + 0.05f)),
                    size:   new Vector3(0.18f, pinH, 0.18f));
            }

            // 스프레더 측면 액추에이터 박스 (좌·우 신축 메커니즘 표현 — Mechanism)
            for (int sx = -1; sx <= 1; sx += 2)
            {
                b.AddBox(2,
                    center: new Vector3(sx * SpreaderLength * 0.3f, SpreaderHeight + 0.15f, 0f),
                    size:   new Vector3(1.2f, 0.3f, SpreaderWidth * 0.5f));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // 디테일 헬퍼: 트러스 다리, 휠 보기, 사다리, 캣워크 핸드레일
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 4-pillar 트러스 다리. baseCenter에서 위로 height만큼 솟음. 단면은 section × section.
        /// 4개 코너 필러 + 수평 N단 보강 + 매 단 4면 X자 사선 보강.
        /// </summary>
        static void BuildLegTruss(MeshBuilder b, int submesh,
                                  Vector3 baseCenter, float height,
                                  float section, float pillar, float brace,
                                  int horizontalRungs)
        {
            float half = section * 0.5f - pillar * 0.5f;
            // 4 코너 필러
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(submesh,
                    center: baseCenter + new Vector3(sx * half, height * 0.5f, sz * half),
                    size:   new Vector3(pillar, height, pillar));
            }

            if (horizontalRungs < 2) horizontalRungs = 2;
            float step = height / (horizontalRungs - 1);
            for (int i = 0; i < horizontalRungs; i++)
            {
                float y = i * step;
                // X 방향 수평빔 (앞/뒤 면)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    b.AddBox(submesh,
                        center: baseCenter + new Vector3(0f, y, sz * half),
                        size:   new Vector3(section, brace, brace));
                }
                // Z 방향 수평빔 (좌/우 면)
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    b.AddBox(submesh,
                        center: baseCenter + new Vector3(sx * half, y, 0f),
                        size:   new Vector3(brace, brace, section));
                }
            }

            // X자 사선 (4면 × (rungs-1)단 — 너무 빽빽하지 않게 단 사이 한 번 교차만)
            for (int i = 0; i < horizontalRungs - 1; i++)
            {
                float y0 = i * step;
                float y1 = (i + 1) * step;
                // 앞면(-Z) / 뒷면(+Z): X-Y 평면 X자
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    AddSlantBeam(b, submesh,
                        p0: baseCenter + new Vector3(-half, y0, sz * half),
                        p1: baseCenter + new Vector3( half, y1, sz * half),
                        thickness: brace);
                    AddSlantBeam(b, submesh,
                        p0: baseCenter + new Vector3( half, y0, sz * half),
                        p1: baseCenter + new Vector3(-half, y1, sz * half),
                        thickness: brace);
                }
                // 좌면(-X) / 우면(+X): Z-Y 평면 X자
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    AddSlantBeam(b, submesh,
                        p0: baseCenter + new Vector3(sx * half, y0, -half),
                        p1: baseCenter + new Vector3(sx * half, y1,  half),
                        thickness: brace);
                    AddSlantBeam(b, submesh,
                        p0: baseCenter + new Vector3(sx * half, y0,  half),
                        p1: baseCenter + new Vector3(sx * half, y1, -half),
                        thickness: brace);
                }
            }
        }

        /// <summary>
        /// 휠 보기: 다리 바닥에 부착되는 박스 + 휠 4개 (박스 단순화). X축 방향으로 길게.
        /// </summary>
        static void BuildBogie(MeshBuilder b, Vector3 center,
                               float length, float height, float width, float wheelR,
                               int wheelCount = 4)
        {
            // 보기 본체 (Frame)
            b.AddBox(1,
                center: center + new Vector3(0f, height * 0.5f, 0f),
                size:   new Vector3(length, height, width));

            // 휠 (Mechanism)
            float wheelY = wheelR;
            if (wheelCount < 2) wheelCount = 2;
            float span = length - wheelR * 2f;
            float step = span / (wheelCount - 1);
            for (int i = 0; i < wheelCount; i++)
            {
                float x = -span * 0.5f + i * step;
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    b.AddBox(2,
                        center: center + new Vector3(x, wheelY, sz * width * 0.5f),
                        size:   new Vector3(wheelR * 2f, wheelR * 2f, 0.35f));
                }
            }
        }

        /// <summary>
        /// 직사각형 사각 핸드레일 프레임. plane = "xz" (수평 면). top/bottom 가로레일 2단 + 코너 기둥.
        /// </summary>
        static void BuildHandrailXZ(MeshBuilder b, int submesh,
                                    Vector3 center, float lengthX, float lengthZ,
                                    float railHeight, float thick)
        {
            float hx = lengthX * 0.5f;
            float hz = lengthZ * 0.5f;
            // 상단 레일
            float topY = railHeight;
            b.AddBox(submesh, center: center + new Vector3(0f, topY, -hz), size: new Vector3(lengthX, thick, thick));
            b.AddBox(submesh, center: center + new Vector3(0f, topY,  hz), size: new Vector3(lengthX, thick, thick));
            b.AddBox(submesh, center: center + new Vector3(-hx, topY, 0f), size: new Vector3(thick, thick, lengthZ));
            b.AddBox(submesh, center: center + new Vector3( hx, topY, 0f), size: new Vector3(thick, thick, lengthZ));
            // 중단 레일 (안전 규정)
            float midY = railHeight * 0.55f;
            b.AddBox(submesh, center: center + new Vector3(0f, midY, -hz), size: new Vector3(lengthX, thick, thick));
            b.AddBox(submesh, center: center + new Vector3(0f, midY,  hz), size: new Vector3(lengthX, thick, thick));
            b.AddBox(submesh, center: center + new Vector3(-hx, midY, 0f), size: new Vector3(thick, thick, lengthZ));
            b.AddBox(submesh, center: center + new Vector3( hx, midY, 0f), size: new Vector3(thick, thick, lengthZ));
            // 4 코너 기둥
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                b.AddBox(submesh,
                    center: center + new Vector3(sx * hx, topY * 0.5f, sz * hz),
                    size:   new Vector3(thick, topY, thick));
            }
        }

        /// <summary>
        /// 계단 타워: 다리 옆에 직립한 박스 케이지 + 지그재그 사다리 + 핸드레일.
        /// </summary>
        static void BuildStairTower(MeshBuilder b, Vector3 baseCenter, float height,
                                    float width, float depth)
        {
            // 외곽 4-pillar (Frame)
            BuildLegTruss(b, 1, baseCenter, height, Mathf.Max(width, depth), 0.18f, 0.12f,
                          horizontalRungs: Mathf.Max(3, Mathf.RoundToInt(height / 4f) + 1));

            // 지그재그 계단 단 (Frame, 사선 박스로 단순화)
            int flights = Mathf.Max(3, Mathf.RoundToInt(height / 3.5f));
            float flightH = height / flights;
            float hw = width * 0.45f;
            for (int i = 0; i < flights; i++)
            {
                float y0 = i * flightH + 0.2f;
                float y1 = (i + 1) * flightH;
                float dir = (i % 2 == 0) ? 1f : -1f;
                AddSlantBeam(b, 1,
                    p0: baseCenter + new Vector3(-hw * dir, y0, 0f),
                    p1: baseCenter + new Vector3( hw * dir, y1, 0f),
                    thickness: 0.18f);
                // 단 디딤판 박스 (얇게)
                b.AddBox(1,
                    center: baseCenter + new Vector3(0f, (y0 + y1) * 0.5f, 0f),
                    size:   new Vector3(width * 0.9f, 0.05f, depth * 0.8f));
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // 유틸
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// 두 점을 잇는 사선 박스를 추가. (회전을 적용한 박스)
        /// </summary>
        static void AddSlantBeam(MeshBuilder b, int submesh, Vector3 p0, Vector3 p1, float thickness)
        {
            Vector3 mid    = (p0 + p1) * 0.5f;
            Vector3 dir    = p1 - p0;
            float   length = dir.magnitude;
            if (length < 1e-4f) return;
            Vector3 axis   = dir.normalized;

            // 박스 로컬: X = length, Y = thickness, Z = thickness.
            // 박스를 (length × t × t)로 만들고, X축이 axis가 되도록 회전.
            Quaternion rot = Quaternion.FromToRotation(Vector3.right, axis);

            // 8 corners (로컬)
            Vector3 h = new Vector3(length * 0.5f, thickness * 0.5f, thickness * 0.5f);
            Vector3[] corners = new Vector3[]
            {
                new Vector3(-h.x, -h.y, -h.z), new Vector3( h.x, -h.y, -h.z),
                new Vector3( h.x,  h.y, -h.z), new Vector3(-h.x,  h.y, -h.z),
                new Vector3(-h.x, -h.y,  h.z), new Vector3( h.x, -h.y,  h.z),
                new Vector3( h.x,  h.y,  h.z), new Vector3(-h.x,  h.y,  h.z),
            };
            for (int i = 0; i < corners.Length; i++)
                corners[i] = mid + rot * corners[i];

            // 6 faces, normal은 평균값으로 단순 계산 (flat shading)
            AddFace(b, submesh, corners[4], corners[5], corners[6], corners[7]); // +Z
            AddFace(b, submesh, corners[1], corners[0], corners[3], corners[2]); // -Z
            AddFace(b, submesh, corners[5], corners[1], corners[2], corners[6]); // +X 끝
            AddFace(b, submesh, corners[0], corners[4], corners[7], corners[3]); // -X 끝
            AddFace(b, submesh, corners[3], corners[7], corners[6], corners[2]); // +Y
            AddFace(b, submesh, corners[0], corners[1], corners[5], corners[4]); // -Y
        }

        static void AddFace(MeshBuilder b, int submesh, Vector3 a, Vector3 c, Vector3 d, Vector3 e)
        {
            Vector3 normal = Vector3.Cross(c - a, d - a).normalized;
            int ia = b.AddVertex(a, normal, new Vector2(0f, 0f));
            int ib = b.AddVertex(c, normal, new Vector2(1f, 0f));
            int ic = b.AddVertex(d, normal, new Vector2(1f, 1f));
            int id = b.AddVertex(e, normal, new Vector2(0f, 1f));
            b.AddQuad(submesh, ia, ib, ic, id);
        }

        static void ApplyScale(Mesh mesh, float scale)
        {
            if (Mathf.Approximately(scale, 1f)) return;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] *= scale;
            mesh.vertices = verts;
            mesh.RecalculateBounds();
        }

        // ═════════════════════════════════════════════════════════════════════
        // MeshBuilder (컨테이너 ProceduralContainerMesh와 동일 구조 — 자체 보유)
        // ═════════════════════════════════════════════════════════════════════
        public sealed class MeshBuilder
        {
            readonly List<Vector3> _verts   = new List<Vector3>(4096);
            readonly List<Vector3> _normals = new List<Vector3>(4096);
            readonly List<Vector2> _uvs     = new List<Vector2>(4096);
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
                    list = new List<int>(2048);
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
                Vector3 p000 = center + new Vector3(-h.x, -h.y, -h.z);
                Vector3 p100 = center + new Vector3( h.x, -h.y, -h.z);
                Vector3 p110 = center + new Vector3( h.x,  h.y, -h.z);
                Vector3 p010 = center + new Vector3(-h.x,  h.y, -h.z);
                Vector3 p001 = center + new Vector3(-h.x, -h.y,  h.z);
                Vector3 p101 = center + new Vector3( h.x, -h.y,  h.z);
                Vector3 p111 = center + new Vector3( h.x,  h.y,  h.z);
                Vector3 p011 = center + new Vector3(-h.x,  h.y,  h.z);

                AddFace6(submesh, p001, p101, p111, p011, Vector3.forward);
                AddFace6(submesh, p100, p000, p010, p110, Vector3.back);
                AddFace6(submesh, p101, p100, p110, p111, Vector3.right);
                AddFace6(submesh, p000, p001, p011, p010, Vector3.left);
                AddFace6(submesh, p011, p111, p110, p010, Vector3.up);
                AddFace6(submesh, p000, p100, p101, p001, Vector3.down);
            }

            void AddFace6(int submesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
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
