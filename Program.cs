// =============================================================================
//  UCC Rocketry – Computational Engineering Model
//  -----------------------------------------------------------------------------
//  Entry point. Boots the PicoGK runtime (voxel kernel + viewer) and renders a
//  placeholder rocket body: a Ø100 mm × 200 mm cylinder.
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

namespace UCCRocketry
{
    internal static class Program
    {
        // Voxel resolution in millimetres. Smaller = finer surface, slower build
        // and more RAM. 0.5 mm is a good first-pass for parts of this scale.
        private const float VoxelSizeMm = 0.5f;

        // Rocket body geometry.
        private const float BodyRadiusMm = 50f;
        private const float BodyHeightMm = 200f;

        public static void Main()
        {
            // Library.Go boots the PicoGK runtime, opens the viewer window on
            // the main thread, and invokes BuildRocketBody on a worker thread.
            // Control returns here only when the viewer window is closed.
            PicoGK.Library.Go(
                fVoxelSizeMM:    VoxelSizeMm,
                fnTask:          BuildRocketBody,
                strWindowTitle:  "UCC Rocketry — Computational Engineering Model");
        }

        /// <summary>
        /// Construction task executed inside the PicoGK runtime. Builds the
        /// rocket body and pushes it to the viewer.
        /// </summary>
        private static void BuildRocketBody()
        {
            try
            {
                Library.Log("Building rocket body...");

                // Place the cylinder centred on the world origin. By default a
                // LocalFrame's local Z axis is the cylinder's length axis, so
                // the rocket points "up".
                LocalFrame oOrigin = new LocalFrame(Vector3.Zero);

                BaseCylinder oRocketBody = new BaseCylinder(
                    oFrame:  oOrigin,
                    fLength: BodyHeightMm,
                    fRadius: BodyRadiusMm);

                Voxels voxRocketBody = oRocketBody.voxConstruct();

                Sh.PreviewVoxels(voxRocketBody, Cp.clrRock);

                Library.Log(
                    $"Rocket body voxelised: " +
                    $"Ø{2f * BodyRadiusMm:0.#} mm × {BodyHeightMm:0.#} mm " +
                    $"@ {VoxelSizeMm:0.##} mm voxels.");
            }
            catch (Exception ex)
            {
                Library.Log($"Failed to build rocket body: {ex}");
            }
        }
    }
}
