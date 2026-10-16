# Projects and dependencies analysis

This document provides a comprehensive overview of the projects and their dependencies in the context of upgrading to .NETCoreApp,Version=v10.0.

## Table of Contents

- [Executive Summary](#executive-Summary)
  - [Highlevel Metrics](#highlevel-metrics)
  - [Projects Compatibility](#projects-compatibility)
  - [Package Compatibility](#package-compatibility)
  - [API Compatibility](#api-compatibility)
  - [Binding Redirect Configuration](#binding-redirect-configuration)
- [Aggregate NuGet packages details](#aggregate-nuget-packages-details)
- [Top API Migration Challenges](#top-api-migration-challenges)
  - [Technologies and Features](#technologies-and-features)
  - [Most Frequent API Issues](#most-frequent-api-issues)
- [Projects Relationship Graph](#projects-relationship-graph)
- [Project Details](#project-details)

  - [FlavorText.csproj](#flavortextcsproj)
  - [SYR\SYR.csproj](#syrsyrcsproj)
  - [VEF\VEF.csproj](#vefvefcsproj)


## Executive Summary

### Highlevel Metrics

| Metric | Count | Status |
| :--- | :---: | :--- |
| Total Projects | 3 | All require upgrade |
| Total NuGet Packages | 8 | 2 need upgrade |
| Total Code Files | 18 |  |
| Total Code Files with Incidents | 3 |  |
| Total Lines of Code | 3763 |  |
| Total Number of Issues | 18 |  |
| Estimated LOC to modify | 0+ | at least 0.0% of codebase |

### Projects Compatibility

| Project | Target Framework | Difficulty | Package Issues | API Issues | Binding Issues | Est. LOC Impact | Description |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| [FlavorText.csproj](#flavortextcsproj) | net48 | 🟢 Low | 4 | 0 | 0 |  | ClassicClassLibrary, Sdk Style = False |
| [SYR\SYR.csproj](#syrsyrcsproj) | net48 | 🟢 Low | 4 | 0 | 0 |  | ClassicClassLibrary, Sdk Style = False |
| [VEF\VEF.csproj](#vefvefcsproj) | net48 | 🟢 Low | 4 | 0 | 0 |  | ClassicClassLibrary, Sdk Style = False |

### Package Compatibility

| Status | Count | Percentage |
| :--- | :---: | :---: |
| ✅ Compatible | 6 | 75.0% |
| ⚠️ Incompatible | 0 | 0.0% |
| 🔄 Upgrade Recommended | 2 | 25.0% |
| ***Total NuGet Packages*** | ***8*** | ***100%*** |

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 4424 |  |
| ***Total APIs Analyzed*** | ***4424*** |  |

## Aggregate NuGet packages details

| Package | Current Version | Suggested Version | Projects | Description |
| :--- | :---: | :---: | :--- | :--- |
| cldrjs | 0.5.1 |  | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | ✅Compatible |
| jquery-globalize | 1.4.2 |  | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | ✅Compatible |
| Lib.Harmony.Ref | 2.4.2 |  | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | ✅Compatible |
| linq2db | 5.4.1 |  | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | ✅Compatible |
| Microsoft.Bcl.AsyncInterfaces | 7.0.0 | 10.0.13 | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | NuGet package upgrade is recommended |
| System.Reflection.Emit | 4.7.0 |  | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | NuGet package functionality is included with framework reference |
| System.Runtime.CompilerServices.Unsafe | 4.5.3 | 6.1.2 | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | NuGet package upgrade is recommended |
| System.Threading.Tasks.Extensions | 4.5.4 |  | [FlavorText.csproj](#flavortextcsproj)<br/>[SYR.csproj](#syrsyrcsproj)<br/>[VEF.csproj](#vefvefcsproj) | NuGet package functionality is included with framework reference |

## Top API Migration Challenges

### Technologies and Features

| Technology | Issues | Percentage | Migration Path |
| :--- | :---: | :---: | :--- |

### Most Frequent API Issues

| API | Count | Percentage | Category |
| :--- | :---: | :---: | :--- |

## Projects Relationship Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart LR
    P1["<b>⚙️&nbsp;FlavorText.csproj</b><br/><small>net48</small>"]
    P2["<b>⚙️&nbsp;SYR.csproj</b><br/><small>net48</small>"]
    P3["<b>⚙️&nbsp;VEF.csproj</b><br/><small>net48</small>"]
    P2 --> P1
    P3 --> P1
    click P1 "#flavortextcsproj"
    click P2 "#syrsyrcsproj"
    click P3 "#vefvefcsproj"

```

## Project Details

<a id="flavortextcsproj"></a>
### FlavorText.csproj

#### Project Info

- **Current Target Framework:** net48
- **Proposed Target Framework:** net10.0
- **SDK-style**: False
- **Project Kind:** ClassicClassLibrary
- **Dependencies**: 0
- **Dependants**: 2
- **Number of Files**: 26
- **Number of Files with Incidents**: 1
- **Lines of Code**: 3470
- **Estimated LOC to modify**: 0+ (at least 0.0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph upstream["Dependants (2)"]
        P2["<b>⚙️&nbsp;SYR.csproj</b><br/><small>net48</small>"]
        P3["<b>⚙️&nbsp;VEF.csproj</b><br/><small>net48</small>"]
        click P2 "#syrsyrcsproj"
        click P3 "#vefvefcsproj"
    end
    subgraph current["FlavorText.csproj"]
        MAIN["<b>⚙️&nbsp;FlavorText.csproj</b><br/><small>net48</small>"]
        click MAIN "#flavortextcsproj"
    end
    P2 --> MAIN
    P3 --> MAIN

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 4294 |  |
| ***Total APIs Analyzed*** | ***4294*** |  |

<a id="syrsyrcsproj"></a>
### SYR\SYR.csproj

#### Project Info

- **Current Target Framework:** net48
- **Proposed Target Framework:** net10.0
- **SDK-style**: False
- **Project Kind:** ClassicClassLibrary
- **Dependencies**: 1
- **Dependants**: 0
- **Number of Files**: 14
- **Number of Files with Incidents**: 1
- **Lines of Code**: 133
- **Estimated LOC to modify**: 0+ (at least 0.0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["SYR.csproj"]
        MAIN["<b>⚙️&nbsp;SYR.csproj</b><br/><small>net48</small>"]
        click MAIN "#syrsyrcsproj"
    end
    subgraph downstream["Dependencies (1"]
        P1["<b>⚙️&nbsp;FlavorText.csproj</b><br/><small>net48</small>"]
        click P1 "#flavortextcsproj"
    end
    MAIN --> P1

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 45 |  |
| ***Total APIs Analyzed*** | ***45*** |  |

<a id="vefvefcsproj"></a>
### VEF\VEF.csproj

#### Project Info

- **Current Target Framework:** net48
- **Proposed Target Framework:** net10.0
- **SDK-style**: False
- **Project Kind:** ClassicClassLibrary
- **Dependencies**: 1
- **Dependants**: 0
- **Number of Files**: 14
- **Number of Files with Incidents**: 1
- **Lines of Code**: 160
- **Estimated LOC to modify**: 0+ (at least 0.0% of the project)

#### Dependency Graph

Legend:
📦 SDK-style project
⚙️ Classic project

```mermaid
flowchart TB
    subgraph current["VEF.csproj"]
        MAIN["<b>⚙️&nbsp;VEF.csproj</b><br/><small>net48</small>"]
        click MAIN "#vefvefcsproj"
    end
    subgraph downstream["Dependencies (1"]
        P1["<b>⚙️&nbsp;FlavorText.csproj</b><br/><small>net48</small>"]
        click P1 "#flavortextcsproj"
    end
    MAIN --> P1

```

### API Compatibility

| Category | Count | Impact |
| :--- | :---: | :--- |
| 🔴 Binary Incompatible | 0 | High - Require code changes |
| 🟡 Source Incompatible | 0 | Medium - Needs re-compilation and potential conflicting API error fixing |
| 🔵 Behavioral change | 0 | Low - Behavioral changes that may require testing at runtime |
| ✅ Compatible | 85 |  |
| ***Total APIs Analyzed*** | ***85*** |  |

