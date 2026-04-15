# ADR-001: WPF-UI Library Choice

- Status: Accepted
- Date: 2025-10-20

## Context
ThreadPilot needs a modern Windows 11 look and feel while keeping full WPF compatibility, low migration friction, and strong control over native desktop workflows.

## Decision
Use WPF-UI as the primary UI component library.

## Rationale
- Native WPF integration without moving to WinUI or MAUI.
- Fluent-style controls aligned with Windows 11 design language.
- Lower migration and maintenance cost compared to mixed custom control stacks.
- Keeps MVVM and XAML architecture intact.

## Alternatives Considered
- MahApps.Metro: stable ecosystem, but visual style does not align as closely with current Fluent goals.
- HandyControl: broad control set, but less aligned with planned design language.
- Pure custom controls: highest flexibility, highest maintenance burden.

## Consequences
- Positive: consistent UI direction and faster feature delivery.
- Trade-off: dependency on WPF-UI package updates and theme API changes.
