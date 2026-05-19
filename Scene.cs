// =============================================================================
//  UCC Rocketry — Scene
//  -----------------------------------------------------------------------------
//  Engine plumbing that turns the active rocket (Rockets.Active) into PicoGK
//  voxels and previews them in the viewer. You should never need to edit this
//  file in normal use.
//
//  · To change WHICH rocket is rendered, edit Rockets.cs §2 (Active).
//  · To toggle which parts render (body / nose / fins / pintle injector), edit Rockets.cs §3.
//  · To change HOW the nose is generated, edit Nosecone/SmartNosecone.cs.
// =============================================================================

using System.IO;
using System.Numerics;
using System.Reflection;
using PicoGK;
using Leap71.ShapeKernel;
using UCCRocketry.Nosecone;

namespace UCCRocketry
{
    /// <summary>
    /// PicoGK construction task entry-point. Composes the scene from
    /// Rockets.Active and the Rockets.* render toggles.
    /// </summary>
    internal static class Scene
    {
        /// <summary>
        /// Build callback invoked inside the PicoGK runtime by Program.Main.
        /// </summary>
        public static void Build()
        {
            try
            {
                RocketParameters rocket = Rockets.Active;

                Library.Log(
                    $"Rocket configuration: " +
                    $"Ø{rocket.fOuterDiameterMm:0.#} mm × " +
                    $"{rocket.TotalLengthMm:0.#} mm total " +
                    $"(body {rocket.fBodyLengthMm:0.#} + nose {rocket.EffectiveNoseLengthMm:0.#}" +
                    NoseLengthAnnotation(rocket) + "), " +
                    $"design M = {rocket.fMaxMach:0.00}, " +
                    $"Library voxel = {Library.fVoxelSizeMM:0.###} mm.");

                if (Rockets.ShowBodyTube)
                {
                    BuildRocketBody(rocket);
                }

                if (Rockets.ShowNosecone)
                {
                    BuildActiveNose(rocket);
                }
                else
                {
                    Library.Log("Skipping nosecone (Rockets.ShowNosecone = false).");
                }

                if (!Rockets.ShowBodyTube && !Rockets.ShowNosecone)
                {
                    Library.Log(
                        "Scene: body tube and nosecone are both off — only fins can add geometry (if ShowFins).");
                }
            }
            catch (Exception ex)
            {
                Library.Log($"Scene construction failed: {ex}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Annotate the nose length in the startup log to make it obvious   //
        //  whether the auto-derive is in effect, the manual value is in     //
        //  use, or the auto-derive has overridden a manual value the user   //
        //  left in source for documentation.                                 //
        // ------------------------------------------------------------------ //
        private static string NoseLengthAnnotation(RocketParameters rocket)
        {
            // Manual value present, no auto-override → straightforward case.
            if (!rocket.NoseLengthIsAuto) return " manual";

            // Auto in effect. Two sub-cases worth distinguishing.
            if (rocket.fNoseLengthMm > 0f)
            {
                // User has a documented manual value but flipped bAutoNoseLength
                // — show both so the comparison is obvious in the console.
                return $" auto, override of manual {rocket.fNoseLengthMm:0.#} mm";
            }
            return " auto";
        }

        // ------------------------------------------------------------------ //
        //  Rocket body (cylinder, parked behind the nosecone)                //
        //  Only invoked when Rockets.ShowBodyTube = true.                    //
        // ------------------------------------------------------------------ //
        private static void BuildRocketBody(RocketParameters rocket)
        {
            Library.Log("Building rocket body...");

            LocalFrame oFrame = new LocalFrame(new Vector3(0f, -300f, 0f));

            BaseCylinder oRocketBody = new BaseCylinder(
                oFrame:  oFrame,
                fLength: rocket.fBodyLengthMm,
                fRadius: rocket.OuterRadiusMm);

            Voxels voxRocketBody = oRocketBody.voxConstruct();
            Sh.PreviewVoxels(voxRocketBody, Cp.clrRock);

            Library.Log(
                $"  rocket body voxelised: " +
                $"Ø{rocket.fOuterDiameterMm:0.#} mm × {rocket.fBodyLengthMm:0.#} mm.");
        }

        // ------------------------------------------------------------------ //
        //  SmartNosecone — the one the active rocket actually flies          //
        //  ----------------------------------------------------------------  //
        //  The nose is centred on the LocalFrame origin. The shape is        //
        //  selected automatically from rocket.fMaxMach by SmartNosecone's    //
        //  internal regime selector — see Nosecone/SmartNosecone.cs.         //
        // ------------------------------------------------------------------ //
        private static void BuildActiveNose(RocketParameters rocket)
        {
            Library.Log("Building active SmartNosecone...");

            SmartNosecone oNose = new SmartNosecone(
                fBaseRadiusMm:     rocket.OuterRadiusMm,
                fLengthMm:         rocket.EffectiveNoseLengthMm,
                fMaxMach:          rocket.fMaxMach,
                fWallThicknessMm:  rocket.fWallThicknessMm,
                fBluffnessRatio:   rocket.fNoseBluffnessRatio,
                fShoulderLengthMm: rocket.EffectiveNoseShoulderLengthMm,
                fShoulderRadiusMm: rocket.NoseShoulderRadiusMm,
                bShoulderCapped:   rocket.bNoseShoulderCapped);

            Voxels voxNose = oNose.voxConstruct();
            Sh.PreviewVoxels(voxNose, Cp.clrRandom());

            Library.Log(
                $"  M = {rocket.fMaxMach:0.00}  →  {oNose.Regime}");

            if (oNose.IsShouldered)
            {
                Library.Log(
                    $"  shoulder: Ø{2f * oNose.ShoulderRadiusMm:0.#} mm × " +
                    $"{oNose.ShoulderLengthMm:0.#} mm" +
                    (rocket.bNoseShoulderCapped ? " (capped)" : " (open)"));
            }

            if (Rockets.ExportStl)
            {
                ExportNoseToStl(voxNose);
            }

            Library.Log("SmartNosecone build complete.");
        }

        // ------------------------------------------------------------------ //
        //  STL export                                                        //
        //  ----------------------------------------------------------------  //
        //  Writes the nose voxels to a binary STL named after the active     //
        //  rocket. Always overwrites — the latest geometry sits at a stable  //
        //  path so a slicer left open can simply reload it.                  //
        // ------------------------------------------------------------------ //
        private static void ExportNoseToStl(Voxels voxNose)
        {
            string strRocketName = ResolveActiveRocketName();
            string strDir        = "exports";
            string strPath       = Path.Combine(strDir, $"{strRocketName}_nose.stl");

            Directory.CreateDirectory(strDir);
            Sh.ExportVoxelsToSTLFile(voxNose, strPath);

            // Log absolute path + file size so the user can find it and
            // sanity-check the size against expected slicer load.
            try
            {
                long lBytes = new FileInfo(strPath).Length;
                Library.Log(
                    $"  STL: {Path.GetFullPath(strPath)} ({lBytes / 1024.0 / 1024.0:0.0} MB)");
            }
            catch
            {
                Library.Log($"  STL: {Path.GetFullPath(strPath)}");
            }
        }

        // ------------------------------------------------------------------ //
        //  Reflectively look up which Rockets.* field holds the same         //
        //  RocketParameters reference as Rockets.Active. This lets the       //
        //  exported file inherit the rocket's source-code name without       //
        //  forcing the user to maintain a parallel "ActiveName" string in    //
        //  Rockets.cs that could drift out of sync with Active.              //
        // ------------------------------------------------------------------ //
        private static string ResolveActiveRocketName()
        {
            foreach (FieldInfo field in typeof(Rockets)
                .GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(RocketParameters)) continue;
                if (field.Name == nameof(Rockets.Active))        continue;
                if (ReferenceEquals(field.GetValue(null), Rockets.Active))
                {
                    return field.Name;
                }
            }
            // Fallback — Active points at an inline-constructed RocketParameters
            // with no static field backing it, so we have no source-code name.
            return "rocket";
        }
    }
}
