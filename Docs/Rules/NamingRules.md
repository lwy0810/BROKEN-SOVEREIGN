# 코드 이름 규칙

C# 스크립트를 만들거나 고칠 때 따르는 규칙입니다. 규칙은 이 문서에만 적습니다.

## 이름 짓기

| 대상 | 형식 | 예시 |
|---|---|---|
| 클래스 | PascalCase | `PlayerMovement` |
| 인터페이스 | `I` + PascalCase | `IDamageable` |
| 메서드 | PascalCase | `CollectItem()` |
| 프로퍼티 | PascalCase | `Score` |
| private 필드 | `_` + camelCase | `_moveSpeed` |
| 지역 변수·매개변수 | camelCase | `itemCount` |
| 상수 | ALL_CAPS | `MAX_HEALTH` |
| enum 타입·값 | PascalCase | `GameState.Playing` |

## 네임스페이스

- 게임 스크립트는 `BrokenSovereign.<폴더 이름>` 네임스페이스에 둡니다. 예: `Scripts/Combat/Health.cs` → `BrokenSovereign.Combat`
- 하위 폴더는 이름을 이어 붙입니다. 예: `Scripts/Core/Events/` → `BrokenSovereign.Core.Events`
- 테스트 스크립트는 `BrokenSovereign.Tests` 네임스페이스에 둡니다.
- 네임스페이스를 쓰는 이유: 게임 클래스 이름이 Unity나 패키지의 클래스 이름과 겹치는 것을 막기 위해서입니다.

## 이벤트

- 이벤트 클래스(구조체)는 이름 끝에 `Event`를 붙이고, 일어난 일을 과거형으로 씁니다. 예: `EnemyKilledEvent`, `SkillCastEvent`
- 이벤트를 받는 메서드는 `On` + 이벤트 이름(`Event` 제외)으로 짓습니다. 예: `OnEnemyKilled(EnemyKilledEvent e)`
- 이벤트를 구독한 컴포넌트는 `OnEnable`에서 구독하고 `OnDisable`에서 해제합니다.

## ScriptableObject

- 정의·설정 데이터를 담는 ScriptableObject 클래스는 이름 끝에 `Definition`을 붙입니다. 예: `SkillDefinition`
- `[CreateAssetMenu]`의 메뉴 경로는 `BrokenSovereign/<폴더 이름>/<클래스 이름>`으로 씁니다.

```csharp
[CreateAssetMenu(menuName = "BrokenSovereign/Skill/SkillDefinition")]
public class SkillDefinition : ScriptableObject
{
    [SerializeField] private string _skillId;
}
```

## 테스트

- 테스트 클래스는 대상 클래스 이름 + `Tests`로 짓습니다. 예: `BreakthroughConditionTests`
- 테스트 메서드는 `메서드_조건_기대결과` 형식으로 짓습니다. 이 형식에서만 `_`를 씁니다.

```csharp
[Test]
public void CanBreakthrough_MainStatBelowThreshold_ReturnsFalse()
```

## 파일

- 파일 하나에 클래스 하나를 둡니다.
- 파일 이름과 클래스 이름을 똑같이 짓습니다. 다르면 유니티가 클래스를 잘못 고르거나 컴포넌트로 쓰지 못하는 경우가 생깁니다.
- 이름에 공백과 한글을 쓰지 않습니다.

## 인스펙터에 보이는 값

- 인스펙터에서 조절할 값은 `public` 필드 대신 `[SerializeField] private` 필드로 둡니다.

```csharp
public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _moveSpeed = 5f;
}
```

## 컴포넌트 가져오기

- `GetComponent`는 `Update`에서 매 프레임 부르지 않습니다. `Awake`에서 한 번 가져와 필드에 담아 둡니다.

```csharp
private Rigidbody _rigidbody;

private void Awake()
{
    _rigidbody = GetComponent<Rigidbody>();
}
```

## 로그

- `Debug.Log` 메시지 앞에 클래스 이름을 붙입니다. 콘솔에서 어느 스크립트가 남긴 로그인지 바로 찾기 위해서입니다.

```csharp
Debug.Log($"PlayerMovement: 아이템 획득 {itemCount}개");
```
