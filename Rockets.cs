// =============================================================================
//  Tintreach — Rockets
//  -----------------------------------------------------------------------------
//  EDIT THIS FILE to add a rocket, switch the active one, or toggle renders.
//  Nothing else in the project should ever need editing for routine work.
//
//  ┌──────────────────────────────────────────────────────────────────────┐
//  │  How do I …                                                          │
//  ├──────────────────────────────────────────────────────────────────────┤
//  │  … add a new rocket?                                                 │
//  │       1. Copy any block in §1 below.                                 │
//  │       2. Rename the field (e.g. `Hyperion`).                         │
//  │       3. Edit the numbers to match your design.                      │
//  │       4. Optionally set it as Active in §2.                          │
//  │                                                                      │
//  │  … switch which rocket is rendered?                                  │
//  │       Edit the single line in §2:                                    │
//  │           public static readonly RocketParameters Active = …;        │
//  │                                                                      │
//  │  · preview fins semi-transparent: FinPreviewTransparent             │
//  │                                                                      │
//  │  · pintle injector STL        → ExportPintleInjectorStl (§3)       │
//  │                                                                      │
//  │  … toggle body / nose / fins / pintle injector in the viewer?       │
//  │       §3: ShowBodyTube, ShowNosecone, ShowFins, ShowPintleInjector.   │
//  │                                                                      │
//  │  … change render options (body tube on/off, etc.)?                   │
//  │       Edit the toggles in §3.                                        │
//  │                                                                      │
//  │  … change the geometry engine itself?                                │
//  │       Don't (unless you mean it). The maths lives in                 │
//  │       Nosecone/SmartNosecone.cs and the data type in                 │
//  │       RocketParameters.cs.                                           │
//  └──────────────────────────────────────────────────────────────────────┘
// =============================================================================

namespace Tintreach
{
    /// <summary>
    /// User-facing rocket registry, active selection, and viewer toggles.
    /// All edits a user would normally make live in this single class.
    /// </summary>
    public static class Rockets
    {
        // ────────────────────────────────────────────────────────────── //
        //  §1  ROCKET DEFINITIONS                                        //
        //      Add / edit entries here. Order does not matter, except    //
        //      that whatever you set as Active in §2 must be defined     //
        //      *above* the Active line in source order.                  //
        //                                                                //
        //  Auto-defaults applied to every rocket unless overridden:      //
        //    · Nose length           : fineness × OD, where fineness =   //
        //                              4 (subsonic), 6 (supersonic), 5   //
        //                              (hypersonic). Pure-aero default;  //
        //                              set explicitly when CG, payload   //
        //                              bay, or total-length budget       //
        //                              matters (Hoerner / Crowell).      //
        //    · Nose shoulder length  : 1.0 × OD  (one caliber, NAR/TARC  //
        //                                         structural minimum).   //
        //    · Nose shoulder Ø       : 0.92 × OD (standard slip-fit).    //
        //    · Shoulder cap          : on (closed bulkhead disk).        //
        //    · Wall thickness        : 2 mm                              //
        //    · Voxel resolution      : 0.15 mm (near-CAD quality)        //
        //    · Bluffness ratio       : 0.15 (only matters for M ≥ 5)     //
        //                                                                //
        //  Per-rocket opt-in / opt-out switches:                         //
        //    · bAutoNoseLength       : true  → ignore the manual         //
        //                                      fNoseLengthMm value and   //
        //                                      use the aero auto-derive  //
        //                                      instead. Lets you keep    //
        //                                      the engineered length     //
        //                                      visible in source while   //
        //                                      rendering the optimal     //
        //                                      one (e.g. for design      //
        //                                      review screenshots).      //
        //                              false → use fNoseLengthMm if      //
        //                                      positive, otherwise auto. //
        //    · fNoseShoulderLengthMm : pass -1f to disable the shoulder  //
        //                              entirely for a single rocket.     //
        // ────────────────────────────────────────────────────────────── //

        /// <summary>
        /// Cerberus — OpenRocket-aligned parameters (design view ~320 cm total length, 113 mm max Ø).
        /// fBodyLengthMm is the cylindrical airframe length only — not nose-to-tail total.
        /// TotalLengthMm ≈ fNoseLengthMm + fBodyLengthMm with bAutoNoseLength false (504 + 2696 = 3200 mm).
        /// CG/CP mm are from nose tip; mass baseline: 9188 g no motors vs 12473 g with motors — pick one consistently with your OR export (hybrid prefers no-fin / no-motor airframe when possible).
        /// </summary>
        public static readonly RocketParameters Cerberus = new(
            fOuterDiameterMm:          113f,
            fBodyLengthMm:             2696f,
            fMaxMach:                  0.77f,
            fNoseLengthMm:             504f,
            bAutoNoseLength:           false,
            fWallThicknessMm:          2f,
            fNoseShoulderLengthMm:     50f,
            fNoseShoulderDiameterMm:   104f,
            bNoseShoulderCapped:       false,
            fBodyNoFinsMassKg:         12.473f,  // wet, OpenRocket "Mass with motors" panel
            fBodyNoFinsCgMm:           1870f,    // from nose tip (no-fins baseline)
            fBodyNoFinsCpMm:           1610f,    // from nose tip (no-fins baseline)
            fFinMaterialDensityKgM3:   1850f,
            fFinReservedRelativeDensity:   0.15f,
            fBodyNoFinsCNa:               7.055f,
            fTargetStaticMargin:       2f,
            fMinRootChordRatio:        1.42f,
            fMinSemiSpanRatio:         0.75f);

       

