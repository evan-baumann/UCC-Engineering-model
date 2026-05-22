// =============================================================================
//  Tintreach — Computational Design Suite
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
//  │  · PDE combustor STL          → ExportPdeCombustorStl               │
//  │  · Divergent nozzle           → ShowDivergentNozzle (§3)           │
//  │  · Fin MDO + CAD              → Fins/SmartFinModule.cs            │
//  │  · Nosecone                   → Nosecone/SmartNosecone.cs        │
//  └────────────────────────────────────────────────────────────────────┘
//
//  Built on top of: PicoGK, LEAP71_ShapeKernel (+ HelixHeatX demo submodule).
// =============================================================================

using System.IO;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using PicoGK;
using Leap71.ShapeKernel;
using Tintreach.Engines;
using Tintreach.Engines.Combustors;
using Tintreach.Engines.Nozzles;
using Tintreach.Fins;

namespace Tintreach
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        static extern void ExitProcess(uint uExitCode);

        static HeadlessCliOptions? s_headlessOptions;

        public static void Main(string[] args)
        {
            if (!HeadlessCliOptions.TryParse(args, out HeadlessCliOptions? headlessOptions, out string? parseError))
            {
                Console.Error.WriteLine(parseError);
                Environment.Exit(1);
            }

            if (headlessOptions is null)
            {
                foreach (string arg in args)
                {
                    if (arg is "--help" or "-h")
                    {
                        Environment.Exit(0);
                        return;
                    }
                }

                RunGuiMode();
                return;
            }

            s_headlessOptions = headlessOptions;
            RunHeadlessMode(headlessOptions);
        }

        static void RunHeadlessMode(HeadlessCliOptions options)
        {
            Console.WriteLine("STARTING HEADLESS AI GENERATION...");
            options.PrintSummary();

            Library.Go(
                fVoxelSizeMM:   options.VoxelSizeMm,
                fnTask:         HeadlessGenerationTask,
                strWindowTitle: "Tintreach — Computational Design Suite");
        }

        static void RunGuiMode()
        {
            const float runtimeVoxelSizeMm = 0.25f;

            Library.Go(
                fVoxelSizeMM:   runtimeVoxelSizeMm,
                fnTask:         BuildSceneAndFins,
                strWindowTitle: "Tintreach — Computational Design Suite");
        }

        static void HeadlessGenerationTask()
        {
            HeadlessCliOptions options = s_headlessOptions
                ?? throw new InvalidOperationException("Headless options were not initialized.");

            string name = ResolveActiveRocketName();

            if (options.Component == HeadlessComponent.SchelkinSpiral)
            {
                Console.WriteLine("Generating PDE engine (combustor" +
                    (options.IncludePintleInjector ? " + pintle" : "") +
                    " + spiral" +
                    (options.IncludeDivergentNozzle ? " + nozzle)..." : ")..."));

                Voxels voxCombustor = options.BuildPdeCombustor();
                ExportPdeCombustorStl(voxCombustor);

                string exportPath = Path.Combine("exports", $"{name}_pde_combustor.stl");
                Console.WriteLine($"SUCCESS: Saved to {exportPath}");
            }
            else
            {
                Console.WriteLine("Generating pintle injector...");

                var oInjector = options.ToInjector();
                Voxels voxInjector = oInjector.voxConstruct();

                ExportPintleInjectorStl(voxInjector);

                string exportPath = Path.Combine("exports", $"{name}_pintle_injector.stl");
                Console.WriteLine($"SUCCESS: Saved to {exportPath}");
            }

            Library.Log("Headless generation complete.");

            new Thread(() =>
            {
                Thread.Sleep(500);
                ExitProcess(0);
            })
            { IsBackground = true }.Start();
        }

        static void BuildSceneAndFins()
        {
            Scene.Build();

            if (Rockets.ShowPintleInjector)
            {
                var oInjector = new PintleInjector();
                Voxels voxInjectorLocal = oInjector.voxConstruct();

                if (Rockets.ExportPintleInjectorStl)
                    ExportPintleInjectorStl(voxInjectorLocal);

                Vector3 vecInjectorShiftMm = new Vector3(0, 0, 600f);
                Voxels voxInjectorTranslated = MeshUtility.voxApplyTransformation(
                    voxInjectorLocal,
                    vecPt => vecPt + vecInjectorShiftMm);

                Sh.PreviewVoxels(voxInjectorTranslated, Cp.clrRock, 1.0f);
            }
            else
            {
                Library.Log("Skipping pintle injector (Rockets.ShowPintleInjector = false).");
            }

            if (Rockets.ShowPdeCombustor)
            {
                Voxels voxCombustorLocal = BuildPdeCombustorFromRockets();

                if (Rockets.ExportPdeCombustorStl)
                    ExportPdeCombustorStl(voxCombustorLocal);

                Vector3 vecCombustorShiftMm = new Vector3(0, 0, 900f);
                Voxels voxCombustorTranslated = MeshUtility.voxApplyTransformation(
                    voxCombustorLocal,
                    vecPt => vecPt + vecCombustorShiftMm);

                Sh.PreviewVoxels(voxCombustorTranslated, Cp.clrWarning, 1.0f);
            }
            else
            {
                Library.Log("Skipping PDE combustor (Rockets.ShowPdeCombustor = false).");
            }

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

        static Voxels BuildPdeCombustorFromRockets()
        {
            var oChamber = new CylindricalCombustionChamber(
                fTubeInnerDiameter: Rockets.PdeChamberInnerDiameterMm,
                fChamberLength: Rockets.PdeChamberLengthMm,
                fWallThickness: Rockets.PdeChamberWallThicknessMm);

            float fChamberOriginZ = 0f;
            float fSpiralStartZ = 0f;
            Voxels voxEngine = new Voxels();

            if (Rockets.ShowPdePintleInjector)
            {
                var oInjector = new PintleInjector();
                voxEngine += oInjector.voxConstruct();
                fChamberOriginZ = oInjector.CombustorInletZMm();
                fSpiralStartZ = oInjector.PintleTipZMm() + Rockets.PdeSpiralClearanceAfterPintleMm;
            }

            voxEngine += oChamber.voxConstructAt(fChamberOriginZ);

            float? fPitch = Rockets.PdeSpiralPitchMm > 0f ? Rockets.PdeSpiralPitchMm : null;
            var oSpiral = new ShchelkinSpiral(
                oChamber,
                Rockets.PdeSpiralLengthMm,
                Rockets.PdeSpiralBlockingRatio,
                fPitch,
                fSpiralStartZ);

            voxEngine += oSpiral.voxConstruct();

            if (Rockets.ShowDivergentNozzle)
            {
                var oNozzle = new DivergentNozzle(
                    oChamber,
                    fExitRadius: Rockets.PdeNozzleExitDiameterMm / 2f,
                    fNozzleLength: Rockets.PdeNozzleLengthMm,
                    fWallThickness: Rockets.PdeNozzleWallThicknessMm,
                    fSleeveLength: Rockets.PdeNozzleSleeveLengthMm);

                voxEngine += oNozzle.voxConstructMountedOn(oChamber, fChamberOriginZ);
            }

            return voxEngine;
        }

        static void ExportPintleInjectorStl(Voxels voxInjectorCadFrame)
        {
            string name = ResolveActiveRocketName();
            Directory.CreateDirectory("exports");
            string path = Path.Combine("exports", $"{name}_pintle_injector.stl");
            Sh.ExportVoxelsToSTLFile(voxInjectorCadFrame, path);
            Library.Log($"STL export (pintle, CAD frame Z=0 at bulkhead): {Path.GetFullPath(path)}.");
        }

        static void ExportPdeCombustorStl(Voxels voxCombustorCadFrame)
        {
            string name = ResolveActiveRocketName();
            Directory.CreateDirectory("exports");
            string path = Path.Combine("exports", $"{name}_pde_combustor.stl");
            Sh.ExportVoxelsToSTLFile(voxCombustorCadFrame, path);
            Library.Log($"STL export (PDE combustor, CAD frame Z=0 at inlet): {Path.GetFullPath(path)}.");
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
