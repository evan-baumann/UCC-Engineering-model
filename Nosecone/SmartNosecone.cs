// =============================================================================
//  Tintreach Propulsion — SmartNosecone
//  -----------------------------------------------------------------------------
//  Mach-aware parametric nosecone, built on top of the Leap71 ShapeKernel
//  (BaseRevolve + LineModulation).
//
//  Three regimes are implemented, each driven by the "smart" maxMach selector:
//
//    M < 0.8         Subsonic     -> Elliptical (prolate hemispheroid)
//                                    Crowell §"Drag" / Wikipedia "Elliptical":
//                                       y(x) = R · sqrt(x · (2L − x)) / L
//                                    Optimal subsonic shape (minimum wetted area
//                                    for tangent-base profile).
//
//    0.8 ≤ M < 5.0   Trans/Super  -> Sharp Von Kármán (LD-Haack, C = 0)
//                                    Wikipedia "Haack series" / AeroSandbox
//                                    `karman()` in nosecone_shapes/haack.py:
//                                       θ(x) = arccos(1 − 2x/L)
//                                       y(x) = (R/√π) · √(θ − sin(2θ)/2)
//                                    Analytical minimum wave drag for fixed
//                                    length and base diameter.
//
//    M ≥ 5.0         Hypersonic   -> Spherically Blunted Cone
//                                    Wikipedia "Spherically blunted conic":
//                                       y_t = r_n · L / sqrt(R² + L²)
//                                       x_t = r_n · L² / (R · sqrt(R² + L²))
//                                       x_o = x_t + sqrt(r_n² − y_t²)
//                                    With a closed-form solve for L_sharp so
//                                    the *blunted* total length equals the
//                                    user's fLengthMm exactly (rather than
//                                    silently shortening the part).
//
//  All three branches are wrapped into a single LineModulation r(z/L), which is
//  then used as the OUTER radius of a BaseRevolve. The INNER radius is r(z/L)
//  minus the wall thickness (clamped at zero so the tip remains solid where
//  the shell would otherwise self-intersect).
//
//  Optional aft shoulder
//  ---------------------
//  When fShoulderLengthMm > 0, the part is extended axially by a constant-
//  radius cylindrical "shoulder" that slides into the body tube. This is the
//  joint feature that aligns and retains the nosecone — it is *not* visible
//  from the outside on a fully-assembled rocket. Geometrically the same
//  LineModulation/BaseRevolve pair is reused, with the profile defined as
//
//      R(z) = nose_contour(z)           for 0 ≤ z ≤ L_nose
//           = R_shoulder                for L_nose < z ≤ L_nose + L_shoulder
//
//  The discontinuity at z = L_nose deliberately produces a flat annular face
//  (the "stop ridge") that the upper rim of the body tube butts against — a
//  load-transferring feature found on virtually every real flight nosecone.
//
//  When bShoulderCapped = true, the inner cavity is truncated one wall
//  thickness short of the very base, leaving a closed disk at the bottom of
//  the shoulder. This bulkhead seals the airframe internal volume against
//  the recovery bay and resists ejection-charge gas pressure.
//
//  Sources
//  -------
//    [Crowell 1996]   Crowell Sr., G.A. — "The Descriptive Geometry of Nose
//                     Cones", available at nakka-rocketry.net.
//    [Wikipedia]      en.wikipedia.org/wiki/Nose_cone_design
//    [AeroSandbox]    github.com/peterdsharpe/AeroSandbox  →
//                     aerosandbox/geometry/nosecone_shapes/haack.py
// =============================================================================

using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

namespace Tintreach.Nosecone
{
    /// <summary>
    /// Mach-regime-aware parametric nosecone. The shape used is selected
    /// automatically from the design Mach number at construction time.
    /// </summary>
    public class SmartNosecone
    {
        public enum ENoseRegime
        {
            /// <summary>Elliptical — minimum wetted area, M &lt; 0.8.</summary>
            SUBSONIC_ELLIPTICAL,

            /// <summary>Sharp Von Kármán (LD-Haack, C = 0) — minimum wave drag, 0.8 ≤ M &lt; 5.</summary>
            SUPERSONIC_VON_KARMAN,

            /// <summary>Spherically blunted cone — survives stagnation heating, M ≥ 5.</summary>
            HYPERSONIC_BLUNTED_CONE
        }

