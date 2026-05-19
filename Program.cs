// =============================================================================
//  UCC Rocketry — Computational Engineering Model
//  -----------------------------------------------------------------------------
//  Entry point. Boots the PicoGK runtime and hands scene construction to
//  Scene.Build, then optionally builds solid fin geometry from Rockets.Active.
//
//  ┌────────────────────────────────────────────────────────────────────┐
//  │  Where do I edit … ?                                               │
//  ├────────────────────────────────────────────────────────────────────┤
//  │  · Rocket designs / toggles   → Rockets.cs (incl. pintle viewer)   │
//  │  · Voxel size (STL quality)   → runtimeVoxelSizeMm below (only edit here) │
//  │  · OpenRocket baselines       → Rockets.cs §1                       │
//  │  · Nosecone STL               → ExportNoseconeStl                     │
//  │  · Fin export STL             → ExportFinStl                        │
//  │  · Pintle injector STL        → ExportPintleInjectorStl             │
//  │  · Fin MDO + CAD              → Fins/SmartFinModule.cs            │
//  │  · Nosecone                   → Nosecone/SmartNosecone.cs        │
//  └────────────────────────────────────────────────────────────────────┘
//
//  Built on top of: PicoGK, LEAP71_ShapeKernel (+ HelixHeatX demo submodule).
// =============================================================================

using System.IO;
using System.Numerics;
using System.Reflection;
using PicoGK;
using Leap71.ShapeKernel;
using UCCRocketry.Fins;

namespace UCCRocketry
{
    internal static class Program
    {
        public static void Main()
        {
            // Smaller voxels reduce cross-bore artifacts; nominal hole sizes live in PintleInjector only.
            const float runtimeVoxelSizeMm = 0.25f;

            Library.Go(
                fVoxelSizeMM:   runtimeVoxelSizeMm,
                fnTask:         BuildSceneAndFins,
                strWindowTitle: "UCC Rocketry — Computational Engineering Model");
        }

        static void BuildSceneAndFins()
        {
            Scene.Build();

            // =========================================================
            // PHASE 3: ARGES PDE INJECTOR
            // =========================================================
            if (Rockets.ShowPintleInjector)
            {
                var oInjector = new UCCRocketry.Engines.PintleInjector();
                Voxels voxInjectorLocal = oInjector.voxConstruct();

                if (Rockets.ExportPintleInjectorStl)
                    ExportPintleInjectorStl(voxInjectorLocal);

                // Move the injector down the Z-axis by 600mm so it sits inside the tube
                Vector3 vecInjectorShiftMm = new Vector3(0, 0, 600f);
                Voxels voxInjectorTranslated = MeshUtility.voxApplyTransformation(
                    voxInjectorLocal,
                    vecPt => vecPt + vecInjectorShiftMm);

                // Preview in scene coordinates (+600 mm Z vs CAD export).
                Sh.PreviewVoxels(voxInjectorTranslated, Cp.clrRock, 1.0f);
            }
            else
            {
                Library.Log("Skipping pintle injector (Rockets.ShowPintleInjector = false).");
            }
            // =========================================================

            if (!Rockets.ShowFins)
            {
                Library.Log("Skipping fins (Rockets.ShowFins = false).");
                return;
            }

            RocketParameters rk = Rockets.Active;
            var oFins = new SmartFinModule(rk);
            oFins.OptimizeFinDimensions();

            Vector3 finDelta = oFins.FinAssemblyWorldTranslationMm();

            Library.Log(
                $"SmartFinModule (M={rk.fMaxMach:0.##}, Ø{rk.fOuterDiameterMm:0.#} mm, L_body={rk.fBodyLengthMm:0.#} mm, " +
                $"ρ_fin={rk.fFinMaterialDensityKgM3:0.#} kg/m³): " +
                $"semi-span = {oFins.OptimalSemiSpanMm:0.#} mm (floor {oFins.MinSemiSpanAeroMm:0.#} = {rk.fMinSemiSpanRatio:0.##}×Ø), " +
                $"root chord = {oFins.RootChordMm:0.#} mm ({rk.fMinRootChordRatio:0.##}×Ø), " +
                $"tip chord = {0.52f * oFins.RootChordMm:0.#} mm, m_fin×4 ≈ {oFins.EstimatedTotalFinMassKg:0.###} kg, " +
                $"SM = {oFins.LastStaticMarginCalib:0.###} cal ((CP−CG)/Ø), " +
                $"CG_combo ≈ {oFins.CombinedCgMmFromNose:0.#} mm, CP_combo ≈ {oFins.CombinedCpMmFromNose:0.#} mm (nose); " +
                $"fin C_Nα ≈ {oFins.LastFinSetCNa:0.##} vs body {rk.fBodyNoFinsCNa:0.#}; " +
                $"pose ΔZ = {finDelta.Z:0.#} mm.");

            Voxels voxFinAssembly = oFins.VoxConstruct();
            voxFinAssembly = MeshUtility.voxApplyTransformation(voxFinAssembly, p => p + finDelta);

            if (Rockets.FinPreviewTransparent)
            {
                Sh.PreviewVoxels(voxFinAssembly, Cp.clrRock, Rockets.FinPreviewTransparencyAlpha);
                Library.Log($"Fin assembly: transparency = {Rockets.FinPreviewTransparencyAlpha:0.##}.");
            }
            else
                Sh.PreviewVoxels(voxFinAssembly, Cp.clrRock);

            if (Rockets.ExportFinStl)
                ExportFinStl(voxFinAssembly);
        }

        static void ExportPintleInjectorStl(Voxels voxInjectorCadFrame)
        {
            string name = ResolveActiveRocketName();
            Directory.CreateDirectory("exports");
            string path = Path.Combine("exports", $"{name}_pintle_injector.stl");
            Sh.ExportVoxelsToSTLFile(voxInjectorCadFrame, path);
            Library.Log($"STL export (pintle, CAD frame Z=0 at bulkhead): {Path.GetFullPath(path)}.");
        }

        static void ExportFinStl(Voxels voxAssemblyWorld)
        {
            string name = ResolveActiveRocketName();
            Directory.CreateDirectory("exports");
            string pathAsm = Path.Combine("exports", $"{name}_fin_assembly.stl");
            Sh.ExportVoxelsToSTLFile(voxAssemblyWorld, pathAsm);
            Library.Log($"STL export: {Path.GetFullPath(pathAsm)}.");
        }

        static string ResolveActiveRocketName()
        {
            foreach (FieldInfo field in typeof(Rockets).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.FieldType != typeof(RocketParameters)) continue;
                if (field.Name == nameof(Rockets.Active)) continue;
                if (ReferenceEquals(field.GetValue(null), Rockets.Active))
                    return field.Name;
            }
            return "rocket";
        }
        
    }
    
}


