// =============================================================================
//  Tintreach Propulsion — RocketParameters
//  -----------------------------------------------------------------------------
//  Engine type definition for a rocket's physical configuration. Pure library
//  code — you should never need to edit this file in normal use.
//
//  To add or switch rockets, edit Rockets.cs instead.
//
//  Every component in the model (nosecone, body, future nozzle, fins, motor
//  case, payload bay, …) MUST derive its geometry from a RocketParameters
//  instance. This guarantees that, for example, the body diameter and the
//  nosecone base diameter cannot drift out of sync — they're computed from a
//  single value (`fOuterDiameterMm`).
// =============================================================================

namespace Tintreach
{
    /// <summary>
    /// Fin airfoil cross-section regime, selected from design Mach.
    /// Boundaries calibrated to drag-divergence onset (≈ M_DD 0.70 for τ ≈ 6 %)
    /// and full-supersonic post-transonic peak (M ≈ 1.2). Boundaries pulled
    /// inward to 0.65 / 1.30 for engineering margin and to side-step the
    /// transonic-shock-oscillation failure mode (Nakka, Frostfire-3, NACA TN-4197).
    /// </summary>
    public enum FinAirfoilRegime
    {
        SubsonicRounded,
        TransonicHexagonal,
        SupersonicHexagonal
    }