        public static readonly RocketParameters Eclipse = new(
            fOuterDiameterMm:          57.9f,
            fBodyLengthMm:             914f,
            fMaxMach:                  0.24f,
            fNoseLengthMm:             232f,
            bAutoNoseLength:           false,
            fWallThicknessMm:          2f,
            fNoseShoulderLengthMm:     20f,
            fNoseShoulderDiameterMm:   54f,
            bNoseShoulderCapped:       false,
            fBodyNoFinsMassKg:         0.491f,  // wet, OpenRocket "Mass with motors" panel
            fBodyNoFinsCgMm:           620f,    // from nose tip (no-fins baseline)
            fBodyNoFinsCpMm:           108f,    // from nose tip (no-fins baseline)
            fFinMaterialDensityKgM3:   1200f,
            fFinReservedRelativeDensity:   0.15f,
            fBodyNoFinsCNa:               2f,
            fTargetStaticMargin:       2.6f);
   
        public static readonly RocketParameters FinMdoDemo = new(
            fOuterDiameterMm:       55f,
            fBodyLengthMm:          1800f,
            fMaxMach:               1.5f,
            fNoseLengthMm:          200f,
            bAutoNoseLength:         true,
            fVoxelSizeMm:           0.35f,
            fBodyNoFinsMassKg:       10f,
            fBodyNoFinsCgMm:         900f,
            fBodyNoFinsCpMm:         700f,
            fFinMaterialDensityKgM3:     1200f,
            fFinReservedRelativeDensity:     0.15f,
            fTargetStaticMargin:         2f);

            public static readonly RocketParameters Orpheus = new(
            fOuterDiameterMm:          100f,
            fBodyLengthMm:             2900f,
            fMaxMach:                  1.0f,
            fNoseLengthMm:             500f,
            bAutoNoseLength:           false,
            fWallThicknessMm:          3f,
            fNoseShoulderLengthMm:     75f,
            fNoseShoulderDiameterMm:   96f,
            bNoseShoulderCapped:       false,
            fBodyNoFinsMassKg:         18.716f,  // wet, OpenRocket "Mass with motors" panel
            fBodyNoFinsCgMm:           2240f,    // from nose tip (no-fins baseline)
            fBodyNoFinsCpMm:           1710f,    // from nose tip (no-fins baseline)
            fFinMaterialDensityKgM3:   1850f,
            fFinReservedRelativeDensity:   0.15f,
            fBodyNoFinsCNa:              11.036f,
            fTargetStaticMargin:       2f);


        // ────────────────────────────────────────────────────────────── //
        //  §2  ACTIVE ROCKET                                             //
        //      Change THIS line to switch which rocket is built.         //
        // ────────────────────────────────────────────────────────────── //
        public static readonly RocketParameters Active = Orpheus;


        // ────────────────────────────────────────────────────────────── //
        //  §3  RENDER & EXPORT TOGGLES                                   //
        //      Cosmetic / context options for the viewer, and toggle     //
        //      for writing a printable file alongside the preview.       //
        // ────────────────────────────────────────────────────────────── //

        /// <summary>
        /// When true, the active <see cref="Tintreach.Nosecone.SmartNosecone"/> is built
        /// and previewed. Set false to view other parts only (e.g. fins alone).
        /// </summary>
        public static readonly bool ShowNosecone = false;

        /// <summary>
        /// When true, the <see cref="Tintreach.Fins.SmartFinModule"/> pass runs after the
        /// scene (optimization + voxel preview). Set false for nose-only or body-only.
        /// </summary>
        public static readonly bool ShowFins = true;

        /// <summary>
        /// When true, fin preview uses <see cref="FinPreviewTransparencyAlpha"/>; otherwise near-opaque.
        /// </summary>
        public static readonly bool FinPreviewTransparent = false;

        /// <summary>
        /// Passed to <c>Sh.PreviewVoxels</c> when <see cref="FinPreviewTransparent"/> is true.
        /// </summary>
        public static readonly float FinPreviewTransparencyAlpha = 0.5f;