        // ------------------------------------------------------------------ //
        //  Mach thresholds (literature-recommended, see header for sources). //
        // ------------------------------------------------------------------ //
        public const float TRANSONIC_MACH_THRESHOLD  = 0.8f;
        public const float HYPERSONIC_MACH_THRESHOLD = 5.0f;

        public const float DEFAULT_WALL_THICKNESS_MM = 2.0f;

        /// <summary>
        /// Default tip-sphere radius / base radius. Crowell observes drag
        /// minimum near 0.15 caliber bluffness; this is also the standard
        /// hypersonic-warhead choice.
        /// </summary>
        public const float DEFAULT_BLUFFNESS_RATIO = 0.15f;

        /// <summary>
        /// Default shoulder OD / base OD when caller supplies a shoulder
        /// length but no explicit shoulder radius. 0.92 corresponds to a
        /// standard slip-fit clearance (≈4 % per side) for fibreglass /
        /// composite body tubes — tight enough to align the nose, loose
        /// enough to assemble by hand.
        /// </summary>
        public const float DEFAULT_SHOULDER_RADIUS_RATIO = 0.92f;

        protected const uint LENGTH_STEPS = 800;
        protected const uint POLAR_STEPS  = 360;

        // ------------------------------------------------------------------ //
        //  Inputs                                                            //
        // ------------------------------------------------------------------ //
        protected readonly float       m_fBaseRadiusMm;
        protected readonly float       m_fLengthMm;
        protected readonly float       m_fMaxMach;
        protected readonly float       m_fWallThicknessMm;
        protected readonly float       m_fBluffnessRatio;
        protected readonly float       m_fShoulderLengthMm;
        protected readonly float       m_fShoulderRadiusMm;
        protected readonly bool        m_bShoulderCapped;
        protected readonly LocalFrame  m_oFrame;
        protected readonly ENoseRegime m_eRegime;

        // ------------------------------------------------------------------ //
        //  Cached blunted-cone parameters (only populated for the M ≥ 5      //
        //  branch). All values are in the *blunted* frame where z = 0 is the //
        //  blunted tip and z = m_fLengthMm is the base.                      //
        // ------------------------------------------------------------------ //
        private float m_fBC_TipSphereRadius;   // r_n
        private float m_fBC_TangentZ;          // z_t' (blunted-frame axial pos. of sphere↔cone tangent)
        private float m_fBC_TangentY;          // y_t  (radial pos. of tangent point)
        private float m_fBC_TanAlpha;          // tan(α) where α is the sharp-cone half-angle

