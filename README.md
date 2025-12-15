# automotive-mes-server

C# ASP.NET Core와 Mitsubishi PLC를 활용한 자동차 제조 전공정(Front-End Process) MES 구축 프로젝트입니다.

---

## 목차

- [automotive-mes-server](#automotive-mes-server)
  - [목차](#목차)
  - [1. 프로젝트 소개](#1-프로젝트-소개)
  - [2. 주요 기능](#2-주요-기능)
    - [설비 제어 및 공정 자동화](#설비-제어-및-공정-자동화)
    - [생산 관리 및 품질 분석](#생산-관리-및-품질-분석)
    - [시스템 모니터링](#시스템-모니터링)
  - [3. 시스템 아키텍처](#3-시스템-아키텍처)
    - [주요 구성 요소](#주요-구성-요소)
  - [4. 기술 스택](#4-기술-스택)
    - [주요 NuGet 패키지](#주요-nuget-패키지)
  - [5. 시작하기](#5-시작하기)
    - [사전 요구사항](#사전-요구사항)
    - [설치 및 실행](#설치-및-실행)
  - [6. 데이터베이스](#6-데이터베이스)
    - [테이블 이원화 전략 (Dual-Table Strategy)](#테이블-이원화-전략-dual-table-strategy)
    - [`order` (주문 관리)](#order-주문-관리)
    - [`order_history` (주문 관리) \[백업\]](#order_history-주문-관리-백업)
    - [`production` (생산 관리)](#production-생산-관리)
    - [`production_history` (생산 관리) \[백업\]](#production_history-생산-관리-백업)
    - [`vision_upper` (상부 비전 카메라)](#vision_upper-상부-비전-카메라)
    - [`vision_lower` (하부 비전 카메라)](#vision_lower-하부-비전-카메라)
  - [7. PLC 입출력 맵](#7-plc-입출력-맵)
    - [입력 (X)](#입력-x)
    - [출력 (Y)](#출력-y)
  - [8. 프로젝트 팀 구성 및 역할](#8-프로젝트-팀-구성-및-역할)
    - [나의 프로젝트 기여도](#나의-프로젝트-기여도)
    - [개발 회고록 (Dev Log)](#개발-회고록-dev-log)



## 1. 프로젝트 소개

본 프로젝트는 자동차 차체(Body) 조립을 위한 **스마트 팩토리 전공정(Front-End Process) 관리 MES 시스템**입니다.

**ASP.NET 기반의 서버**를 주축으로, 설비(PLC), 로봇(Dobot), 비전, 그리고 관리자(WPF Client)를 유기적으로 연결하였습니다.
단순히 설비를 제어하는 것을 넘어, 생산 주문(Order) 생성부터 로봇 조립, 비전 검사를 통한 품질 판정까지
**제조 공정의 전체 라이프사이클을 자동화하고 데이터를 시각화**하는 데 중점을 두었습니다.

이 시스템은 장비 간의 통신 프로토콜을 통합하여,
**제어(OT) 영역과 정보(IT) 영역이 실시간으로 데이터를 주고받는 스마트 제조 환경**을 구현하였습니다.

## 2. 주요 기능

### 설비 제어 및 공정 자동화
*   **실시간 PLC 제어**: MX Component를 통해 설비(PLC)의 구동 상태를 모니터링하고 제어 명령을 수행합니다.
*   **Dobot 로봇 연계**: Dobot 로봇을 통해 자동차 조립 공정과 도색 작업을 수행합니다.

### 생산 관리 및 품질 분석
*   **생산 오더 관리 (MOM)**: MySQL 데이터베이스를 기반으로 작업 지시를 생성하고, 공정 상황을 추적 관리합니다.
*   **비전 기반 품질 관리 (QA)**: 비전 카메라가 판독한 양품/불량 데이터를 수신하여 DB에 저장하고, 불량 발생 시 즉각적인 알람 및 리젝트(Reject) 로직을 수행합니다.

### 시스템 모니터링
*   **통합 대시보드**: WPF 클라이언트를 통해 전체 공정의 흐름과 생산 실적을 시각적으로 모니터링할 수 있습니다.
*   **시스템 로그 추적**: Serilog를 도입하여 서버와 장비 간의 통신 로그 및 예외 상황을 기록, 시스템 안정성을 확보했습니다.

## 3. 시스템 아키텍처

본 프로젝트는 2대의 PC를 기반으로 구성됩니다. 메인 서버 PC에서는 MES 서버가 동작하며,
클라이언트 PC에서는 비전 카메라, Dobot 로봇 제어 프로그램 및 WPF 클라이언트가 실행됩니다.
서버와 클라이언트 PC는 동일한 공유기 네트워크에 연결되어 통신합니다.

```mermaid
graph TD
    subgraph ClientPC["Client PC"]
        WPF["WPF Client<br/>(Dashboard)"]
        Vision["Vision Client<br/>(Camera Control)"]
        Dobot["Dobot Client<br/>(Robot Control)"]
    end

    subgraph ServerPC["Server PC"]
        Server["ASP.NET Core Server<br/>(Business Logic)"]
        DB[("MySQL Database")]
    end

    subgraph FactoryDevice["Factory Device"]
        MX["MX Component"]
        PLC["Mitsubishi PLC"]
    end

    WPF <-->|"TCP/IP"| Server
    Vision -->|"TCP/IP (Data Only)"| Server
    
    Server <-->|"Query/Save"| DB
    Server <-->|"PlcConnector"| MX
    MX <-->|"PLC Communication"| PLC
    
    Dobot <-->|"pymcprotocol"| PLC
    Vision <-->|"pymcprotocol"| PLC
```

### 주요 구성 요소

1.  **PLC-연계 제어**
    *   **MES Server**: **MX Component**를 미들웨어로 사용하여 Mitsubishi PLC와 통신합니다. 설비 데이터를 주기적으로 Polling(읽기)하거나 제어 명령(쓰기)을 수행합니다.
    *   **Device Clients (Vision, Dobot)**: **pymcprotocol** 라이브러리를 통해 PLC와 직접 통신합니다.
        *   **Vision Client**: PLC로부터 검사 시작 신호를 직접 읽어(Read) 촬영을 진행하며, 도색 판정 결과를 PLC 메모리에 기록(Write)합니다.
        *   **Dobot Client**: PLC로부터 작업 신호를 수신하여 로봇 동작을 수행하고, 완료 신호를 전송합니다.

2.  **서버-클라이언트 통신 (TCP/IP)**
    *   **Vision Client**: 도색 판정 결과를 서버로 전송합니다. 이 과정은 데이터 로깅을 위한 단방향 통신으로, **서버로부터 별도의 응답(Response)을 기다리지 않고** 데이터를 전송합니다.
    *   **WPF Client**: 작업자를 위한 대시보드로, 생산 현황 모니터링 및 공정 관리 기능을 제공하기 위해 서버와 양방향 통신합니다.

3.  **데이터 관리 및 로깅**
    *   **OrderService**: 수집된 데이터를 비즈니스 로직에 맞춰 가공합니다.
    *   **MySQL**: 생산 이력, 불량, 주문 정보 등 모든 데이터를 저장하고 관리합니다.
    *   **Serilog**: 서버의 운영 상태 및 예외 상황을 체계적으로 로깅하여 유지보수 효율을 높입니다.

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
- MySQL
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

기본적으로 모든 컬럼은 `NOT NULL` 제약조건을 가지며, 예외적으로 생산 종료 시점이 나중에 결정되는 `production` 및 `production_history` 테이블의 `end_date` 컬럼만 `NULL`을 허용합니다.

### 테이블 이원화 전략 (Dual-Table Strategy)

본 프로젝트는 데이터 무결성과 성능 최적화를 위해 **이원화된 테이블 구조**를 채택했습니다.
이러한 설계는 **CIMON SCADA 시스템의 태그(Tag) 구조적 한계**를 극복하고 성능을 최적화하기 위해 도입되었습니다. SCADA는 미리 생성된 정적 태그를 사용하므로, 데이터가 계속 누적되는 테이블을 직접 연동할 경우 성능 저하가 발생할 수 있습니다.

*   **운영 테이블 (Active DB)**: `order`, `production`
    *   현재 진행 중인 최신 데이터(최대 30건)만 유지합니다.
    *   데이터 양을 일정하게 유지하여 SCADA 시스템의 부하를 최소화합니다.
*   **기록 테이블 (History DB)**: `order_history`, `production_history`
    *   운영 테이블에서 30건을 초과한 데이터는 이곳으로 **이관(Migration)**됩니다.
    *   데이터의 영구 보존과 사후 분석을 위한 용도입니다.

이 구조를 통해 **실시간 모니터링의 효율성**과 **데이터의 영구적 보존**이라는 두 가지 목표를 동시에 달성합니다.

### `order` (주문 관리)

| 컬럼명 | 설명 | 데이터 타입 | 제약조건 |
|---|---|---|---|
| `order_id` | 주문 ID | `INT(11)` | `PRIMARY KEY` |
| `model_code` | 모델명 | `VARCHAR(50)` | |
| `order_quantity`| 주문 수량 | `INT(11)` | |
| `order_date` | 주문 날짜 | `DATETIME` | |
| `order_status` | 주문 상태 | `VARCHAR(50)` | |

### `order_history` (주문 관리) [백업]

| 컬럼명 | 설명 | 데이터 타입 | 제약조건 |
|---|---|---|---|
| `backup_id` | 백업 ID | `INT(11)` | `PRIMARY KEY` & `AUTO_INCREMENT` |
| `order_id` | 주문 ID | `INT(11)` | |
| `model_code` | 모델명 | `VARCHAR(50)` | |
| `order_quantity`| 주문 수량 | `INT(11)` | |
| `order_date` | 주문 날짜 | `DATETIME` | |
| `order_status` | 주문 상태 | `VARCHAR(50)` | |
| `backed_date` | 백업 날짜 | `DATETIME` | |

### `production` (생산 관리)

| 컬럼명 | 설명 | 데이터 타입 | 제약조건 |
|---|---|---|---|
| `production_id` | 생산 ID | `INT(11)` | `PRIMARY KEY` |
| `model_code` | 모델명 | `VARCHAR(50)` | |
| `upper_quantity`| 상부 생산 수량 | `INT(11)` | |
| `lower_quantity`| 하부 생산 수량 | `INT(11)` | |
| `good_quantity` | 양품 수량 | `INT(11)` | |
| `bad_quantity` | 불량 수량 | `INT(11)` | |
| `start_date` | 생산 시작 날짜 | `DATETIME` | |
| `end_date` | 생산 종료 날짜 | `DATETIME` | `NULL 허용` |

### `production_history` (생산 관리) [백업]

| 컬럼명 | 설명 | 데이터 타입 | 제약조건 |
|---|---|---|---|
| `backup_id` | 백업 ID | `INT(11)` | `PRIMARY KEY` & `AUTO_INCREMENT` |
| `production_id` | 생산 ID | `INT(11)` | |
| `model_code` | 모델명 | `VARCHAR(50)` | |
| `upper_quantity`| 상부 생산 수량 | `INT(11)` | |
| `lower_quantity`| 하부 생산 수량 | `INT(11)` | |
| `good_quantity` | 양품 수량 | `INT(11)` | |
| `bad_quantity` | 불량 수량 | `INT(11)` | |
| `start_date` | 생산 시작 날짜 | `DATETIME` | |
| `end_date` | 생산 종료 날짜 | `DATETIME` | `NULL 허용` |
| `backed_date` | 백업 날짜 | `DATETIME` | |

### `vision_upper` (상부 비전 카메라)

| 컬럼명 | 설명 | 데이터 타입 | 제약조건 |
|---|---|---|---|
| `vision_id` | 비전 ID | `INT(11)` | `PRIMARY KEY` & `AUTO_INCREMENT` |
| `production_id` | 생산 ID | `INT(11)` | |
| `model_code` | 모델명 | `VARCHAR(50)` | |
| `result` | 결과 | `VARCHAR(50)` | |
| `measured_at` | 측정 시각 | `DATETIME` | |

### `vision_lower` (하부 비전 카메라)

| 컬럼명 | 설명 | 데이터 타입 | 제약조건 |
|---|---|---|---|
| `vision_id` | 비전 ID | `INT(11)` | `PRIMARY KEY` & `AUTO_INCREMENT` |
| `production_id` | 생산 ID | `INT(11)` | |
| `model_code` | 모델명 | `VARCHAR(50)` | |
| `result` | 결과 | `VARCHAR(50)` | |
| `measured_at` | 측정 시각 | `DATETIME` | |

## 7. PLC 입출력 맵

### 입력 (X)

| 접점 | 설명 |
|---|---|
| X0 | 하부공급감지센서 |
| X1 | 하부비전감지센서 |
| X2 | 하부로봇감지센서 |
| X3 | 하부종단센서 |
| X4 | 상부공급감지센서 |
| X5 | 상부로봇감지센서 |
| X6 | 상부비전감지센서 |
| X7 | 상부종단센서 |
| X8 | 하부스토퍼 하강센서 |
| X10 | 상부공급실린더 전진센서 |
| X11 | 상부공급실린더 후진센서 |
| X12 | 하부배출실린더 전진센서 |
| X13 | 하부배출실린더 후진센서 |
| X14 | 하부공급실린더 전진센서 |
| X15 | 하부공급실린더 후진센서 |
| X40 | 자동모드 스위치 |
| X41 | 비상정지 B접점 |

### 출력 (Y)

| 접점 | 설명 |
|---|---|
| Y30 | 하부1차 컨베이어 출력 |
| Y31 | 하부2차 컨베이어 출력 |
| Y32 | 상부1차 컨베이어 출력 |
| Y33 | 상부2차 컨베이어 출력 |
| Y34 | 하부공급실린더 출력 |
| Y35 | 하부배출실린더 출력 |
| Y36 | 상부공급실린더 출력 |
| Y37 | 하부스토퍼 출력(B접점) |
| Y50 | PLC전원램프(하얀색) |
| Y51 | 작업동작램프(초록색) |
| Y52 | 수동동작램프(노랑색) |
| Y53 | 비상정지램프(빨간색) |

## 8. 프로젝트 팀 구성 및 역할

총 5명의 팀원이 각자의 강점을 살려 하드웨어 설계부터 소프트웨어 개발까지 역할을 분담했습니다.

| 이름 | 포지션 | 담당 업무 |
| :--- | :--- | :--- |
| **최윤호** | **MES Server & PLC & HMI & SCADA** | **ASP.NET 서버 구축, PLC 제어, 시스템 통합 (본인)** |
| 강선혁 | Team Leader | PM, SolidWorks 기구 설계, 3D 프린팅 및 설비 조립 |
| 김현아 | Client & Robot | Python(Dobot, Vision) 제어, WPF 대시보드 클라이언트 개발 |
| 신우재 | HW Support | Dobot 티칭, 하드웨어 조립 및 트러블 슈팅 |
| 최승규 | Electrical Design | EPLAN 전기 도면 설계 |

### 나의 프로젝트 기여도

저는 본 프로젝트에서 **OT(제어)와 IT(서버)를 잇는 시스템 통합**을 주도했습니다.
하단부의 PLC 제어부터 상단부의 MES 서버 구축까지 **데이터 파이프라인의 전 과정**을 담당했습니다.

*   **MES Backend Server 개발**
    *   C# ASP.NET Core 기반의 RESTful API 서버 구축
    *   전체 시스템 아키텍처 설계 및 데이터베이스(MySQL) 모델링
*   **PLC 제어 및 시스템 연동**
    *   Mitsubishi PLC 래더 로직 작성 및 공정 제어
    *   MX Component를 활용한 `PLC <--> Server` 간 미들웨어 구현
*   **HMI / SCADA 구축**
    *   M2I HMI 작화 및 현장 제어 패널 구성
    *   CIMON SCADA를 활용한 공정 모니터링 시스템 구축

### 개발 회고록 (Dev Log)

이 프로젝트를 진행하며 마주친 기술적 난관과 해결 과정을 블로그에 기록했습니다.

*   [MES 구축 프로젝트 1주차: 최종 프로젝트를 시작하며](https://velog.io/@yunho21/%EB%A1%9C%EB%B4%87%ED%99%9C%EC%9A%A921%EC%A3%BC%EC%B0%A8-MES-%EA%B5%AC%EC%B6%95-1%EC%A3%BC%EC%B0%A8-%ED%9A%8C%EA%B3%A0%EB%A1%9D)
*   [MES 구축 프로젝트 2주차: 공정 설계와 PLC 통신 테스트](https://velog.io/@yunho21/%EB%A1%9C%EB%B4%87%ED%99%9C%EC%9A%A922%EC%A3%BC%EC%B0%A8-MES-%EA%B5%AC%EC%B6%95-2%EC%A3%BC%EC%B0%A8-%ED%9A%8C%EA%B3%A0%EB%A1%9D)
*   [MES 구축 프로젝트 3주차: HMI 연동과 C# 제어의 시작](https://velog.io/@yunho21/%EB%A1%9C%EB%B4%87%ED%99%9C%EC%9A%A923%EC%A3%BC%EC%B0%A8-MES-%EA%B5%AC%EC%B6%95-3%EC%A3%BC%EC%B0%A8-%ED%9A%8C%EA%B3%A0%EB%A1%9D)
*   [MES 구축 프로젝트 4주차: MES 서버의 기초 공사](https://velog.io/@yunho21/%EB%A1%9C%EB%B4%87%ED%99%9C%EC%9A%A924%EC%A3%BC%EC%B0%A8-MES-%EA%B5%AC%EC%B6%95-4%EC%A3%BC%EC%B0%A8-%ED%9A%8C%EA%B3%A0%EB%A1%9D)
*   [MES 구축 프로젝트 5주차: DB 구축과 설비의 완성](https://velog.io/@yunho21/%EB%A1%9C%EB%B4%87%ED%99%9C%EC%9A%A925%EC%A3%BC%EC%B0%A8-MES-%EA%B5%AC%EC%B6%95-5%EC%A3%BC%EC%B0%A8-%ED%9A%8C%EA%B3%A0%EB%A1%9D)
*   [MES 구축 프로젝트 6주차: 설비 로직 작성과 코드 품질 개선](https://velog.io/@yunho21/%EB%A1%9C%EB%B4%87%ED%99%9C%EC%9A%A926%EC%A3%BC%EC%B0%A8-MES-%EA%B5%AC%EC%B6%95-6%EC%A3%BC%EC%B0%A8-%ED%9A%8C%EA%B3%A0%EB%A1%9D)
*   [MES 구축 프로젝트 7주차: SCADA 관제 구현과 트러블 슈팅](https://velog.io/@yunho21/%EB%A1%9C%EB%B4%87%ED%99%9C%EC%9A%A927%EC%A3%BC%EC%B0%A8-MES-%EA%B5%AC%EC%B6%95-7%EC%A3%BC%EC%B0%A8-%ED%9A%8C%EA%B3%A0%EB%A1%9D)