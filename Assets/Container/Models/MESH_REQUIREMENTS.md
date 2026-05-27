# 컨테이너 3D 메시 요구사항 (1차년도)

이 폴더에 외부 조달한 20ft ISO 컨테이너 메시(.fbx, .obj 등)를 넣습니다.

## 1. 규격 (ISO 668 표준 20ft Dry, Type 22G1)

| 항목 | 값 |
|---|---|
| 외부 길이 (Length) | 6.058 m (20'0") |
| 외부 폭 (Width) | 2.438 m (8'0") |
| 외부 높이 (Height) | 2.591 m (8'6") |
| Unity Scale | 1 unit = 1 m (그대로 사용) |

> 참조 이미지: 디자인팀 회의에서 공유한 흰색 20ft, 22G1 타입

## 2. 폴리곤·텍스처

| 항목 | 기준 |
|---|---|
| 폴리곤 수 | 8,000 ~ 15,000 tri (Single LOD) |
| 텍스처 해상도 | 2048×2048 또는 4096×4096 |
| 텍스처 셋 (PBR) | Albedo, Normal, Roughness, Metallic |
| UV | 0~1 범위 내, 겹침 없음 |
| Pivot | 컨테이너 바닥 중심 (X=0, Y=0, Z=0) |
| Forward | +Z 방향이 도어가 있는 쪽 (또는 -Z, 일관성만 유지) |

## 3. 머티리얼 / 셰이더 슬롯

런타임에 색상이 변경되어야 하므로 **본체(Body)는 별도 머티리얼**로 분리해야 합니다.

| 서브메시 | 용도 | 색상 변경 |
|---|---|---|
| Body (Corrugated panels) | 본체 외판 | ✅ 랜덤 색상 적용 |
| Door | 도어 (선택적으로 본체와 같은 색) | ✅ |
| Frame / Corner Castings | 코너 캐스팅, 프레임 | ❌ 검정/회색 고정 |
| Floor (내부 바닥) | 내부 바닥 (있다면) | ❌ |

> 본체 머티리얼은 URP/Lit 또는 Standard 셰이더 사용, `_BaseColor` 프로퍼티가 노출되어야 함.

## 4. 텍스처 베이크 금지 항목

다음은 **텍스처에 구워 넣으면 안 됩니다** (런타임에 동적으로 변경되어야 함):

- ❌ 컨테이너 번호 (예: MRBU 200125 [8])
- ❌ ISO 타입 코드 (예: 22G1)
- ❌ 선사 로고
- ❌ 화물 정보 (MAX. GR. / TARE / NET / CU. CAP.) — 1차년도는 선택, 2차년도부터 동적 처리

대신 메시 측면·도어에 **빈 패널 영역(Decal Anchor)** 을 표시해두면, 그 위치에 TMP_Text 또는 Decal로 정보를 덧붙입니다.

## 5. 조달 옵션

| 옵션 | 비용 | 비고 |
|---|---|---|
| Unity Asset Store | $10~$50 | 가장 빠름, PBR/LOD 포함 모델 다수 |
| Sketchfab (CC0/Standard) | 무료~$30 | 폴리곤 정리 필요할 수 있음 |
| TurboSquid / CGTrader | $20~$100 | 고품질, FBX 변환 필요 가능 |
| 자체 모델링 (Blender) | 인건비 | 3~5일 소요, 완전 커스터마이즈 |

## 6. 메시 입수 후 후속 작업

1. 이 폴더(`Assets/Container/Models/`)에 메시 파일 배치
2. `Assets/Container/Prefabs/Container_20ft.prefab` 생성
   - 루트에 `ContainerInstance.cs` 부착
   - 메시는 자식 오브젝트로 배치
   - 본체 Renderer → `bodyRenderers` 슬롯에 할당
   - 측면/도어 TMP_Text → `idLabels` 슬롯에 할당
3. `ContainerSpawner.cs`를 빈 GameObject에 부착하여 씬에 배치
4. Spawner의 `containerPrefab` 슬롯에 프리팹 연결, `palette`에 ContainerColorPalette 에셋 연결
5. 플레이 모드로 검증

## 7-A. 절차적 메시 (외부 조달 전 임시 베이스, 2026-05-21 추가)

외부 메시 조달 결정 전, 디자인 검토용으로 절차적 메시 생성 파이프라인을 구축했습니다.
요구사항(섹션 1~3)에 부합하도록 설계되어 그대로 1차년도 베이스로 사용 가능합니다.

### 7-A.1 실행 절차

1. Unity 에디터에서 프로젝트를 엽니다.
2. 컴파일 에러가 없는지 확인합니다 (Console 비어있어야 함).
3. 상단 메뉴 **`Container` → `Build 20ft Procedural Prefab`** 을 실행합니다.
4. 다음 에셋이 생성/갱신됩니다:
   - `Assets/Container/Models/Container_20ft_Procedural.asset`
   - `Assets/Container/Materials/Container_Body.mat`
   - `Assets/Container/Materials/Container_Door.mat`
   - `Assets/Container/Materials/Container_Frame.mat`
   - `Assets/Container/Materials/Container_Castings.mat`
   - `Assets/Container/Container_Palette_Default.asset`
   - `Assets/Container/Prefabs/Container_20ft.prefab`
5. 완료 대화상자가 표시되며, 프리팹이 자동으로 Project 창에 ping 됩니다.

### 7-A.2 단일 컨테이너 시각 확인

- 프리팹을 빈 씬에 드래그합니다.
- Scene 뷰에서 카메라를 컨테이너 주위로 돌려 다음을 확인:
  - 측면/전면/도어의 수직 주름판이 살아 있는지
  - 코너 캐스팅 8개, 코너 포스트 4개, top/bottom rail 8개가 모두 보이는지
  - 도어 락바 4개(좌2 + 우2)와 힌지 8개(좌4 + 우4)가 보이는지
  - 지붕에 미세한 캠버(가운데 솟음)가 있는지

### 7-A.3 다수 스폰 + 색상/번호 검증

1. 씬에 빈 GameObject 생성 → `ContainerSpawner` 컴포넌트 부착
2. Inspector에서 다음 슬롯 연결:
   - `Container Prefab` ← `Container_20ft.prefab`
   - `Palette` ← `Container_Palette_Default.asset`
3. `Spawn Count`(기본 8), `Columns Per Row`(기본 4) 조정
4. Play 모드 진입 → 각 컨테이너가 12종 선사 컬러 중 하나로 랜덤하게 칠해지고, 측면·도어에 ISO 6346 번호가 표시되는지 확인

### 7-A.4 메시 재생성 (코드 수정 후)

`ProceduralContainerMesh.cs` 파라미터(주름 깊이, 캐스팅 크기 등)를 조정한 뒤:
- 메뉴 **`Container` → `Regenerate Mesh Only`** 실행 → 기존 머티리얼/프리팹은 유지, 메시 자산만 갱신

### 7-A.5 색상 변경이 본체·도어에만 적용되는 이유

`ContainerInstance.cs`의 `bodyRenderers` 슬롯이 머티리얼 인덱스 0(Body), 1(Door)만 가리키도록 빌더가 자동 설정합니다.
Frame(2), Castings(3) 머티리얼은 `MaterialPropertyBlock` 영향을 받지 않아 다크 그레이/블랙 고정입니다.

### 7-A.6 절차적 메시의 한계

| 항목 | 절차적 메시 | 외부 조달 시 기대 수준 |
|---|---|---|
| 폴리곤 디테일 | 단순 박스/평면 조합 (수천 tri) | 8k~15k tri (요구사항) |
| 코너 캐스팅 ISO 1161 구멍 | 단순 박스, 구멍 없음 | 정확한 4면 구멍 표현 |
| 표면 디테일(마모/리벳/용접) | 없음 (PBR 텍스처 없음) | Normal/Roughness 맵 포함 |
| 도어 힌지 메커니즘 | 단순 박스 | 실제 힌지 형상 |

→ 외부 조달 결정 시 즉시 갈아끼울 수 있도록 슬롯 구조는 동일하게 유지했습니다.

---

## 7-B. 1차년도에서 제외된 항목

연구개발계획서에는 포함되어 있으나 1차년도 디자인팀 작업에서 제외한 항목:

- ❌ LOD (다단계 디테일) → 2차년도로 이연
- ❌ 도어 개폐 애니메이션 → 2차년도로 이연
- ❌ 40ft 컨테이너 (42G1) → 20ft 완료 후 검토
- ❌ 상태(정상/주의/이상) 표시 셰이더 → 클라이언트팀(한지호) 협업으로 별도 진행
