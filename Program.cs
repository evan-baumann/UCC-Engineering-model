// =============================================================================
//  UCC Rocketry – Computational Engineering Model
//  -----------------------------------------------------------------------------
//  Entry point. Boots the PicoGK runtime (voxel kernel + viewer) and renders:
//
//    1. The rocket body (cylinder), behind the nosecone showcase.
//    2. Three SmartNosecone instances side-by-side, one per Mach regime, so
//       every branch of the regime selector is visually verified at a glance:
//         · M = 0.5  → Subsonic     → Elliptical
//         · M = 3.5  → Supersonic   → Sharp Von Kármán
//         · M = 6.0  → Hypersonic   → Spherically Blunted Cone
//       The instance whose Mach matches the rocket's design fMaxMach is the
//       "actual rocket" nosecone — it's the one that will physically attach
//       to the body. The other two are visual reference only.
//
//  Every dimension below is derived from `RocketParameters.Default` (see
//  RocketParameters.cs) — there are no magic numbers in this file.
//
//  Built on top of:
//    - PicoGK              (voxel geometry kernel,         NuGet)
//    - LEAP71_ShapeKernel  (parametric shape primitives,   git submodule)
//    - LEAP71_LatticeLib.  (lattice / TPMS infill toolkit, git submodule)
//    - LEAP71_HelixHeatX   (helical heat-exchanger demo,   git submodule)
// =============================================================================

using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;
using UCCRocketry.Nosecone;

namespace UCCRocketry
{
    internal static class Program
    {
        // ============================================================== //
        //  ACTIVE ROCKET — change this one line to switch designs.       //
        //  --------------------------------------------------------------//
        //  Available configs (defined in RocketParameters.cs):           //
        //    · RocketParameters.Cerberus      — UCCRPL two-stage M 0.77  //
        //    · RocketParameters.Pathfinder    — subscale subsonic test   //
        //    · RocketParameters.Competition   — main supersonic rocket   //
        //    · RocketParameters.HighAltitude  — hypersonic stretch goal  //
        //    · RocketParameters.Default       — alias to the primary one //
        //                                                                //
        //  Add your own by appending another `public static readonly     //
        //  RocketParameters …` to RocketParameters.cs.                   //
        // ============================================================== //
        private static readonly RocketParameters Rocket = RocketParameters.Cerberus;

        public static void Main()
        {
            PicoGK.Library.Go(
                fVoxelSizeMM:    Rocket.fVoxelSizeMm,
                fnTask:          BuildScene,
                strWindowTitle:  "UCC Rocketry — Computational Engineering Model");
        }

        /// <summary>
        /// Construction task executed inside the PicoGK runtime.
        /// </summary>
        private static void BuildScene()
        {
            try
            {
                Library.Log(
                    $"Rocket configuration: " +
                    $"Ø{Rocket.fOuterDiameterMm:0.#} mm × " +
                    $"{Rocket.TotalLengthMm:0.#} mm total " +
                    $"(body {Rocket.fBodyLengthMm:0.#} + nose {Rocket.fNoseLengthMm:0.#}), " +
                    $"design M = {Rocket.fMaxMach:0.0}.");

                BuildRocketBody();
                BuildNoseconeShowcase();
            }
            catch (Exception ex)
            {
                Library.Log($"Scene construction failed: {ex}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Rocket body (cylinder, parked behind the nosecone showcase)       //
        // ------------------------------------------------------------------ //
        private static void BuildRocketBody()
        {
            Library.Log("Building rocket body...");

            LocalFrame oFrame = new LocalFrame(new Vector3(0f, -300f, 0f));

            BaseCylinder oRocketBody = new BaseCylinder(
                oFrame:  oFrame,
                fLength: Rocket.fBodyLengthMm,
                fRadius: Rocket.OuterRadiusMm);

            Voxels voxRocketBody = oRocketBody.voxConstruct();
            Sh.PreviewVoxels(voxRocketBody, Cp.clrRock);

            Library.Log(
                $"  rocket body voxelised: " +
                $"Ø{Rocket.fOuterDiameterMm:0.#} mm × {Rocket.fBodyLengthMm:0.#} mm.");
        }

        // ------------------------------------------------------------------ //
        //  SmartNosecone — one render per Mach regime                        //
        //  ----------------------------------------------------------------  //
        //  The three Mach values below are *deliberately fixed* so that all  //
        //  three branches of the regime selector appear in the viewer for    //
        //  visual regression. Geometry (radius, length, wall, bluffness) is  //
        //  drawn from RocketParameters — only the Mach number varies.        //
        // ------------------------------------------------------------------ //
        private static void BuildNoseconeShowcase()
        {
            Library.Log("Building SmartNosecone showcase...");

            // Spacing: 2.5 × outer radius gives a clear gap between adjacent noses.
            float fSpacing = 2.5f * Rocket.OuterRadiusMm;

            // Determine which slot represents the active rocket's actual nose
            // by REGIME (not by exact Mach), so the marker stays meaningful as
            // configs change (e.g. Cerberus M=0.77 still highlights M=0.5).
            SmartNosecone.ENoseRegime eActiveRegime =
                new SmartNosecone(
                    fBaseRadiusMm: Rocket.OuterRadiusMm,
                    fLengthMm:     Rocket.fNoseLengthMm,
                    fMaxMach:      Rocket.fMaxMach).Regime;

            (float fX, float fMach, ColorFloat clr)[] aSlots =
            {
                (-fSpacing, 0.5f, Cp.clrFrozen),    // subsonic   — light blue
                ( 0f,       3.5f, Cp.clrYellow),    // supersonic — yellow
                ( fSpacing, 6.0f, Cp.clrWarning),   // hypersonic — orange
            };

            foreach ((float fX, float fMach, ColorFloat clr) in aSlots)
            {
                LocalFrame    oFrame = new LocalFrame(new Vector3(fX, 0f, 0f));
                SmartNosecone oNose  = new SmartNosecone(
                    fBaseRadiusMm:     Rocket.OuterRadiusMm,
                    fLengthMm:         Rocket.fNoseLengthMm,
                    fMaxMach:          fMach,
                    oFrame:            oFrame,
                    fWallThicknessMm:  Rocket.fWallThicknessMm,
                    fBluffnessRatio:   Rocket.fNoseBluffnessRatio,
                    fShoulderLengthMm: Rocket.fNoseShoulderLengthMm,
                    fShoulderRadiusMm: Rocket.NoseShoulderRadiusMm,
                    bShoulderCapped:   Rocket.bNoseShoulderCapped);

                Voxels voxNose = oNose.voxConstruct();
                Sh.PreviewVoxels(voxNose, clr);

                bool   bIsActualRegime = oNose.Regime == eActiveRegime;
                string strMarker       = bIsActualRegime
                    ? $"  ◀── matches active rocket (M = {Rocket.fMaxMach:0.00})"
                    : "";
                Library.Log(
                    $"  M = {fMach:0.0}  →  {oNose.Regime}{strMarker}");
            }

            if (Rocket.fNoseShoulderLengthMm > 0f)
            {
                Library.Log(
                    $"  shoulder: Ø{Rocket.fNoseShoulderDiameterMm:0.#} mm × " +
                    $"{Rocket.fNoseShoulderLengthMm:0.#} mm" +
                    (Rocket.bNoseShoulderCapped ? " (capped)" : " (open)"));
            }

            Library.Log("SmartNosecone showcase complete.");
        }
    }
}
