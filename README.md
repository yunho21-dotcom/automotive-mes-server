# automotive-mes-server

C# ASP.NET Core와 Mitsubishi PLC를 활용한 자동차 제조 전공정(Front-End Process) MES 서버 구축 프로젝트입니다.

---

## 목차

- [1. 프로젝트 소개](#1-프로젝트-소개)
- [2. 주요 기능](#2-주요-기능)
- [3. 시스템 아키텍처](#3-시스템-아키텍처)
- [4. 기술 스택](#4-기술-스택)
- [5. 시작하기](#5-시작하기)
  - [사전 요구사항](#사전-요구사항)
  - [설치 및 실행](#설치-및-실행)
- [6. 데이터베이스](#6-데이터베이스)

## 1. 프로젝트 소개

본 프로젝트는 자동차 차체(Body)가 완성되기까지의 **전공정(Front-End Process)**을 관리하는 MES(Manufacturing Execution System) 서버를 구축하는 것을 목표로 합니다.

현재는 생산 오더 관리 및 PLC 통신에 중점을 두고 있으며, 최종적으로는 ASP.NET 서버를 통해 PLC가 자동차 제조 전공정 전체를 원활히 수행할 수 있도록 지원하는 것을 목적으로 합니다.

### 향후 확장 계획
- **비전 검사 통합**: 비전 카메라 클라이언트로부터 자동차 바퀴의 불량 여부 데이터를 TCP/IP 통신으로 수신하여 품질 관리를 자동화합니다.
- **로봇 조립 자동화**: Dobot 로봇 제어 클라이언트와 양방향 TCP/IP 통신을 통해 자동차 조립 공정을 제어하고 상태를 모니터링합니다.

## 2. 주요 기능

- **실시간 설비 제어 및 모니터링**: MX Component를 통해 Mitsubishi PLC와 통신하여 설비의 상태를 읽고, 제어 신호를 전송합니다.
- **생산 오더 관리**: 데이터베이스에 저장된 주문 정보를 바탕으로 생산 오더를 관리합니다.

### 구현 예정 기능
- **품질 데이터 수집**: 비전 카메라 클라이언트로부터 전달받은 부품의 불량 여부 데이터를 수집하고 통계를 관리합니다.
- **로봇 공정 제어**: Dobot 로봇의 조립 공정 시작/종료를 제어하고, 작업 상태를 실시간으로 추적합니다.

## 3. 시스템 아키텍처

본 프로젝트는 2대의 PC를 기반으로 구성됩니다. 하나의 PC에서는 MES 서버가 동작하며, 다른 PC에서는 비전 카메라 및 Dobot 로봇 클라이언트가 실행됩니다. 두 PC는 동일한 공유기 네트워크에 연결되어 TCP/IP 프로토콜로 통신합니다.

```
+----------------+      +---------------------+      +-------------------+      +----------------+
|  Web Client    |      |                     |      |  MX Component     | <=>  | Mitsubishi PLC |
| (구현 예정)    | ---► |                     |      | (PLC Communication) |      |  (Field Device)|
+----------------+      |                     |      +-------------------+      +----------------+
                        |                     |
+----------------+      |    ASP.NET Core     |
| Vision Client  | -----►|       Server        |
| (TCP/IP)       |      | (Business Logic,    |
+----------------+      |    TCP/IP Server)   |      +-------------------+
                        |                     | <=>  |       MySQL       |
+----------------+      |                     |      |      (DB)         |
| Dobot Client   | ◄---►|                     |      +-------------------+
| (TCP/IP)       |      |                     |
+----------------+      +---------------------+
```
1.  **PLC 통신**: `PlcConnector`가 **MX Component**를 통해 주기적으로 PLC의 데이터를 읽고 쓰며 설비를 제어합니다.
2.  **외부 클라이언트 통신**:
    - **Vision Client**: 자동차 바퀴의 불량 정보를 서버로 전송합니다 (단방향).
    - **Dobot Client**: 서버와 조립 명령 및 상태 정보를 주고받습니다 (양방향).
3.  **데이터 관리**: `OrderService`는 수집된 데이터를 비즈니스 로직에 따라 가공하며, 모든 데이터는 **MySQL** 데이터베이스와 양방향으로 통신하며 저장/조회됩니다.

## 4. 기술 스택

- **Backend**: C# (ASP.NET Core 8.0)
- **Database**: MySQL
- **PLC Communication**: Mitsubishi MX Component
- **IDE**: Visual Studio

### 주요 NuGet 패키지
| 패키지명 | 버전 | 설명 |
|---|---|---|
| `Serilog.AspNetCore` | 9.0.0 | 애플리케이션 로깅을 위한 라이브러리 |
| `Serilog.Sinks.File` | 7.0.0 | 로그를 파일에 저장하기 위한 Serilog 싱크 |
| `Serilog.Sinks.Console`| 6.1.1 | 로그를 콘솔에 출력하기 위한 Serilog 싱크 |
| `Serilog.Sinks.Async` | 2.1.0 | 로그를 비동기 방식으로 처리하기 위한 Serilog 싱크 |
| `System.Data.Odbc` | 8.0.0 | ODBC 데이터 소스 연결을 위한 라이브러리 |

## 5. 시작하기

### 사전 요구사항

- .NET 8.0 SDK
- Visual Studio
- MySQL Server
- Mitsubishi MX Component

### 설치 및 실행

1.  리포지토리를 클론합니다.
    ```shell
    git clone https://github.com/your-username/automotive-mes-server.git
    ```
2.  Visual Studio에서 `MES.Server.sln` 파일을 엽니다.
3.  `appsettings.json` 파일에 자신의 DB 연결 문자열 및 기타 설정을 입력합니다.
    ```json
    {
      "ConnectionStrings": {
        "DefaultConnection": "Server=localhost;Database=mes_db;Uid=root;Pwd=your_password;"
      }
    }
    ```
4.  솔루션을 빌드하고, `F5` 키를 눌러 프로젝트를 실행합니다.

## 6. 데이터베이스

데이터베이스의 모든 테이블 컬럼은 `NOT NULL` 제약조건을 가집니다.

### 1. 주문 (Orders)
고객의 주문 정보를 관리합니다.

| 컬럼명 | 데이터 타입 | 제약조건 | 설명 |
|---|---|---|---|
| `주문ID` | `INT` | `Primary Key` | 주문 고유 식별자 |
| `주문수량` | `INT` | | 주문된 제품의 총 수량 |

### 2. 생산 (Production)
실제 생산 공정의 정보를 관리합니다.

| 컬럼명 | 데이터 타입 | 제약조건 | 설명 |
|---|---|---|---|
| `생산코드` | `INT` | `Primary Key` | 생산 작업 고유 식별자 |
| `생산수량` | `INT` | | 현재까지 생산된 제품의 수량 |
