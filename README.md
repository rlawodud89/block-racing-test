# Block Racing Test

Block Racing Server를 대상으로 부하 및 기능 테스트를 수행하기 위한 **테스트 클라이언트 프로젝트**입니다.

여러 개의 테스트 클라이언트를 생성하여 실제 클라이언트의 네트워크 요청을 재현하고, 다수의 클라이언트가 동시에 서버에 연결하거나 매칭 및 게임을 진행하는 상황을 테스트할 수 있습니다.

## Project Structure

```text
block-racing-test
│
├── block-racing-test
│   ├── Program.cs
│   ├── TestClient.cs
│   ├── TestConfig.cs
│   └── Tests
│       ├── ConnectionLoadTest.cs
│       ├── MatchmakingTest.cs
│       ├── GameLoadTest.cs
│       └── GamePlayTest.cs
│
├── Common
│   └── block-racing-common
│
├── block-racing-test.sln
├── .gitmodules
└── README.md
```

### Program

테스트 클라이언트를 실행하고 테스트 시나리오를 선택합니다.

현재 다음 테스트를 제공합니다.

```text
1. Connection Load Test
2. Matchmaking Test
3. Game Load Test
4. Game Play Test
```

선택한 테스트의 `RunAsync()`를 호출하여 해당 시나리오를 실행합니다.

### TestConfig

테스트 실행에 필요한 설정을 관리합니다.

테스트 클라이언트의 수, 서버 연결 정보 및 테스트에 필요한 각종 값을 변경하여 동일한 테스트를 다양한 조건에서 수행할 수 있습니다.

### TestClient

서버와의 실제 네트워크 통신을 담당하는 테스트 클라이언트입니다.

각 `TestClient`는 독립적으로 서버에 연결하고 패킷을 송수신하며, 서버에서 전달되는 패킷을 처리합니다.

여러 개의 `TestClient`를 생성하여 실제 다수의 클라이언트가 서버에 연결한 상황을 재현합니다.

### Tests

각 테스트 시나리오를 실행하기 위한 클래스를 포함합니다.

각 클래스는 테스트 목적에 맞는 수의 `TestClient`를 생성하고, 연결부터 패킷 송수신 및 결과 확인, 연결 종료까지 테스트 전체 흐름을 관리합니다.

| Test                 | Description                      |
| -------------------- | -------------------------------- |
| `ConnectionLoadTest` | 다수의 클라이언트를 동시에 연결하여 연결 부하를 재현    |
| `MatchmakingTest`    | 다수의 클라이언트가 동시에 매칭을 요청하는 상황을 재현   |
| `GameLoadTest`       | 여러 클라이언트가 게임에 진입하는 상황을 재현        |
| `GamePlayTest`       | 여러 클라이언트가 실제 게임 플레이를 수행하는 상황을 재현 |

### Common

Block Racing Server와 테스트 클라이언트가 공유하는 공통 프로젝트입니다.

서버와 동일한 네트워크 패킷 및 게임 관련 데이터 구조를 사용하기 위해 Git Submodule로 참조합니다.

---

## Test Completion

각 테스트는 테스트 단계에 필요한 서버의 응답을 기준으로 진행 결과를 확인합니다.

각 단계에서는 지정된 Timeout 시간 내에 필요한 응답을 수신해야 하며, 시간 내에 응답을 수신하지 못한 클라이언트는 실패로 처리됩니다.

테스트는 여러 클라이언트의 결과를 동시에 기다리며, 해당 단계에 참여한 모든 클라이언트의 결과가 확인된 후 다음 단계로 진행합니다.

예를 들어 Matchmaking Test에서는 모든 클라이언트의 매칭 결과를 확인한 후 테스트 결과를 집계합니다.

```text
Client 1 ──► Match Request ──► Match Result
Client 2 ──► Match Request ──► Match Result
Client 3 ──► Match Request ──► Timeout
Client 4 ──► Match Request ──► Match Result
                         │
                         ▼
                  All Results Checked
                         │
                         ▼
                     Test Result
```

