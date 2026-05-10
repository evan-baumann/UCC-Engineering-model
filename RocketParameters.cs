// =============================================================================
//  UCC Rocketry — RocketParameters
//  -----------------------------------------------------------------------------
//  Single source of truth for the rocket's physical configuration.
//
//  Every component in the model (nosecone, body, future nozzle, fins, motor
//  case, payload bay, …) MUST derive its geometry from a RocketParameters
//  instance. This guarantees that, for example, the body diameter and the
//  nosecone base diameter cannot drift out of sync — they're computed from a
//  single value (`fOuterDiameterMm`).
//
//  Design tip: prefer adding a new field here over hard-coding a "magic
//  number" inside a component. Keeping the model parametric is what makes it
//  a Computational Engineering Model rather than a one-off CAD file.
// =============================================================================

namespace UCCRocketry
{
    /// <summary>
    /// Physical configuration of the entire rocket. Pass this (or a derived
    /// view) to every component constructor.
    /// </summary>
    /// <param name="fOuterDiameterMm">
    /// Outer diameter of the airframe at the body-tube. The nosecone base
    /// diameter and any future motor-case OD inherit from this value.
    /// </param>
    /// <param name="fBodyLengthMm">
    /// Length of the cylindrical body tube (excluding nosecone).
    /// </param>
    /// <param name="fNoseLengthMm">
    /// Length of the nosecone from tip to base.
    /// </param>
    /// <param name="fMaxMach">
    /// Design maximum Mach number. Drives the SmartNosecone regime selection
    /// and (in future iterations) sizing of the thermal-protection layer,
    /// nozzle expansion ratio, and aerodynamic loads.
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
    /// RAM. Applies model-wide.
    /// </param>
    /// <param name="fNoseShoulderLengthMm">
    /// Length of the nosecone's aft shoulder (the cylindrical stub that
    /// inserts into the body tube). 0 disables the shoulder. Standard
    /// practice: ≥ 1.0 × fOuterDiameterMm for a structurally robust joint.
    /// </param>
    /// <param name="fNoseShoulderDiameterMm">
    /// Outer diameter of the nosecone's aft shoulder. Must be strictly less
    /// than fOuterDiameterMm. 0 (default) auto-derives as 0.92 × the body
    /// diameter (slip-fit clearance). Ignored when fNoseShoulderLengthMm = 0.
    /// </param>
    /// <param name="bNoseShoulderCapped">
    /// When true, the shoulder ends in a closed bulkhead disk (one wall
    /// thickness deep). Mirrors OpenRocket's `aftshouldercapped` flag.
    /// </param>
    public sealed record RocketParameters(
        float fOuterDiameterMm,
        float fBodyLengthMm,
        float fNoseLengthMm,
        float fMaxMach,
        float fWallThicknessMm        = 2f,
        float fNoseBluffnessRatio     = 0.15f,
        float fVoxelSizeMm            = 0.5f,
        float fNoseShoulderLengthMm   = 0f,
        float fNoseShoulderDiameterMm = 0f,
        bool  bNoseShoulderCapped     = true)
    {
        /// <summary>
        /// Outer radius (= half the outer diameter). Convenience accessor so
        /// component code reads naturally as `Rocket.OuterRadiusMm` rather
        /// than `Rocket.fOuterDiameterMm / 2f`.
        /// </summary>
        public float OuterRadiusMm => fOuterDiameterMm / 2f;

        /// <summary>
        /// Total airframe length (body + nose), useful for CG / moment-of-
        /// inertia calculations down the line. The nose shoulder is excluded
        /// because it lives *inside* the body tube and does not protrude.
        /// </summary>
        public float TotalLengthMm => fBodyLengthMm + fNoseLengthMm;

        /// <summary>
        /// Outer radius of the nose shoulder (= half the shoulder OD). Pass-
        /// through to SmartNosecone, which handles the auto-derivation when
        /// fNoseShoulderDiameterMm = 0.
        /// </summary>
        public float NoseShoulderRadiusMm => fNoseShoulderDiameterMm / 2f;

        // ============================================================== //
        //  Named rocket configurations                                   //
        //  ------------------------------------------------------------  //
        //  Add a new `public static readonly RocketParameters Foo = ...` //
        //  here for every distinct rocket you want to model. Switch      //
        //  which one Program.cs builds by changing a single line:        //
        //                                                                //
        //      private static readonly RocketParameters Rocket =         //
        //          RocketParameters.Competition;                         //
        //                                                                //
        //  These three example configs are deliberately spread across    //
        //  the Mach regimes so that each one selects a *different*       //
        //  SmartNosecone branch — switching configs visibly changes the  //
        //  nose shape in the viewer.                                     //
        //                                                                //
        //  Rename / replace these with your team's actual project        //
        //  names (e.g. "EuRoC2027", "ICLR_TestArticle", etc.).           //
        // ============================================================== //

        /// <summary>
        /// Subscale subsonic flight-test article. Small, slow, used to
        /// validate avionics, recovery, and ground-handling procedures
        /// before scaling up. → Elliptical nosecone.
        /// </summary>
        public static readonly RocketParameters Pathfinder = new(
            fOuterDiameterMm: 60f,
            fBodyLengthMm:    600f,
            fNoseLengthMm:    180f,
            fMaxMach:         0.6f);

        /// <summary>
        /// Main competition rocket — supersonic, transonic-bursting flight
        /// profile typical of EuRoC / Spaceport America Cup entries.
        /// → Sharp Von Kármán nosecone.
        /// </summary>
        public static readonly RocketParameters Competition = new(
            fOuterDiameterMm: 100f,
            fBodyLengthMm:    2000f,
            fNoseLengthMm:    350f,
            fMaxMach:         1.5f);

        /// <summary>
        /// Stretch-goal high-altitude / hypersonic vehicle. Sustained
        /// flight at high Mach drives nosecone blunting for thermal
        /// protection. → Spherically Blunted Cone nosecone.
        /// </summary>
        public static readonly RocketParameters HighAltitude = new(
            fOuterDiameterMm: 80f,
            fBodyLengthMm:    1500f,
            fNoseLengthMm:    400f,
            fMaxMach:         6.0f);

        /// <summary>
        /// Cerberus — UCCRPL two-stage subsonic flight vehicle.
        ///
        /// Dimensions extracted from Ce_17_04_26.ork (designer="UCCRPL"):
        ///   · Body tube OD       : 96 mm   (sustainer + booster share Ø)
        ///   · Sustainer body len : 1600 mm (Upper 620 + Avionics 30 + Lower 950 mm,
        ///                                   3 mm-wall fibreglass sections)
        ///   · Booster body len   : 1000 mm (carbon-fibre, drops away post-staging,
        ///                                   not modelled here as it never carries
        ///                                   the nosecone)
        ///   · Wall thickness     : 2 mm    (matches existing OR nose for drop-in)
        ///   · Existing nose      : 504 mm ogive (REPLACED by SmartNosecone — under
        ///                                   the literature mapping, M 0.77 selects
        ///                                   an Elliptical nose, which Crowell
        ///                                   explicitly recommends for subsonic
        ///                                   rockets)
        ///   · Nose aft shoulder  : 50 mm long, Ø89 mm, capped (preserved verbatim
        ///                                   from the .ork so the printed nose is a
        ///                                   drop-in physical replacement)
        ///
        /// Flight performance (RK4 sim, ideal conditions):
        ///   · Max altitude       : 2 785 m (~9 100 ft)
        ///   · Max velocity       : 258 m/s
        ///   · Max Mach           : 0.77    → Subsonic regime → Elliptical nosecone
        ///   · Max acceleration   : 9.6 g
        ///   · Motors             : K515 booster → K1200 sustainer
        /// </summary>
        public static readonly RocketParameters Cerberus = new(
            fOuterDiameterMm:        96f,
            fBodyLengthMm:           1600f,
            fNoseLengthMm:           504f,
            fMaxMach:                0.77f,
            fWallThicknessMm:        2f,
            fNoseShoulderLengthMm:   50f,
            fNoseShoulderDiameterMm: 89f,
            bNoseShoulderCapped:     true);

        /// <summary>
        /// Convenience alias used by Program.cs as the "build this one"
        /// pointer. Re-bind to switch the active rocket — every component
        /// re-derives its dimensions automatically.
        /// </summary>
        public static readonly RocketParameters Default = Cerberus;
    }
}
