# Compliance and Quality Audit

This document captures the current compliance posture of ThreadPilot and the concrete actions required to align with requested standards.

## Scope

- Product: ThreadPilot (`net8.0-windows`, WPF desktop app)
- Baseline date: 2026-04-11
- Audit method: static repository review + architecture inspection

## Executive Summary

- The application has solid architecture foundations (MVVM, DI, structured services, logging, error handling) but lacks formal governance artifacts required for enterprise compliance.
- Most requested standards are process- and evidence-heavy and require dedicated lifecycle artifacts, traceability, and automated pipeline enforcement.
- Immediate technical risk addressed in this pass: UI re-entrancy and collection concurrency faults in fast tab switching scenarios.

## ISO/Standards Mapping

### ISO 25010 (Product Quality)

Current strengths:
- Functional suitability: rich process/power-plan management feature set
- Usability: themed UI with tray integration and status signaling
- Maintainability: modular services + DI configuration (`Services/ServiceConfiguration.cs`)

Gaps:
- No formal quality model matrix with measurable criteria
- No non-functional acceptance thresholds documented per release

Required artifacts:
- `docs/quality/iso25010-quality-model.md`
- Quality gates (performance, reliability, security, maintainability)

### ISO 12207 (Software Lifecycle)

Current strengths:
- Defined architecture and service layering in docs and code

Gaps:
- No lifecycle process assets (requirements baseline, verification plan, transition evidence)
- No formal traceability from requirement to tests and releases

Required artifacts:
- Lifecycle plan
- Requirements traceability matrix
- Release readiness checklist

### OWASP + ISO 27001 (Security)

Current strengths:
- Elevated privilege checks and guarded admin operations
- Some defensive error handling and service-level logging

Gaps:
- Missing threat model and secure coding policy document
- No automated SAST/dependency/secrets scanning pipeline
- No formal control mapping to ISO 27001 Annex A controls

Required controls:
- SAST + dependency + secrets scan in CI
- Security issue triage SLA
- Signed release and artifact integrity checks

### CI/CD + DevSecOps

Current state:
- No repository CI workflows detected

Required:
- Build, test, and security workflow on pull requests and main
- Release pipeline with immutable artifacts and provenance metadata

### ISTQB / ISO 29119 (Testing)

Current strengths:
- Integrated runtime tests in `Tests/`

Gaps:
- No standardized test specification set (plan/design/cases/procedure)
- Not integrated with `dotnet test` framework and CI coverage reporting

Required:
- Test strategy and test design docs
- Structured test execution reports in CI

### Microsoft + HLK Guidance

Current strengths:
- Windows-targeted app with manifest and platform-specific integration

Gaps:
- No formal HLK-aligned validation checklist or records
- No packaging/signing verification evidence in pipeline

Required:
- Windows compatibility checklist
- Release validation records for startup, tray, privilege modes, and resiliency

### Secure Coding (CERT / MISRA)

Applicability note:
- CERT secure coding is applicable to C#/.NET development practices.
- MISRA is primarily aimed at C/C++ in safety-critical embedded domains; use only for native interop boundaries if mandated by policy.

Required:
- Secure coding standard tailored for C# + interop
- Static analysis ruleset and coding exceptions register

## Immediate Technical Remediation Applied

- Added re-entrancy guards for fast tab switching in `MainWindow.xaml.cs`
- Hardened collection update patterns in `ViewModels/ProcessViewModel.cs` and `ViewModels/PerformanceViewModel.cs`
- Added UI exception dialog throttling in `App.xaml.cs` to prevent dialog storms
- Removed problematic gear glyph usage and normalized section labels
- Restored denser content spacing while preserving modern visual style in theme dictionaries

## Recommended Next Implementation Sprint

1. Add CI workflow: build + smoke tests + static analysis.
2. Add security workflow: dependency audit + CodeQL/SAST + secret scanning.
3. Introduce test project for `dotnet test` and keep integrated runtime tests for operational checks.
4. Publish quality and security governance docs under `docs/` with ownership and review cadence.
5. Add release checklist with HLK-aligned Windows validation evidence.

## Corrective Actions (2026-04-21)

- Replaced Docker-based changelog generation in release workflow with runner-native `git-cliff` binary installation using pinned version and checksum verification.
- Removed vendored `gitleaks-bin` artifacts from repository tracking to eliminate recurring secret-scanning false positives from upstream scanner sample content.
- Added repository guardrails in `.gitignore` to prevent recommitting scanner binaries and archives.
- Added operational documentation updates in release and security checklists to keep the remediation stable across future release cycles.

## Acceptance Criteria For Compliance Baseline

- CI required on all pull requests and main branch merges.
- Security scans block merges on high/critical findings.
- Traceability matrix links features -> tests -> release notes.
- Documented quality metrics and release thresholds are versioned.
