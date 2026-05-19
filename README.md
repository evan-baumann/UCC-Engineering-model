# UCC Engineering Model

Parametric rocket geometry for UCC Rocketry, built on [PicoGK](https://github.com/leap71/PicoGK) and the LEAP71 ShapeKernel. The model generates **nosecones**, **fins** (with stability-driven sizing), and optional engine hardware as voxel previews and STL exports.

**Audience:** structural / design leads who need to change dimensions without touching the geometry engine.

---

## Quick start

### Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- Windows (PicoGK viewer; primary dev platform)
- Git submodules initialized:

```bash
git clone https://github.com/evan-baumann/UCC-Engineering-model.git
cd UCC-Engineering-model
git submodule update --init --recursive
```

### Run

```bash
dotnet run
```

A 3D viewer opens. The **terminal** prints the active rocket config and, when fins are enabled, optimized fin dimensions (semi-span, root chord, static margin, mass, etc.).

---

## Where to edit

### Routine work → **`Rockets.cs` only**

All vehicle-specific numbers (diameter, nose, fins, OpenRocket baselines) and viewer/export toggles are set in **`Rockets.cs`**. That is the only file the structural team should need.

| Goal | Location in `Rockets.cs` |
|------|-------------------------|
| Switch vehicle (Cerberus, Orpheus, …) | §2 — `Active = …` |
| Change body OD, length, Mach | §1 — your rocket definition (e.g. `Cerberus = new(...)`) |
| Nose length, shoulder, wall | §1 — same definition |
| Fin sizing & OpenRocket baselines | §1 — same definition |
| Show/hide nose, fins, body; export STL | §3 — toggles |

Each rocket in §1 is written as `new RocketParameters(...)` — you edit the **argument values** in `Rockets.cs`, not a separate config file.

### Do not edit for normal work

| File | Role |
|------|------|
| **`RocketParameters.cs`** | Defines the *shape* of a rocket record (field names, defaults, team-wide constants like tab depth). **Not** where per-vehicle numbers live. |
| `Program.cs`, `Scene.cs` | Runtime / viewer plumbing |
| `Nosecone/SmartNosecone.cs`, `Fins/SmartFinModule.cs` | Geometry and MDO logic |

`RocketParameters` is the **type**; `Rockets.cs` **governs** which rocket is active and what values it carries.

---

## Switching rockets

In `Rockets.cs` §2:

```csharp
public static readonly RocketParameters Active = Orpheus;  // or Cerberus, Eclipse, …
```

Each rocket is defined in §1 by copying an existing `new RocketParameters(...)` block and renaming the field (e.g. `Cerberus`, `Orpheus`).

---

## Nosecone parameters

Set these on your rocket in **`Rockets.cs` §1**. They feed `Nosecone/SmartNosecone.cs` via `Rockets.Active`.

| Parameter | Unit | What it controls |
|-----------|------|------------------|
| `fOuterDiameterMm` | mm | Body (and nose base) outer diameter — **master scale** for the whole vehicle |
| `fMaxMach` | — | Design Mach; selects nose **shape family** (see below) |
| `fNoseLengthMm` | mm | Nose length tip → base (when not using auto length) |
| `bAutoNoseLength` | bool | `true` = ignore `fNoseLengthMm` and use aero fineness × OD |
| `fWallThicknessMm` | mm | Nose shell wall thickness |
| `fNoseShoulderLengthMm` | mm | Aft cylindrical shoulder into body tube. `0` = auto (1× OD). `-1` = no shoulder |
| `fNoseShoulderDiameterMm` | mm | Shoulder OD. `0` = auto (~0.92× body OD) |
| `bNoseShoulderCapped` | bool | `true` = closed bulkhead disk at shoulder base |
| `fNoseBluffnessRatio` | — | Tip bluntness for hypersonic regime only (default `0.15`) |
| `fVoxelSizeMm` | mm | Mesh resolution (smaller = finer STL, slower). Default `0.3` on record; runtime in `Program.cs` is `0.25` |

### Nose shape vs Mach (`fMaxMach`)

| Mach range | Shape |
|------------|--------|
| M &lt; 0.8 | Elliptical (subsonic) |
| 0.8 ≤ M &lt; 5 | Von Kármán (supersonic) |
| M ≥ 5 | Spherically blunted cone (hypersonic) |

### Auto nose length (`bAutoNoseLength = true`)

Length = **fineness × OD**, with fineness from Mach:

| Regime | Fineness × OD |
|--------|----------------|
| Subsonic (M &lt; 0.8) | 4× |
| Supersonic (0.8 ≤ M &lt; 5) | 6× |
| Hypersonic (M ≥ 5) | 5× |

Keep your real `fNoseLengthMm` in source for documentation; set `bAutoNoseLength` only when comparing to the aero-optimal length.

### Nose viewer & export (`Rockets.cs` §3)

```csharp
public static readonly bool ShowNosecone = true;        // preview in viewer (optional)
public static readonly bool ExportNoseconeStl = true;   // writes exports/{RocketName}_nose.stl
```

Export works even when `ShowNosecone` is `false` (faster if you only need the STL).

---

## Fin parameters

Fin geometry is built by **`Fins/SmartFinModule.cs`** using the active rocket’s values from **`Rockets.cs` §1** (passed in via `Rockets.Active`).

### Structural / planform (edit in `Rockets.cs`)

All fin **lengths scale with body diameter** (rocketry convention: ratios vs **Ø**, not radius).

| Parameter | Typical | What it controls |
|-----------|---------|------------------|
| `fMinRootChordRatio` | `1.42` | Root chord = **Ø × ratio** (e.g. Cerberus Ø113 → ~160 mm) |
| `fMinSemiSpanRatio` | `0.75` | Minimum exposed fin semi-span = **Ø × ratio** (MDO floor) |
| `fTargetStaticMargin` | `2.0` | Target SM in **calibres**: (CP − CG) / Ø; MDO **increases span** until met |
| `fFinMaterialDensityKgM3` | `1200`–`1850` | Solid density (e.g. PETG ≈ 1200, filled nylon ≈ 1850) for mass estimate |

**Cerberus example** (explicit in repo):

```csharp
fMinRootChordRatio: 1.42f,
fMinSemiSpanRatio:  0.75f,
```

Other rockets inherit defaults (`1.42` / `0.75`) unless you override them in their block.

### OpenRocket alignment (stability MDO)

Import these from OpenRocket (no fins, consistent mass basis) so the fin optimizer has the right baseline:

| Parameter | OpenRocket source |
|-----------|-------------------|
| `fBodyNoFinsMassKg` | Vehicle mass **without** fin set (note wet vs dry in comments) |
| `fBodyNoFinsCgMm` | CG from **nose tip**, mm |
| `fBodyNoFinsCpMm` | CP from **nose tip**, mm |
| `fBodyNoFinsCNa` | Normal-force coefficient slope of body (no fins) |

The MDO loop grows **semi-span** from the floor until `fTargetStaticMargin` is reached (or hits an internal cap).

### Fixed fin geometry (developers only — not in `Rockets.cs`)

These are team-wide defaults baked into the engine. Changing them requires editing source outside `Rockets.cs` (coordinate with simulation / structures owners):

| Item | Value | Where defined |
|------|-------|----------------|
| Tip chord | 52% of root chord | `SmartFinModule.cs` |
| TTW tab radial depth | 15 mm | `RocketParameters.cs` (`FinModuleTtwTabRadialDepthMm`) |
| Tab length / root chord | 0.55 | `RocketParameters.cs` (`FinModuleTtwTabChordFraction`) |
| Min peak / edge thickness | 6 mm / 2 mm | `RocketParameters.cs` |
| Fin count | 4 | `SmartFinModule.cs` |

Airfoil cross-section (rounded vs hex wedge) follows **`fMaxMach`** you set in `Rockets.cs`; Mach thresholds (`0.65` / `1.30`) are fixed in `RocketParameters.cs`.

### Fin viewer & export (`Rockets.cs` §3)

```csharp
public static readonly bool ShowFins = true;
public static readonly bool ExportFinStl = true;   // exports/{RocketName}_fin_assembly.stl
```

### Reading the terminal (fin run)

After `OptimizeFinDimensions()`, look for a line like:

```text
SmartFinModule (...): semi-span = … mm (floor … = 0.75×Ø), root chord = … mm (1.42×Ø), tip chord = … mm, m_fin×4 ≈ … kg, SM = … cal, ...
```

| Log field | Meaning |
|-----------|---------|
| `semi-span` | Optimized exposed span (OpenRocket **height** candidate) |
| `floor` | Minimum span = `fMinSemiSpanRatio × Ø` |
| `root chord` | `fMinRootChordRatio × Ø` |
| `tip chord` | 0.52 × root chord |
| `SM` | Achieved static margin in calibres |
| `m_fin×4` | Estimated mass of all four fins |

Copy planform values into OpenRocket’s **trapezoidal fin set** (mm, 4 fins). Use mass override if you want the model’s estimate.

---

## Shared body parameters

These affect nose, fins, and body context together:

| Parameter | Meaning |
|-----------|---------|
| `fOuterDiameterMm` | Airframe OD at body tube |
| `fBodyLengthMm` | Cylindrical body length **only** (excludes nose) |
| `fMaxMach` | Design Mach (nose regime + fin airfoil + sweep) |

---

## Viewer toggles (summary)

In **`Rockets.cs` §3**:

| Toggle | Default (check repo) | Effect |
|--------|----------------------|--------|
| `ShowNosecone` | varies | Build/preview nose |
| `ShowFins` | `true` | Run fin MDO + preview |
| `ShowBodyTube` | `false` | Cylindrical body for context |
| `ShowPintleInjector` | `false` | Engine injector (separate subsystem) |
| `ExportNoseconeStl` | `false` | Nose STL → `exports/{name}_nose.stl` |
| `ExportFinStl` | `false` | Fin assembly STL → `exports/` |
| `FinPreviewTransparent` | `false` | Semi-transparent fin preview |

---

## Project layout

```text
Rockets.cs              ← EDIT HERE: all per-rocket values + toggles
RocketParameters.cs     ← Type definition + shared constants (do not edit for Cerberus/Orpheus numbers)
Program.cs              ← Entry point (runtime voxel size)
Scene.cs                ← Assembles nose + body preview
Nosecone/SmartNosecone.cs
Fins/SmartFinModule.cs
Engines/PintleInjector.cs
exports/                ← Generated STLs (gitignored by default)
```

LEAP71 libraries are git **submodules** under `LEAP71_ShapeKernel/`, etc.

---

## Workflow for structural review

1. Set `Active` to your vehicle in `Rockets.cs` §2.
2. Update §1 dimensions from CAD / OpenRocket (OD, body length, nose, shoulder).
3. Paste OpenRocket CG/CP/mass/CNa into the fin baseline fields.
4. Set `fMinRootChordRatio` / `fMinSemiSpanRatio` to team standards (Cerberus: `1.42`, `0.75`).
5. Set `fTargetStaticMargin` to program requirement (often 2–2.6 cal).
6. `dotnet run` — check terminal SM and masses; enable `ExportNoseconeStl` / `ExportFinStl` for CAD handoff.
7. Compare STL or viewer geometry to OpenRocket trapezoidal fin entries (root, tip, height, sweep).

---

## Questions / deeper changes

- **New rocket:** copy an existing rocket definition in `Rockets.cs` §1, rename it, edit the `new RocketParameters(...)` values.
- **Add a new parameter every rocket should have:** that requires changing `RocketParameters.cs` (the record) *and then* setting values in `Rockets.cs` — simulation owner task.
- **Change nose mathematics** (profile equations): `Nosecone/SmartNosecone.cs` — coordinate with aerodynamics.
- **Change fin mesh rules** (tabs, airfoil, MDO): `Fins/SmartFinModule.cs` — coordinate with structures + GNC.

For issues or access, contact the UCC Rocketry simulation / structures owners of this repository.