        // ================================================================== //
        //  Construction                                                       //
        // ================================================================== //
        /// <param name="fShoulderLengthMm">
        /// Length of the cylindrical aft shoulder that inserts into the body
        /// tube. 0 (default) disables the shoulder entirely. Standard
        /// practice: ≥ 1.0 × body diameter for a structurally robust joint.
        /// </param>
        /// <param name="fShoulderRadiusMm">
        /// Outer radius of the shoulder. Must be strictly less than
        /// fBaseRadiusMm (it has to slip *into* the body tube). Pass 0
        /// (default) to auto-derive as DEFAULT_SHOULDER_RADIUS_RATIO ×
        /// fBaseRadiusMm. Ignored when fShoulderLengthMm = 0.
        /// </param>
        /// <param name="bShoulderCapped">
        /// When true, a closed disk (one wall thickness deep) is left at the
        /// very base of the shoulder, sealing the airframe interior. Match
        /// OpenRocket's `aftshouldercapped` flag. Ignored when
        /// fShoulderLengthMm = 0.
        /// </param>
        public SmartNosecone(
            float       fBaseRadiusMm,
            float       fLengthMm,
            float       fMaxMach,
            LocalFrame? oFrame             = null,
            float       fWallThicknessMm   = DEFAULT_WALL_THICKNESS_MM,
            float       fBluffnessRatio    = DEFAULT_BLUFFNESS_RATIO,
            float       fShoulderLengthMm  = 0f,
            float       fShoulderRadiusMm  = 0f,
            bool        bShoulderCapped    = true)
        {
            if (fBaseRadiusMm <= 0f)
                throw new ArgumentException("Base radius must be positive.", nameof(fBaseRadiusMm));
            if (fLengthMm <= fBaseRadiusMm)
                throw new ArgumentException(
                    "Length must exceed base radius (otherwise the tangent ogive degenerates " +
                    "to a hemisphere and the Von Kármán parametrisation is geometrically nonsensical).",
                    nameof(fLengthMm));
            if (fMaxMach < 0f)
                throw new ArgumentException("Mach number cannot be negative.", nameof(fMaxMach));
            if (fWallThicknessMm <= 0f || fWallThicknessMm >= fBaseRadiusMm)
                throw new ArgumentException(
                    "Wall thickness must lie in (0, baseRadius).", nameof(fWallThicknessMm));
            if (fBluffnessRatio <= 0f || fBluffnessRatio >= 1f)
                throw new ArgumentException(
                    "Bluffness ratio (r_n / R_base) must lie in (0, 1).", nameof(fBluffnessRatio));
            if (fShoulderLengthMm < 0f)
                throw new ArgumentException(
                    "Shoulder length cannot be negative (use 0 to disable).",
                    nameof(fShoulderLengthMm));

            // Auto-derive shoulder radius if not supplied (and shoulder is
            // enabled). Sentinel value 0 means "use default ratio".
            float fEffectiveShoulderRadius = (fShoulderRadiusMm > 0f)
                ? fShoulderRadiusMm
                : fBaseRadiusMm * DEFAULT_SHOULDER_RADIUS_RATIO;

            if (fShoulderLengthMm > 0f)
            {
                if (fEffectiveShoulderRadius <= fWallThicknessMm)
                    throw new ArgumentException(
                        "Shoulder radius must exceed wall thickness, otherwise the cavity vanishes.",
                        nameof(fShoulderRadiusMm));
                if (fEffectiveShoulderRadius >= fBaseRadiusMm)
                    throw new ArgumentException(
                        "Shoulder radius must be strictly less than base radius — it has to slip " +
                        "INSIDE the body tube. Drop it to ~0.92 × base radius for a typical slip-fit.",
                        nameof(fShoulderRadiusMm));
                if (bShoulderCapped && fShoulderLengthMm <= fWallThicknessMm)
                    throw new ArgumentException(
                        "Capped shoulder must be longer than one wall thickness, otherwise the cap " +
                        "consumes the entire shoulder and leaves no clearance to slide into the body.",
                        nameof(fShoulderLengthMm));
            }

            m_fBaseRadiusMm     = fBaseRadiusMm;
            m_fLengthMm         = fLengthMm;
            m_fMaxMach          = fMaxMach;
            m_fWallThicknessMm  = fWallThicknessMm;
            m_fBluffnessRatio   = fBluffnessRatio;
            m_fShoulderLengthMm = fShoulderLengthMm;
            m_fShoulderRadiusMm = fEffectiveShoulderRadius;
            m_bShoulderCapped   = bShoulderCapped;
            m_oFrame            = oFrame ?? new LocalFrame();
            m_eRegime           = eSelectRegime(fMaxMach);

            if (m_eRegime == ENoseRegime.HYPERSONIC_BLUNTED_CONE)
            {
                PrecomputeBluntedConeParameters();
            }
        }

        // ================================================================== //
        //  Public API                                                         //
        // ================================================================== //
        public ENoseRegime Regime => m_eRegime;

        public float BaseRadiusMm     => m_fBaseRadiusMm;
        public float LengthMm         => m_fLengthMm;
        public float MaxMach          => m_fMaxMach;
        public float ShoulderLengthMm => m_fShoulderLengthMm;
        public float ShoulderRadiusMm => m_fShoulderRadiusMm;
        public bool  IsShouldered     => m_fShoulderLengthMm > 0f;

        /// <summary>
        /// Total axial length of the part: nose + (optional) aft shoulder.
        /// </summary>
        public float TotalLengthMm => m_fLengthMm + m_fShoulderLengthMm;