GamePlayTest에서는 게임 시작 후 클라이언트가 반복적으로 게임 Input을 전송하고, `GAME END`를 수신하면 Input 전송을 중단합니다.

`GAME END`를 지정된 시간 내에 수신하지 못한 클라이언트는 실패로 처리됩니다.

```text
Game Start
    │
    ▼
Send Game Input
    │
    ├── Input
    ├── Input
    ├── Input
    └── ...
    │
    ▼
Wait for GAME END
    │
    ├── GAME END ─────► Success
    │
    └── Timeout ──────► Fail
```

모든 클라이언트의 결과가 확인되면 해당 테스트 단계가 종료되고 다음 단계 또는 테스트 종료 과정으로 진행합니다.

---

## Test Scenarios

### 1. Connection Load Test

다수의 테스트 클라이언트를 생성하여 서버에 동시에 연결합니다.

```text
              ┌── Client 1 ──┐
              ├── Client 2 ──┤
              ├── Client 3 ──┤
Test Client ──┼── Client ... ┼──► Block Racing Server
              ├─ Client N-1 ─┤
              └── Client N ──┘
```

여러 클라이언트의 동시 연결과 연결 종료를 수행하여 서버의 연결 처리 과정을 테스트합니다.

---

### 2. Matchmaking Test

다수의 테스트 클라이언트가 서버에 연결한 뒤 동시에 매칭을 요청합니다.

```text
Clients
   │
   ├── Match Request
   ├── Match Request
   ├── Match Request
   └── ...
          │
          ▼
     Matchmaking
          │
          ▼
        Room
```

여러 클라이언트가 동시에 매칭을 요청하고, 매칭이 완료된 클라이언트들이 게임 시작 단계로 진행되는 전체 흐름을 테스트합니다.

---

### 3. Game Load Test

여러 클라이언트를 게임에 진입시켜 동시에 게임이 실행되는 상황을 재현합니다.

```text
Match
  │
  ▼
Room
  │
  ├── Player 1
  ├── Player 2
  ├── Player 3
  └── ...
        │
        ▼
    Game Start
```

다수의 플레이어가 동시에 게임에 참여하는 상황에서 서버의 게임 시작 과정을 테스트합니다.

---

### 4. Game Play Test

게임이 시작된 이후 여러 테스트 클라이언트가 실제 게임 플레이를 수행하는 상황을 재현합니다.

```text
Client 1 ─┐
Client 2 ─┤
Client 3 ─┼──► Game Server
Client 4 ─┤        │
   ...   ─┘        │
                   ▼
               Game State
```

테스트 클라이언트는 서버에 연결하고 로그인한 후 매칭을 요청하여 게임에 진입합니다.

게임이 시작되면 각 클라이언트가 일정한 간격으로 무작위 게임 Input을 전송하며 실제 게임 플레이 상황을 재현합니다.

`GAME END`를 수신하면 Input 전송을 중단하고 테스트를 종료합니다.

---

## Test Flow

각 테스트는 독립적으로 실행할 수 있으며, 다음과 같은 주요 단계로 서버의 동작을 검증합니다.

```text
Connection
    │
    ▼
Matchmaking
    │
    ▼
Game Load
    │
    ▼
Game Play
```

각 테스트에서 생성되는 클라이언트 수와 설정 값을 변경하여 다양한 동시 접속 및 게임 진행 상황을 재현할 수 있습니다.

## Related Repositories

* [Block Racing](https://github.com/rlawodud89/block-racing) — Block Racing 전체 프로젝트
* [Block Racing Server](https://github.com/rlawodud89/block-racing-server) — 게임 서버
* [Block Racing Common](https://github.com/rlawodud89/block-racing-common) — 서버와 클라이언트가 공유하는 공통 코드
