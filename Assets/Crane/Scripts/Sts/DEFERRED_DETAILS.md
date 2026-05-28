# STS 크레인 — 보류된 디테일 모음

`StsCraneCreator.cs`에서 한 번 만들었다가 **일시 보류**한 형상 블록들을 모아 둔다.
클린 코드를 위해 본문에서는 주석 처리된 코드를 모두 제거했고, 복구가 필요하면
아래 코드를 해당 메서드의 표시된 위치에 그대로 다시 붙여 넣으면 된다.

- 좌표/치수는 작성 당시의 상수(`Scale`, `RailH`, `GaugeZ`, `LegSpanX`, `GirderZ` 등)에 의존한다.
  이후 상수가 바뀌었다면 위치가 어긋날 수 있으니 복원 후 씬에서 확인할 것.
- 헬퍼 메서드 `Box / Rod / Strut / Ball / Cone`은 본문에 그대로 있으므로 호출만 복구하면 된다.
- 단, **#9 접근 계단**은 전용 헬퍼 `BuildStair`까지 함께 제거했으므로, 복구 시
  맨 아래 "보류된 헬퍼" 절의 `BuildStair`도 같이 되살려야 한다.

---

## 1. Portal_Brace — 포털 횡방향(Z) X-브레이스
**위치:** `BuildPortal` — 실 빔(Sill_Beam) 루프 다음, "전체 디테일 보강" 직전
**사유:** 좌우(port/star) 다리 사이 X-브레이스. 사용자 요청으로 보류.

```csharp
// 포털 횡방향(Z) X-브레이스 — 좌우(port/star) 다리 사이.
foreach (float x in legX)
{
    Strut(root, "Portal_Brace",
        new Vector3(x, RailH * 0.12f, -halfZ),
        new Vector3(x, RailH * 0.55f,  halfZ), 0.008f, CStruct);
    Strut(root, "Portal_Brace",
        new Vector3(x, RailH * 0.12f,  halfZ),
        new Vector3(x, RailH * 0.55f, -halfZ), 0.008f, CStruct);
}
```

## 2. Leg_Floodlight — 다리 상단 작업등
**위치:** `BuildPortal` — "전체 디테일 보강" 안쪽 `Leg_BasePlate` 박스 바로 다음
**사유:** 사용자 요청으로 보류.

```csharp
// 다리 상단 작업등
Box(root, "Leg_Floodlight", new Vector3(x, RailH - 0.04f, lz + s * LegSec * 1.3f),
    new Vector3(0.016f, 0.012f, 0.012f), CDark);
```

## 3. Rail_Buffer — 레일 끝 완충 버퍼
**위치:** `BuildPortal` — "베이스/부두 인터페이스 디테일" 루프 다음
**사유:** 각 주행레일 Z 양 끝(적색). 사용자 요청으로 보류.

```csharp
// 레일 끝 완충 버퍼(각 주행레일 Z 양 끝, 적색)
float railLenB = GaugeZ + 0.30f;
foreach (float x in legX)
for (int e = -1; e <= 1; e += 2)
{
    Box(root, "Rail_Buffer",
        new Vector3(x, 0.02f, e * railLenB * 0.5f),
        new Vector3(0.03f, 0.03f, 0.018f), CWarn);
}
```

## 4. AntiCollision_Sensor — 충돌방지 센서
**위치:** `BuildPortal` — Rail_Buffer 다음(메서드 끝)
**사유:** 주행방향(Z) 양끝을 향함(옆 크레인 감지). 사용자 요청으로 보류.

```csharp
// 충돌방지 센서 — 주행방향(Z) 양끝을 향함(옆 크레인 감지)
for (int s = -1; s <= 1; s += 2)
{
    Box(root, "AntiCollision_Sensor",
        new Vector3(WaterLegX, RailH * 0.5f, s * (halfZ + 0.02f)),
        new Vector3(0.014f, 0.02f, 0.014f), CDark);
}
```

