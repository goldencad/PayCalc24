# PayCalc24 — SVG Icon System & Media Asset Contract

## 1. Objective
TS24 Media owns visual icon artwork. Dev owns semantic icon contracts and rendering infrastructure. Media must be able to replace/update SVG artwork without requiring feature code or payroll logic changes.

## 2. Semantic IconKey
Feature code references semantic keys:
```text
NAV_DASHBOARD
NAV_PAYROLL
NAV_ATTENDANCE
NAV_PERFORMANCE
NAV_FUNDS
NAV_SIMULATION
NAV_REPORTS
NAV_INTEGRATION
NAV_SETTINGS

ACTION_ADD
ACTION_EDIT
ACTION_DELETE
ACTION_IMPORT
ACTION_VALIDATE
ACTION_CALCULATE
ACTION_APPROVE
ACTION_REJECT
ACTION_LOCK
ACTION_UNLOCK
ACTION_EXPORT

STATUS_INFO
STATUS_SUCCESS
STATUS_WARNING
STATUS_ERROR
STATUS_PENDING
STATUS_LOCKED
```

Do not use filenames as the application contract.

## 3. Asset pack layout
Recommended:
```text
Assets/
└─ IconPacks/
   └─ default/
      ├─ manifest.json
      └─ svg/
         ├─ dashboard.svg
         ├─ payroll.svg
         ├─ calculate.svg
         └─ ...
```

Example manifest:
```json
{
  "packId": "ts24-paycalc24-default",
  "version": "1.0.0",
  "icons": {
    "NAV_DASHBOARD": "svg/dashboard.svg",
    "NAV_PAYROLL": "svg/payroll.svg",
    "ACTION_CALCULATE": "svg/calculate.svg",
    "STATUS_WARNING": "svg/warning.svg"
  }
}
```

## 4. Runtime contracts
Suggested abstractions:
```text
IconKey
IconDescriptor
IIconProvider
IconRegistry
IconPackLoader
```

Feature XAML/ViewModels request an IconKey. The provider resolves the current pack.

## 5. Shared UI components
Feature views should use shared components such as:
```text
PcIcon
PcIconButton
PcNavigationItem
PcStatusBadge
PcToolbarButton
PcEmptyState
```

Example conceptual usage:
```text
PcIconButton IconKey="ACTION_CALCULATE"
```

The shared component handles SVG loading, sizing, foreground/tint, disabled state and fallback.

## 6. Media delivery rules
Media supplies:
- valid SVG;
- agreed viewBox;
- no external linked resources;
- no scripts;
- optimized paths;
- semantic filename/manifest mapping;
- preview sheet if useful;
- icon pack version/changelog.

For themeable icons, prefer artwork that can inherit/tint foreground. Brand logos or illustrations may remain multicolor.

## 7. Theme separation
Icons are separate from theme tokens.

Theme layer owns:
- typography;
- spacing;
- radius;
- surfaces;
- borders;
- foregrounds;
- status colors;
- light/dark behavior.

Feature XAML does not scatter brand constants.

## 8. Pack replacement
A new Media pack should require only:
1. validate manifest;
2. validate all required IconKeys;
3. replace/register pack;
4. run UI asset tests;
5. visually review;
6. publish.

No feature ViewModel or payroll engine code changes should be required.

## 9. Missing/invalid assets
Missing SVG must:
- render a safe fallback placeholder;
- log a diagnosable warning with IconKey/pack version;
- never crash a payroll workflow.

Invalid/unsafe SVG content is rejected during pack validation/build.

## 10. Cross-platform requirements
Test SVG rendering at minimum on:
- Windows normal/high DPI;
- macOS ARM64 Retina;
- Linux supported desktop environment.

Icons must remain crisp at common UI sizes and should not depend on OS-specific fonts.

## 11. Versioning
Payroll business history does not need to snapshot icon artwork. UI releases should record application/theme/icon-pack versions for diagnostics. Media can upgrade icons independently from formula/policy versions.

## 12. Definition of Done
The SVG system is ready when:
- feature code contains no direct SVG paths;
- changing manifest/artwork changes icons without feature code edits;
- light/dark rendering works for themeable icons;
- missing icon fallback works;
- Windows/macOS/Linux smoke tests pass.