    /// <summary>
    /// Physical configuration of a rocket. Pass this (or a derived view) to
    /// every component constructor. Construct instances in Rockets.cs.
    /// </summary>
    /// <param name="fOuterDiameterMm">
    /// Outer diameter of the airframe at the body-tube. The nosecone base
    /// diameter and any future motor-case OD inherit from this value.
    /// </param>
    /// <param name="fBodyLengthMm">
    /// Length of the cylindrical body tube (excluding nosecone).
    /// </param>
    /// <param name="fMaxMach">
    /// Design maximum Mach number. Drives the SmartNosecone regime selection
    /// and (in future iterations) sizing of the thermal-protection layer,
    /// nozzle expansion ratio, and aerodynamic loads.
    /// </param>
    /// <param name="fNoseLengthMm">
    /// Manually-specified nosecone length from tip to base. By default this
    /// value is *used when positive* and *ignored when 0 / negative* (in
    /// which case the auto-derive kicks in — see bAutoNoseLength).
    ///
    /// Keep your real engineered value here even when you're temporarily
    /// rendering the aero-optimal version: flipping bAutoNoseLength to true
    /// overrides this number without erasing it, so the documented dimension
    /// stays visible in source.
    ///
    /// Consumers should read EffectiveNoseLengthMm rather than this raw
    /// field — the Effective property handles auto / manual resolution.
    /// </param>
    /// <param name="bAutoNoseLength">
    /// Opt-in / opt-out switch for the nose-length auto-derive:
    ///   · false (default) → use fNoseLengthMm if positive; otherwise
    ///                       fall through to auto-derive.
    ///   · true            → ALWAYS auto-derive, ignoring fNoseLengthMm
    ///                       even if it's set to a positive value. Useful
    ///                       for "what would the aero-optimal version look
    ///                       like?" comparisons without deleting your real
    ///                       engineered length from source.
    ///
    /// Auto-derive rule: L = fineness × OD, with fineness chosen by the
    /// design Mach regime —
    ///     Subsonic   (M &lt; 0.8)    → L = 4 × OD
    ///     Supersonic (0.8 ≤ M &lt; 5) → L = 6 × OD
    ///     Hypersonic (M ≥ 5.0)    → L = 5 × OD
    /// (Sources: Hoerner "Fluid-Dynamic Drag" §6, Crowell "Descriptive
    /// Geometry of Nose Cones", Sears-Haack body theory.)
    ///
    /// CAUTION: the auto-derive is pure aerodynamics. It ignores stability
    /// margin (CG/CP), payload-bay volume, total-length constraints, and
    /// manufacturing limits — keep a real engineered fNoseLengthMm for any
    /// flight build.
    /// </param>
    /// <param name="fWallThicknessMm">
    /// Default wall thickness for hollow components (nosecone shell, future
    /// motor-case shell, payload-bay shell, …). Individual components may
    /// override this for local reinforcement.
    /// </param>
    /// <param name="fNoseBluffnessRatio">
    /// Tip-sphere radius / base radius for the spherically-blunted-cone
    /// nosecone branch (only relevant when fMaxMach ≥ 5.0). Crowell observes
    /// drag minimum near 0.15 caliber bluffness.
    /// </param>
    /// <param name="fVoxelSizeMm">
    /// PicoGK voxel resolution. Smaller = finer surface, slower build, more
    /// RAM. Applies model-wide. Rough rule of thumb (single nose, ~96 mm Ø,
    /// ~500 mm long, contemporary laptop):
    ///   · 0.50 mm → ~5 s build, visibly stair-stepped curves.
    ///   · 0.25 mm → ~20 s, smooth at normal viewing distance.
    ///   · 0.15 mm → ~1 min, near-CAD quality (default).
    ///   · 0.10 mm → ~2 min, print-ready preview; 1 GB+ RAM territory.
    /// </param>
    /// <param name="fNoseShoulderLengthMm">
    /// Length of the nosecone's aft shoulder (the cylindrical stub that
    /// inserts into the body tube). Three modes:
    ///   ·  0 (default) → AUTO = 1.0 × fOuterDiameterMm (one caliber).
    ///                    This is the NAR/TARC structural minimum and the
    ///                    Apogee Components recommended starting point for
    ///                    a robust nose-body joint. Every rocket gets a
    ///                    sensible shoulder unless you opt out.
    ///   · positive     → use the explicit value (overrides the default).
    ///   · negative     → disable the shoulder entirely.
    /// Consumers should read EffectiveNoseShoulderLengthMm rather than this
    /// raw field — the Effective property resolves the auto/disable cases.
    /// </param>
    /// <param name="fNoseShoulderDiameterMm">
    /// Outer diameter of the nosecone's aft shoulder. Must be strictly less
    /// than fOuterDiameterMm. 0 (default) auto-derives as 0.92 × the body
    /// diameter (slip-fit clearance). Ignored when the shoulder is disabled
    /// (i.e. when fNoseShoulderLengthMm is negative).
    /// </param>
    /// <param name="fFinMaterialDensityKgM3">
    /// Fin solid density (e.g. PETG ≈ 1200). Used with
    /// <see cref="MassKgFromVolumeMm3"/> (kg/m³ × mm³ × 10⁻⁹ → kg).
    /// </param>
    /// <param name="fFinReservedRelativeDensity">
    /// Reserved scalar (default 0.15). Solid <see cref="Fins.SmartFinModule"/> does not use it.
    /// </param>
    /// <param name="fBodyNoFinsMassKg">OpenRocket (or simulation) baseline vehicle mass excluding fins.</param>
    /// <param name="fBodyNoFinsCgMm">Baseline CG measured from nose tip (mm).</param>
    /// <param name="fBodyNoFinsCpMm">Baseline CP measured from nose tip (mm).</param>
    /// <param name="fTargetStaticMargin">
    /// Target static margin in calibres: (CombinedCpMm − CombinedCgMm) / fOuterDiameterMm after hybrid fin sizing.
    /// <see cref="Tintreach.Fins.SmartFinModule"/> grows semi-span until this is met, with structural floors
    /// (semi-span ≥ fOuterDiameterMm × <see cref="fMinSemiSpanRatio"/>, peak thickness ≥ module minimum).
    /// </param>
    /// <param name="fMinRootChordRatio">
    /// Root chord floor as a multiple of body OD (rocketry convention: ratios vs diameter, not radius).
    /// <see cref="Tintreach.Fins.SmartFinModule.RootChordMm"/> = fOuterDiameterMm × this value.
    /// </param>
    /// <param name="fMinSemiSpanRatio">
    /// Minimum exposed fin semi-span (MDO start / floor) as a multiple of body OD.
    /// </param>
    public sealed record RocketParameters(
        float fOuterDiameterMm,
        float fBodyLengthMm,
        float fMaxMach,
        float fNoseLengthMm           = 0f,    // 0 = no manual value provided
        bool  bAutoNoseLength         = false, // true = override fNoseLengthMm and auto-derive
        float fWallThicknessMm        = 2f,
        float fNoseBluffnessRatio     = 0.15f,
        float fVoxelSizeMm            = 0.3f,
        float fNoseShoulderLengthMm   = 0f,
        float fNoseShoulderDiameterMm = 0f,
        bool  bNoseShoulderCapped     = true,
        float fBodyNoFinsMassKg       = 0f,
        float fBodyNoFinsCgMm         = 0f,
        float fBodyNoFinsCpMm         = 0f,
        float fFinMaterialDensityKgM3     = 1200f,
        float fFinReservedRelativeDensity   = 0.15f,
        float fBodyNoFinsCNa             = 7.055f,
        float fTargetStaticMargin         = 2f,
        float fMinRootChordRatio          = 1.42f,
        float fMinSemiSpanRatio           = 0.75f)
    {
        // ============================================================== //
        //  Default fineness ratios for the nose-length auto-derive       //
        //  (only consulted when fNoseLengthaMm = 0; ignored otherwise).   //
        //  ----------------------------------------------------------    //
        //  Sources:                                                      //
        //    · Hoerner, "Fluid-Dynamic Drag" (1965), §6 — fineness 4–6   //
        //      brackets the subsonic drag minimum for length-fixed       //
        //      bodies of revolution.                                     //
        //    · Crowell, "Descriptive Geometry of Nose Cones" — notes     //
        //      subsonic drag varies <5 % across F = 2–6, so 4 is a safe  //
        //      central choice.                                           //
        //    · NASA TR-1135 / Sears-Haack body theory — supersonic wave  //
        //      drag ∝ (D/L)², driving the optimum to higher F as Mach    //
        //      increases. Values 5–8 are typical at 1 < M < 5; we pick   //
        //      6 as a balanced default.                                  //
        //    · Hypersonic vehicle literature (Anderson, "Hypersonic and  //
        //      High-Temperature Gas Dynamics") — F = 4–6 is structural   //
        //      / thermal-protection territory; 5 is a centred default.   //
        //  Mach thresholds (0.8, 5.0) intentionally match SmartNosecone. //
        // ============================================================== //
        public const float DEFAULT_FINENESS_SUBSONIC   = 4f;
        public const float DEFAULT_FINENESS_SUPERSONIC = 6f;
        public const float DEFAULT_FINENESS_HYPERSONIC = 5f;

        // ───────── Tintreach SmartFinModule — locked structural standards (high-power) ─────────
        /// <summary>Radial TTW tab intrusion past body OD (deep interlock with motor / detonation tube).</summary>
        public const float FinModuleTtwTabRadialDepthMm = 15f;

        /// <summary>Axial rectangular tab length ÷ root chord, centred on chord (LE/TE recess equal).</summary>
        public const float FinModuleTtwTabChordFraction = 0.55f;

        // ─── Modified Hexagonal Airfoil — geometric & regime constants ───
        /// <summary>Absolute minimum thickness (mm) at the LE/TE flat faces — kills 0 mm pinch crashes.</summary>
        public const float FinModuleMinEdgeThicknessMm = 2.0f;

        /// <summary>Absolute minimum peak thickness (mm) anywhere along span — drives skin-offset survival.</summary>
        public const float FinModuleMinPeakThicknessMm = 6.0f;

        /// <summary>Below this Mach use the rounded-subsonic profile; at/above use Modified Hexagonal.</summary>
        public const float FinModuleTransonicMachLow = 0.65f;

        /// <summary>At/above this Mach switch from transonic to full-supersonic hex parameters.</summary>
        public const float FinModuleTransonicMachHigh = 1.30f;

        /// <summary>Chord-fraction of forward max-thickness corner (OpenVSP Wedge `ThickLoc`).</summary>
        public const float FinModuleAirfoilThickLocFrac = 0.30f;

        /// <summary>Chord-fraction extent of the flat top in the transonic regime (`FlatUp` in OpenVSP).</summary>
        public const float FinModuleAirfoilFlatTopFracTransonic = 0.30f;

        /// <summary>Chord-fraction extent of the flat top in the supersonic regime (longer = closer to true wedge).</summary>
        public const float FinModuleAirfoilFlatTopFracSupersonic = 0.40f;

        /// <summary>Regime selector — drives airfoil shape, thickness ratio, CP fraction.</summary>
        public FinAirfoilRegime FinAirfoilRegimeForMach()
            =>  fMaxMach <  FinModuleTransonicMachLow  ? FinAirfoilRegime.SubsonicRounded
              : fMaxMach <  FinModuleTransonicMachHigh ? FinAirfoilRegime.TransonicHexagonal
              :                                          FinAirfoilRegime.SupersonicHexagonal;

        /// <summary>
        /// Outer radius (= half the outer diameter). Convenience accessor so
        /// component code reads naturally as `Rocket.OuterRadiusMm` rather
        /// than `Rocket.fOuterDiameterMm / 2f`.
        /// </summary>
        public float OuterRadiusMm => fOuterDiameterMm / 2f;

        /// <summary>
        /// Resolved nose length, after auto / manual handling. Use this
        /// (rather than the raw <see cref="fNoseLengthMm"/> field) when
        /// actually constructing the part. Resolution order:
        ///   1. <see cref="bAutoNoseLength"/> = true            → auto-derive.
        ///   2. else <see cref="fNoseLengthMm"/> &gt; 0         → use it.
        ///   3. else (no manual value, no auto opt-in)          → auto-derive.
        ///
        /// Auto-derive is fineness × OD, with fineness chosen by Mach regime
        /// (see the DEFAULT_FINENESS_* constants above).
        /// </summary>
        public float EffectiveNoseLengthMm
        {
            get
            {
                if (!bAutoNoseLength && fNoseLengthMm > 0f) return fNoseLengthMm;
                return AutoDerivedNoseLengthMm;
            }
        }

        /// <summary>
        /// What the auto-derive *would* return for this rocket, regardless
        /// of whether it's currently in effect. Useful for "manual vs auto"
        /// comparison logging without having to instantiate two records.
        /// </summary>
        public float AutoDerivedNoseLengthMm
        {
            get
            {
                // Mach thresholds duplicate SmartNosecone's regime selector
                // by design — the two MUST stay in sync. Hard-coded here to
                // avoid this pure-data record taking a dependency on the
                // engine layer.
                float fineness =
                    fMaxMach <  0.8f ? DEFAULT_FINENESS_SUBSONIC
                  : fMaxMach <  5.0f ? DEFAULT_FINENESS_SUPERSONIC
                  :                    DEFAULT_FINENESS_HYPERSONIC;

                return fineness * fOuterDiameterMm;
            }
        }

        /// <summary>
        /// True when EffectiveNoseLengthMm is currently driven by the
        /// auto-derive (either because bAutoNoseLength is set, or because
        /// no positive fNoseLengthMm was provided). Used by Scene.cs to
        /// label log output.
        /// </summary>
        public bool NoseLengthIsAuto => bAutoNoseLength || fNoseLengthMm <= 0f;

        /// <summary>
        /// Total airframe length (body + nose), using the resolved nose
        /// length so auto-derived noses are reflected. Useful for CG /
        /// moment-of-inertia calculations down the line. The nose shoulder
        /// is excluded because it lives *inside* the body tube and does not
        /// protrude.
        /// </summary>
        public float TotalLengthMm => fBodyLengthMm + EffectiveNoseLengthMm;

        /// <summary>
        /// Outer radius of the nose shoulder (= half the shoulder OD). Pass-
        /// through to SmartNosecone, which handles the auto-derivation when
        /// fNoseShoulderDiameterMm = 0 (it uses 0.92 × base radius for a
        /// standard slip-fit clearance).
        /// </summary>
        public float NoseShoulderRadiusMm => fNoseShoulderDiameterMm / 2f;

        /// <summary>
        /// Resolved shoulder length, after auto / disable handling. Use this
        /// (rather than the raw <see cref="fNoseShoulderLengthMm"/> field)
        /// when actually constructing the part:
        ///   ·  0 → auto: 1.0 × fOuterDiameterMm (industry-standard minimum).
        ///   · positive → explicit override, returned as-is.
        ///   · negative → disabled, resolves to 0.
        /// </summary>
        public float EffectiveNoseShoulderLengthMm =>
            fNoseShoulderLengthMm  < 0f ? 0f
          : fNoseShoulderLengthMm == 0f ? fOuterDiameterMm
          : fNoseShoulderLengthMm;

        /// <summary>
        /// Converts a volume in mm³ and material density in kg/m³ to mass in kg:
        /// m = V_mm³ × ρ × 10⁻⁹.
        /// </summary>
        public static float MassKgFromVolumeMm3(float fVolumeMm3, float fDensityKgM3)
            => fVolumeMm3 * fDensityKgM3 * 1e-9f;
    }
}