## 5. Boom_EndCap / EndRib / Girder_EdgeTrim — 거더 끝단·모서리 마감
**위치:** `BuildBoomStructure` — `Boom_Rail` 박스 다음, 격자 대각 직전
**사유:** 사용자 요청으로 보류. (`x0`, `x1`, `len`, `mid`는 메서드 상단에서 정의됨)

```csharp
// 거더 끝단/모서리 마감
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
```

## 6. E_House — 보조 E-house
**위치:** `BuildBoomStructure` — 창(MH_Mullion) 루프 다음, `Cable_Tray` 직전
**사유:** 사용자 요청으로 보류. (`mhx`는 기계실 X 중심)

```csharp
// 보조 E-house
Box(boom, "E_House", new Vector3(mhx + 0.12f, 0.085f, 0f),
    new Vector3(0.07f, 0.06f, GirderZ * 1.1f), CMachine);
```

## 7. Davit — 정상 정비용 데빗(소형 지브)
**위치:** `BuildApexAndStays` — A-프레임 측면 브레이스 루프 다음, "정상 시브 네스트" 직전
**사유:** 튀어나와 보여 보류. 복구 시 끝단 후크 등 추가 권장. (`apex`, `halfZ`)

```csharp
// 정상 정비용 데빗(소형 지브)
Box(root, "Davit_Mast", new Vector3(apex.x - 0.03f, apex.y + 0.025f, halfZ * 0.4f),
    new Vector3(0.005f, 0.05f, 0.005f), CStruct);
Box(root, "Davit_Arm", new Vector3(apex.x - 0.05f, apex.y + 0.045f, halfZ * 0.4f),
    new Vector3(0.05f, 0.005f, 0.005f), CStruct);
```

## 8. Ladder_Landing — 사다리 중간 휴식 랜딩 + 난간
**위치:** `BuildAccessDetails` — 안전 케이지(Cage) 루프 다음
**사유:** 사다리 정면을 막아 보류. 복구 시 옆으로 빼서 배치 권장. (`ly0`, `ly1`, `ladderZ`)

```csharp
// 중간 휴식 랜딩 + 난간
float landY = Mathf.Lerp(ly0, ly1, 0.55f);
Box(root, "Ladder_Landing", new Vector3(LandLegX, landY, ladderZ + 0.02f),
    new Vector3(0.05f, 0.004f, 0.045f), CMachine);
Box(root, "Ladder_Landing_Rail", new Vector3(LandLegX, landY + 0.03f, ladderZ + 0.042f),
    new Vector3(0.05f, 0.004f, 0.004f), CStruct);
```

## 9. 육지쪽 접근 계단 (Stair) — `BuildStair` 사용
**위치:** `BuildAccessDetails` — #8 다음, "전력 케이블 릴" 직전
**사유:** 어둡고 급경사라 사다리처럼 보여 보류. 복구 시 완경사로 조정 권장.
**주의:** 전용 헬퍼 `BuildStair`(아래 "보류된 헬퍼")를 함께 되살려야 한다.

```csharp
// 육지쪽 접근 계단(port측)
float stairZ = -(GaugeZ * 0.5f + LegSec * 1.7f * 0.5f + 0.02f);
BuildStair(root, LandLegX - 0.04f, stairZ, 0f, RailH * 0.55f, 0.10f, 9, CMachine);
// 계단 상단 랜딩 + 난간
Box(root, "Stair_Landing", new Vector3(LandLegX + 0.065f, RailH * 0.55f, stairZ),
    new Vector3(0.05f, 0.004f, 0.05f), CMachine);
Box(root, "Landing_Rail", new Vector3(LandLegX + 0.065f, RailH * 0.55f + 0.03f, stairZ - 0.022f),
    new Vector3(0.05f, 0.004f, 0.004f), CStruct);
```