        /// <summary>
        /// Builds the hollow nosecone shell as a Voxels object.
        /// The tip sits at the LocalFrame origin; the nose↔shoulder joint
        /// (the stop ridge that contacts the body tube) is at +Z·LengthMm;
        /// the base of the shoulder is at +Z·TotalLengthMm.
        /// </summary>
        public Voxels voxConstruct()
        {
            float fNoseLen  = m_fLengthMm;
            float fTotalLen = TotalLengthMm;

            // r_outer(z/L_total) — nose contour stitched onto the constant
            // shoulder cylinder. The nose modulation is parameterised on its
            // own [0,1] over fNoseLen, so we re-map fLR_total -> fLR_nose
            // before sampling it. The discontinuity at z = fNoseLen is
            // intentional: it produces a flat annular face that the body
            // tube butts up against (the joint's stop ridge).
            LineModulation oNoseRadial = oGetOuterRadiusModulation();

            LineModulation oOuter = new LineModulation(fLR =>
            {
                float fZ = fLR * fTotalLen;
                if (fZ <= fNoseLen)
                {
                    float fLR_nose = (fNoseLen > 0f) ? (fZ / fNoseLen) : 0f;
                    return oNoseRadial.fGetModulation(fLR_nose);
                }
                return m_fShoulderRadiusMm;
            });

            // r_inner(z/L_total) = max(0, r_outer − wall). Two clamps apply:
            //
            //   1. Near the tip, r_outer < wall_thickness; clamping at zero
            //      makes the tip region naturally solid (otherwise BaseRevolve
            //      would render a self-intersecting tube).
            //
            //   2. When the shoulder is capped, the bottommost wall_thickness
            //      of the shoulder is zeroed out, leaving a closed disk
            //      bulkhead at the shoulder base.
            float fCapStart = (IsShouldered && m_bShoulderCapped)
                ? fTotalLen - m_fWallThicknessMm
                : float.PositiveInfinity;

            LineModulation oInner = new LineModulation(fLR =>
            {
                float fZ = fLR * fTotalLen;
                if (fZ >= fCapStart) return 0f;

                float fOuter = oOuter.fGetModulation(fLR);
                return MathF.Max(0f, fOuter - m_fWallThicknessMm);
            });

            // Straight spine on the LocalFrame's local Z axis, length = nose
            // + shoulder. BaseRevolve sweeps r_inner..r_outer around the Z
            // axis to produce the axisymmetric hollow shell + shoulder in a
            // single voxelisation pass.
            Frames aSpine = new Frames(fTotalLen, m_oFrame);

            BaseRevolve oRevolve = new BaseRevolve(
                m_oFrame,
                aSpine,
                fInwardRadius:  0f,
                fOutwardRadius: 0f);   // overridden below by SetRadius
            oRevolve.SetRadius(oInner, oOuter);
            oRevolve.SetLengthSteps(LENGTH_STEPS);
            oRevolve.SetPolarSteps(POLAR_STEPS);

            return oRevolve.voxConstruct();
        }

        // ================================================================== //
        //  Regime selection                                                   //
        // ================================================================== //
        protected static ENoseRegime eSelectRegime(float fMach)
        {
            if (fMach <  TRANSONIC_MACH_THRESHOLD)  return ENoseRegime.SUBSONIC_ELLIPTICAL;
            if (fMach <  HYPERSONIC_MACH_THRESHOLD) return ENoseRegime.SUPERSONIC_VON_KARMAN;
            return ENoseRegime.HYPERSONIC_BLUNTED_CONE;
        }

        // ================================================================== //
        //  Radius modulation factory                                          //
        // ================================================================== //
        protected LineModulation oGetOuterRadiusModulation()
        {
            return m_eRegime switch
            {
                ENoseRegime.SUBSONIC_ELLIPTICAL    => new LineModulation(fGetEllipticalRadius),
                ENoseRegime.SUPERSONIC_VON_KARMAN  => new LineModulation(fGetVonKarmanRadius),
                ENoseRegime.HYPERSONIC_BLUNTED_CONE => new LineModulation(fGetBluntedConeRadius),
                _ => throw new InvalidOperationException(
                         $"Unhandled nose regime: {m_eRegime}")
            };
        }

        // ------------------------------------------------------------------ //
        //  Elliptical                                                        //
        //                                                                    //
        //    y(x) = R · sqrt(x · (2L − x)) / L                               //
        //         = R · sqrt(fLR · (2 − fLR))     (with fLR = x/L)           //
        //                                                                    //
        //  At fLR = 0  →  y = 0   (tip)                                      //
        //  At fLR = 1  →  y = R   (base, tangent to cylinder)                //
        // ------------------------------------------------------------------ //
        protected float fGetEllipticalRadius(float fLR)
        {
            float fArg = fLR * (2f - fLR);
            fArg       = MathF.Max(fArg, 0f);     // guard floating-point noise
            return m_fBaseRadiusMm * MathF.Sqrt(fArg);
        }

