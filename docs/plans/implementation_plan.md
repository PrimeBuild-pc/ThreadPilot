# 🌊 Fluent Design Migration Plan (Safe & Incremental)

This revised plan adopts `Wpf.Ui` to natively implement Windows 11 Fluent Design while strictly avoiding a destructive rewrite. We will use a staged deprecation model to gracefully phase out legacy custom styles without causing UI collapses or broken resource references.

## 1. Staged Deprecation Plan

**Rule:** We will *not* delete all custom styles at once.

| Category | Action | Reasoning |
| :--- | :--- | :--- |
| **Control Templates** (TabControl, TabItem, ComboBox, Button, TextBox) | **Remove Immediately** (in Phase 1) | `Wpf.Ui`'s global `ControlsDictionary` implicitly and instantly styles these when custom styles are cleared. Keeping custom styles causes collisions. |
| **Structural Styles** (Grid, Border layout snaps) | **Preserve Temporarily** | These don't conflict with Fluent Design and prevent immediate layout shifts. |
| **Color Brushes** (SurfaceBrush, AccentBrush, etc.) | **Preserve via Aliasing** | Explicitly mapping our legacy brush names to `Wpf.Ui` keys ensures un-migrated Views do not crash or turn transparent. |
| **State Brushes** (SuccessBackgroundBrush, etc.) | **Preserve Permanently** | `Wpf.Ui` focuses on structural elements; maintaining our custom success/error semantic tokens is necessary for valid status bars. |

## 2. Brush Migration Strategy & Mapping

To prevent broken `DynamicResource` bindings during the transition, we will transform `Themes/FluentDark.xaml` into an **Alias Dictionary**.

| Current Custom Brush | `Wpf.Ui` Equivalent Target |
| :--- | :--- |
| `AppBackgroundBrush` | `ApplicationBackgroundBrush` |
| `SurfaceBrush` | `CardBackgroundFillColorDefaultBrush` (or `ControlFillColorDefaultBrush`) |
| `SurfaceAltBrush` | `CardBackgroundFillColorSecondaryBrush` |
| `TextPrimaryBrush` | `TextFillColorPrimaryBrush` |
| `TextSecondaryBrush` | `TextFillColorSecondaryBrush` |
| `TextDisabledBrush` | `TextFillColorDisabledBrush` |
| `AccentBrush` | `SystemAccentColorPrimaryBrush` |
| `AccentAltBrush` | `SystemAccentColorSecondaryBrush` |
| `BorderBrush` | `CardStrokeColorDefaultBrush` |
| `InputBorderBrush` | `ControlStrokeColorDefaultBrush` |

*Fallback strategy:* If a specific legacy brush requires an exact opacity/hex that Fluent lacks, we will retain the exact `<SolidColorBrush>` entry rather than mapping it.

## 3. Control Migration Scope (Phase 1)

Only the following core interactive elements will be yielded to Fluent styling during the infrastructure phase:

- **TabControl / TabItem**: Will switch to Fluent tabs. Wpf.Ui native tabs use underline selection effects instead of filled block backgrounds.
- **ComboBox**: Will transition to modern, rounded popup cards.
- **Buttons**: Will adopt Fluent background states and rounded corners (`CornerRadius="4"` default).
- **ListView / DataGrid**: Will adopt Fluent bordered-row styles natively.

**Known Structural Risks in Scope**:
- Fluent `DataGrid` rows have larger innate vertical padding. This may cause scrollbars to appear earlier in constrained `Heights`.
- Fluent `ComboBox` dropdowns render outside the parent visual tree constraints (Popup), which usually resolves Z-index issues perfectly but may alter perceived alignment if wrapped tightly in a canvas.

## 4. Risk Mitigation Strategies

1. **Preventing UI Regressions**: We will explicitly map `System.Windows.Controls` styles prior to deleting any custom XAML headers. Un-migrated tabs will still visually resolve their `Background="{DynamicResource SurfaceBrush}"` to a Fluent native shade.
2. **Preventing Broken Bindings**: No C# `DataContext` or `Binding` logic will be altered during XAML structural updates.
3. **Preventing Layout Collapses**: As elements transition to Fluent geometries, we will systematically strip fixed `Height="XXX"` or `MaxLength` restraints globally from elements, instead leveraging `Grid.RowDefinitions="Auto,*"` to allow fluent elements to define their own organic sizing needs.

## 5. Incremental Execution Plan

We divide Phase 2 into strict atomic units to test visually before advancing.

### Step 1: Framework Base & Aliasing (The Infrastructure)
- `dotnet add package Wpf.Ui`
- Update `App.xaml` with core `.MergedDictionaries`.
- Strip `Themes/FluentDark.xaml` of all `<Style TargetType="Button">`, `<Style TargetType="ComboBox">`, etc.
- Rewrite remaining `SolidColorBrush` definitions in `FluentDark.xaml` to `DynamicResource` aliases to Wpf.Ui.

### Step 2: MainWindow Integration
- Transform the root `<Window>` into `<ui:FluentWindow>` to unlock Mica backdrop effects and custom titlebars.
- Replace the legacy `<TabControl>` with Wpf.Ui native navigation (or clean native TabControl). Verify it renders precisely.

### Step 3: View Anchor Migration (LogViewer)
- Target `LogViewerView.xaml`. 
- Cleanse it of rigid layout blocks. Implement responsive `<Grid>` expansions to distribute columns evenly.
- Swap explicit legacy Brush calls (e.g. `Foreground="{DynamicResource TextPrimaryBrush}"`) for direct `Wpf.Ui` brush keys.

### Step 4: Iterative Rollout
Target the remaining views (`ProcessManagementView`, `MasksView`, `PowerPlanView`) sequentially, verifying their respective grids, slider components, and checkboxes adopt Fluent visuals flawlessly.