## 10. Cable_Reel — 전력 케이블 릴(드럼)
**위치:** `BuildAccessDetails` — #9 다음, "갠트리 페스툰" 직전
**사유:** 육지쪽 베이스. 사용자 요청으로 보류.

```csharp
// 전력 케이블 릴(드럼) — 육지쪽 베이스
float drumX = LandLegX - 0.07f;
Box(root, "Reel_Frame", new Vector3(drumX, 0.03f, 0f),
    new Vector3(0.05f, 0.06f, 0.07f), CStruct);
Rod(root, "Cable_Reel",
    new Vector3(drumX, 0.05f, -0.025f),
    new Vector3(drumX, 0.05f, 0.025f), 0.032f, CDark);
```

## 11. Gantry_Festoon_Cable — 갠트리 페스툰 늘어진 케이블 루프
**위치:** `BuildAccessDetails` — `Gantry_Festoon_Track` 박스 다음 (`gfZ`는 트랙과 공유, 본문에 유지)
**사유:** 사용자 요청으로 보류. (트랙 `Gantry_Festoon_Track`은 본문에 유지)

```csharp
// 늘어진 케이블 루프(port측)
for (int i = 0; i < 6; i++)
{
    float fx = Mathf.Lerp(LandLegX + 0.03f, WaterLegX - 0.03f, i / 5f);
    Box(root, "Gantry_Festoon_Cable", new Vector3(fx, 0.073f, gfZ),
        new Vector3(0.005f, 0.028f, 0.005f), CDark);
}
```

## 12. Flipper — 스프레더 트위스트락 플리퍼 가이드 판
**위치:** `BuildSpreaderVisual` — 좌/우 텔레스코픽 암 루프의 트위스트락(`c`) 생성 직후 (`sx`, `sz`, `c` 사용)
**사유:** 코너 바깥으로 튀어나와 보여 보류. (텔레스코픽 재구성으로 `c`는 암 루프 안에서 정의되며, `hl` 대신 `spreaderHalf` 기준)

```csharp
// 플리퍼 가이드 판(코너 바깥·아래로)
Strut(spreader, "Flipper",
    c + new Vector3(sx * 0.004f, -0.006f, sz * 0.004f),
    c + new Vector3(sx * 0.026f, -0.05f, sz * 0.026f), 0.016f, CMetal);
```

---

## 보류된 헬퍼 — `BuildStair`
**위치:** `StsCraneCreator` 클래스 내 디테일 지오메트리 헬퍼 영역(`BuildInclinedLadder` 부근)
**사유:** #9 접근 계단에서만 호출되어 함께 제거. #9 복구 시 같이 되살릴 것.

```csharp
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
```

---

## 보류된 에디터 메뉴

### Move STS Crane to Container
**위치:** `StsCraneCreator` — `CreateAtContainer` 다음 (`Create()` 직전)
**사유:** 사용자 요청으로 보류. 기존 STS_Crane을 재생성 없이 씬의 컨테이너 위치로 옮기는 메뉴.

```csharp
// 기존 크레인을 재생성 없이 컨테이너 위치로 이동(스프레더 정지 위치가 컨테이너 위에 오게)
[MenuItem("Container/Move STS Crane to Container")]
public static void MoveToContainer()
{
    var crane = GameObject.Find(RootName);
    if (crane == null)
    {
        UnityEngine.Debug.LogWarning("[STS] STS_Crane이 없습니다 — 먼저 'Create STS Crane'으로 생성하세요.");
        return;
    }
    Vector3 anchor = FindContainerAnchor();
    Undo.RecordObject(crane.transform, "Move STS Crane to Container");
    crane.transform.position = anchor - new Vector3(TrolleyRestX, 0f, 0f);
    Selection.activeGameObject = crane;
    var sv = SceneView.lastActiveSceneView;
    if (sv != null) sv.FrameSelected();
    UnityEngine.Debug.Log($"[STS] 크레인을 컨테이너 위치로 이동: anchor={anchor}");
}
```