        // ------------------------------------------------------------------ //
        //  Sharp Von Kármán (LD-Haack, C = 0)                                //
        //                                                                    //
        //    θ(x) = arccos(1 − 2x/L)                                         //
        //    y(x) = (R / sqrt(π)) · sqrt(θ − sin(2θ)/2)                      //
        //                                                                    //
        //  This is identical (modulo a single normalisation factor) to the   //
        //  AeroSandbox `karman()` in nosecone_shapes/haack.py.               //
        //                                                                    //
        //  At fLR = 0 → θ = 0,  y = 0          (tip, slope is infinite)      //
        //  At fLR = 1 → θ = π,  y = R          (base, NOT tangent to body —  //
        //                                       Crowell notes the small gap  //
        //                                       is "imperceptible")          //
        // ------------------------------------------------------------------ //
        protected float fGetVonKarmanRadius(float fLR)
        {
            // arccos input clamped to its valid range to absorb FP edge noise.
            float fAcosArg = float.Clamp(1f - 2f * fLR, -1f, 1f);
            float fTheta   = MathF.Acos(fAcosArg);
            float fInner   = fTheta - 0.5f * MathF.Sin(2f * fTheta);
            fInner         = MathF.Max(fInner, 0f);
            return m_fBaseRadiusMm * MathF.Sqrt(fInner / MathF.PI);
        }

        // ------------------------------------------------------------------ //
        //  Spherically Blunted Cone, length-preserving                       //
        //                                                                    //
        //  Inputs in the SmartNosecone object:                               //
        //    L_target = m_fLengthMm                                          //
        //    R        = m_fBaseRadiusMm                                      //
        //    r_n      = m_fBluffnessRatio · R    (tip-sphere radius)         //
        //                                                                    //
        //  Solve for L_sharp (the *sharp* cone's length, used to fix α) such //
        //  that the blunted total length equals L_target. Tangency requires  //
        //                                                                    //
        //     R · (r_n + L_sharp − L_target) = r_n · sqrt(R² + L_sharp²)     //
        //                                                                    //
        //  Squaring and rearranging gives a quadratic in L_sharp whose       //
        //  geometrically meaningful root is                                  //
        //                                                                    //
        //     L_sharp = ( R²(L − r_n) + R · r_n · sqrt(R² + L² − 2 r_n L) )  //
        //               / ( R² − r_n² )                                      //
        //                                                                    //
        //  The half-angle, tangent point, and per-z radius then follow the   //
        //  Wikipedia "Spherically blunted conic" formulas, shifted into the  //
        //  blunted frame (new tip at z = 0).                                 //
        // ------------------------------------------------------------------ //
        protected void PrecomputeBluntedConeParameters()
        {
            float L  = m_fLengthMm;
            float R  = m_fBaseRadiusMm;
            float rn = m_fBluffnessRatio * R;

            float fNumerator   = R * R * (L - rn)
                               + R * rn * MathF.Sqrt(R * R + L * L - 2f * rn * L);
            float fDenominator = R * R - rn * rn;
            float fLengthSharp = fNumerator / fDenominator;

            float fAlpha = MathF.Atan(R / fLengthSharp);
            float fSin   = MathF.Sin(fAlpha);
            float fCos   = MathF.Cos(fAlpha);

            // Sphere centre is one radius behind the new (blunted) tip, so in
            // the blunted frame: z_centre = r_n. The tangent point on the
            // sphere lies at (z = r_n − r_n·sinα, y = r_n·cosα).
            m_fBC_TipSphereRadius = rn;
            m_fBC_TangentZ        = rn * (1f - fSin);
            m_fBC_TangentY        = rn * fCos;
            m_fBC_TanAlpha        = MathF.Tan(fAlpha);
        }

        protected float fGetBluntedConeRadius(float fLR)
        {
            float fZ = fLR * m_fLengthMm;

            if (fZ <= m_fBC_TangentZ)
            {
                // Spherical cap:  y(z) = sqrt( r_n² − (z − r_n)² )
                float fDz  = fZ - m_fBC_TipSphereRadius;
                float fArg = m_fBC_TipSphereRadius * m_fBC_TipSphereRadius - fDz * fDz;
                fArg       = MathF.Max(fArg, 0f);
                return MathF.Sqrt(fArg);
            }

            // Conical mantle:  y(z) = y_t + (z − z_t) · tan(α)
            return m_fBC_TangentY + (fZ - m_fBC_TangentZ) * m_fBC_TanAlpha;
        }
    }
}