        /// <summary>
        /// When true, the cylindrical rocket body tube is rendered behind
        /// the nosecone for context. Off by default since most reviews
        /// focus on the nose itself.
        /// </summary>
        public static readonly bool ShowBodyTube = false;

        /// <summary>
        /// When true, the Argos PDE pintle injector (<see cref="Tintreach.Engines.PintleInjector"/>)
        /// is built and previewed after the scene. Set false to hide it.
        /// </summary>
        public static readonly bool ShowPintleInjector = false;

        /// <summary>When true, writes <c>exports/{name}_fin_assembly.stl</c> (solid sleeve + fins).</summary>
        public static readonly bool ExportFinStl = false;

        /// <summary>
        /// When true, writes <c>exports/{name}_pintle_injector.stl</c> (injector solid in its CAD frame —
        /// bulkhead base at Z=0, not the +600 mm viewer shift).
        /// </summary>
        public static readonly bool ExportPintleInjectorStl = false;

        /// <summary>
        /// When true, the PDE combustor tube + Shchelkin spiral assembly is built and previewed.
        /// Dimensions come from the <c>PdeChamber*</c> and <c>PdeSpiral*</c> fields below.
        /// </summary>
        public static readonly bool ShowPdeCombustor = false;

        /// <summary>When true, writes <c>exports/{name}_pde_combustor.stl</c>.</summary>
        public static readonly bool ExportPdeCombustorStl = false;

        /// <summary>
        /// When true, adds the divergent nozzle to the PDE assembly (mounted on the combustor open end).
        /// Requires <see cref="ShowPdeCombustor"/>.
        /// </summary>
        public static readonly bool ShowDivergentNozzle = true;

        /// <summary>
        /// When true, mounts a <see cref="Tintreach.Engines.PintleInjector"/> at the combustor inlet (Z=0)
        /// in the PDE assembly. Sizing comes from <c>Engines/PintleInjector.cs</c> constructor defaults only.
        /// </summary>
        public static readonly bool ShowPdePintleInjector = true;

        /// <summary>
        /// Axial gap (mm) between the pintle tip and the first turn of the Shchelkin spiral.
        /// </summary>
        public static readonly float PdeSpiralClearanceAfterPintleMm = 5f;

        /// <summary>PDE combustor tube inner diameter (mm).</summary>
        public static readonly float PdeChamberInnerDiameterMm = 50f;

        /// <summary>PDE combustor tube axial length (mm).</summary>
        public static readonly float PdeChamberLengthMm = 300f;

        /// <summary>PDE combustor tube wall thickness (mm).</summary>
        public static readonly float PdeChamberWallThicknessMm = 3f;

        /// <summary>Shchelkin wire helix section length (mm).</summary>
        public static readonly float PdeSpiralLengthMm = 300f;

        /// <summary>Shchelkin helix pitch (mm). 0 = default to chamber inner diameter.</summary>
        public static readonly float PdeSpiralPitchMm = 0f;

        /// <summary>Target Shchelkin blockage ratio (0–1).</summary>
        public static readonly float PdeSpiralBlockingRatio = 0.45f;

        /// <summary>Nozzle exit inner diameter (mm).</summary>
        public static readonly float PdeNozzleExitDiameterMm = 80f;

        /// <summary>Diverging section length (mm).</summary>
        public static readonly float PdeNozzleLengthMm = 80f;

        /// <summary>Nozzle structural wall thickness (mm).</summary>
        public static readonly float PdeNozzleWallThicknessMm = 4f;

        /// <summary>Sleeve overlap length on combustor OD (mm).</summary>
        public static readonly float PdeNozzleSleeveLengthMm = 20f;

        /// <summary>
        /// When true, the active nosecone is also exported to a binary STL
        /// file in the project's `exports/` folder, alongside the live
        /// viewer preview. The file is named after the active rocket and
        /// overwritten on every run, so the latest geometry is always at
        /// the same path (handy for keeping a slicer open and reloading).
        ///
        /// File path: `exports/{ActiveRocketName}_nose.stl`
        ///   e.g.    `exports/Cerberus_nose.stl`
        ///
        /// File size scales with voxel resolution: ~50–150 MB at the
        /// default 0.15 mm voxel for a Ø96 mm × 500 mm nose. If your
        /// slicer chokes or transfer is slow, bump the rocket's
        /// fVoxelSizeMm to 0.25 mm — visually identical at FDM scales,
        /// ~4× smaller file.
        ///
        /// In the slicer:
        ///   · Set INFILL = 0 % (the part is already a hollow shell).
        ///   · Print tip-down for elliptical / Von Kármán noses (no
        ///     supports needed, base rests on bed).
        ///   · Print tip-up + tree supports for hypersonic blunted noses.
        /// </summary>
        public static readonly bool ExportStl = false;
    }
}
