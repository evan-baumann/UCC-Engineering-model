using System.Globalization;
using PicoGK;
using Tintreach.Engines;
using Tintreach.Engines.Combustors;
using Tintreach.Engines.Nozzles;

namespace Tintreach
{
    internal enum HeadlessComponent
    {
        PintleInjector,
        SchelkinSpiral,
    }

    internal sealed class HeadlessCliOptions
    {
        public HeadlessComponent Component { get; set; } = HeadlessComponent.PintleInjector;
        public float VoxelSizeMm { get; set; } = 0.25f;

        // Pintle injector
        public float TubeIdMm { get; set; } = 70.0f;
        public float BulkheadThicknessMm { get; set; } = 35.0f;
        public float PintleDiameterMm { get; set; } = 15.0f;
        public float PintleProtrusionMm { get; set; } = 20.0f;
        public float PropaneChannelDiamMm { get; set; } = 9.0f;
        public int PropanePorts { get; set; } = 8;
        public float PropanePortDiamMm { get; set; } = 3.1f;
        public float GoxAnnulusIdMm { get; set; } = 15.0f;
        public float GoxAnnulusOdMm { get; set; } = 23.3f;

        // PDE combustor chamber
        public float ChamberIdMm { get; set; } = 50.0f;
        public float ChamberLengthMm { get; set; } = 300.0f;
        public float ChamberWallThicknessMm { get; set; } = 3.0f;

        // Shchelkin spiral
        public float SpiralLengthMm { get; set; } = 300.0f;
        public bool SpiralPitchSpecified { get; set; }
        public float SpiralPitchMm { get; set; }
        public float SpiralBlockingRatio { get; set; } = 0.45f;

        // Divergent nozzle (included in --schelkin-spiral unless --no-nozzle)
        public bool IncludeDivergentNozzle { get; set; } = true;

        // Pintle at combustor inlet (included in --schelkin-spiral unless --no-pintle)
        public bool IncludePintleInjector { get; set; } = true;
        public float SpiralClearanceAfterPintleMm { get; set; } = 5.0f;

        public float NozzleExitDiameterMm { get; set; } = 80.0f;
        public float NozzleLengthMm { get; set; } = 80.0f;
        public float NozzleWallThicknessMm { get; set; } = 4.0f;
        public float NozzleSleeveLengthMm { get; set; } = 20.0f;

        public PintleInjector ToInjector() =>
            new PintleInjector(
                fTubeID: TubeIdMm,
                fBulkheadThickness: BulkheadThicknessMm,
                fPintleDiameter: PintleDiameterMm,
                fPintleProtrusion: PintleProtrusionMm,
                fPropaneChannelDiam: PropaneChannelDiamMm,
                nPropanePorts: PropanePorts,
                fPropanePortDiam: PropanePortDiamMm,
                fGoxAnnulusID: GoxAnnulusIdMm,
                fGoxAnnulusOD: GoxAnnulusOdMm);

        public CylindricalCombustionChamber ToChamber() =>
            new CylindricalCombustionChamber(
                fTubeInnerDiameter: ChamberIdMm,
                fChamberLength: ChamberLengthMm,
                fWallThickness: ChamberWallThicknessMm);

        public ShchelkinSpiral ToSpiral(CylindricalCombustionChamber oChamber, float fStartOffsetZ = 0f) =>
            new ShchelkinSpiral(
                oChamber,
                fSpiralLength: SpiralLengthMm,
                fBlockingRatio: SpiralBlockingRatio,
                fPitchMm: SpiralPitchSpecified ? SpiralPitchMm : null,
                fStartOffsetZ: fStartOffsetZ);

        public float ComputeSpiralStartZ()
        {
            if (!IncludePintleInjector)
                return 0f;

            PintleInjector oInjector = ToInjector();
            return oInjector.PintleTipZMm() + SpiralClearanceAfterPintleMm;
        }

        public DivergentNozzle ToNozzle(CylindricalCombustionChamber oChamber) =>
            new DivergentNozzle(
                oChamber,
                fExitRadius: NozzleExitDiameterMm / 2f,
                fNozzleLength: NozzleLengthMm,
                fWallThickness: NozzleWallThicknessMm,
                fSleeveLength: NozzleSleeveLengthMm);

        public Voxels BuildPdeCombustor()
        {
            CylindricalCombustionChamber oChamber = ToChamber();
            float fChamberOriginZ = 0f;
            Voxels voxEngine = new Voxels();

            if (IncludePintleInjector)
            {
                PintleInjector oInjector = ToInjector();
                voxEngine += oInjector.voxConstruct();
                fChamberOriginZ = oInjector.CombustorInletZMm();
            }

            voxEngine += oChamber.voxConstructAt(fChamberOriginZ);

            float fSpiralStartZ = ComputeSpiralStartZ();
            ShchelkinSpiral oSpiral = ToSpiral(oChamber, fSpiralStartZ);
            voxEngine += oSpiral.voxConstruct();

            if (IncludeDivergentNozzle)
                voxEngine += ToNozzle(oChamber).voxConstructMountedOn(oChamber, fChamberOriginZ);

            return voxEngine;
        }

        public void PrintSummary()
        {
            Console.WriteLine($"Headless component: {Component}");
            Console.WriteLine($"  voxel-size = {VoxelSizeMm:0.###}");

            if (Component == HeadlessComponent.PintleInjector)
            {
                Console.WriteLine("Pintle injector parameters (mm unless noted):");
                Console.WriteLine($"  tube-id              = {TubeIdMm:0.###}");
                Console.WriteLine($"  bulkhead-thickness   = {BulkheadThicknessMm:0.###}");
                Console.WriteLine($"  pintle-diameter      = {PintleDiameterMm:0.###}");
                Console.WriteLine($"  pintle-protrusion    = {PintleProtrusionMm:0.###}");
                Console.WriteLine($"  propane-channel-diam = {PropaneChannelDiamMm:0.###}");
                Console.WriteLine($"  propane-ports        = {PropanePorts}");
                Console.WriteLine($"  propane-port-diam    = {PropanePortDiamMm:0.###}");
                Console.WriteLine($"  gox-annulus-id       = {GoxAnnulusIdMm:0.###}");
                Console.WriteLine($"  gox-annulus-od       = {GoxAnnulusOdMm:0.###}");
                return;
            }

            CylindricalCombustionChamber oChamber = ToChamber();
            float fSpiralStartZ = ComputeSpiralStartZ();
            ShchelkinSpiral oSpiral = ToSpiral(oChamber, fSpiralStartZ);

            Console.WriteLine("PDE combustor chamber (mm unless noted):");
            Console.WriteLine($"  chamber-id             = {ChamberIdMm:0.###}");
            Console.WriteLine($"  chamber-length         = {ChamberLengthMm:0.###}");
            Console.WriteLine($"  chamber-wall-thickness = {ChamberWallThicknessMm:0.###}");

            if (IncludePintleInjector)
            {
                PintleInjector oInjector = ToInjector();
                Console.WriteLine("Pintle injector (inlet, Z=0):");
                Console.WriteLine($"  tube-id                = {TubeIdMm:0.###}");
                Console.WriteLine($"  bulkhead-thickness     = {BulkheadThicknessMm:0.###}");
                Console.WriteLine($"  pintle-diameter        = {PintleDiameterMm:0.###}");
                Console.WriteLine($"  pintle-protrusion      = {PintleProtrusionMm:0.###}");
                Console.WriteLine($"  derived pintle-tip-z   = {oInjector.PintleTipZMm():0.###}");
                Console.WriteLine($"  spiral-clearance       = {SpiralClearanceAfterPintleMm:0.###}");
            }
            else
            {
                Console.WriteLine("Pintle injector: omitted (--no-pintle).");
            }

            Console.WriteLine("Shchelkin spiral:");
            Console.WriteLine($"  spiral-start-z         = {fSpiralStartZ:0.###}");
            Console.WriteLine($"  spiral-length          = {SpiralLengthMm:0.###}");
            Console.WriteLine($"  spiral-pitch           = {(SpiralPitchSpecified ? SpiralPitchMm.ToString("0.###", CultureInfo.InvariantCulture) : $"{oChamber.DefaultPitchMm:0.###} (default = chamber-id)")}");
            Console.WriteLine($"  spiral-blocking-ratio  = {SpiralBlockingRatio:0.###}");
            Console.WriteLine($"  derived wire-diameter  = {oSpiral.WireDiameterMm:0.###}");
            Console.WriteLine($"  derived turn-count     = {oSpiral.TurnCount:0.##}");

            if (!IncludeDivergentNozzle)
            {
                Console.WriteLine("Divergent nozzle: omitted (--no-nozzle).");
                return;
            }

            DivergentNozzle oNozzle = ToNozzle(oChamber);
            Console.WriteLine("Divergent nozzle:");
            Console.WriteLine($"  nozzle-exit-diameter   = {NozzleExitDiameterMm:0.###}");
            Console.WriteLine($"  nozzle-length          = {NozzleLengthMm:0.###}");
            Console.WriteLine($"  nozzle-wall-thickness  = {NozzleWallThicknessMm:0.###}");
            Console.WriteLine($"  nozzle-sleeve-length   = {NozzleSleeveLengthMm:0.###}");
            Console.WriteLine($"  mount-origin-z         = {oNozzle.MountOriginZMm(oChamber):0.###}");
        }

        public static void PrintUsage()
        {
            Console.WriteLine("""
                Tintreach headless CLI

                Usage:
                  dotnet run -- --headless [--pintle-injector | --schelkin-spiral] [options]

                Modes (pick one; default is pintle injector):
                  --pintle-injector          Generate pintle injector STL (default)
                  --schelkin-spiral          Generate PDE combustor + Shchelkin spiral STL

                Shared:
                  --headless                 Run without opening the 3D viewer
                  --help, -h                 Show this help text
                  --voxel-size <mm>          PicoGK voxel size / STL resolution (default 0.25)

                Pintle injector flags:
                  --tube-id <mm>             Motor tube inner diameter (default 70)
                  --bulkhead-thickness <mm>  Bulkhead thickness (default 35)
                  --pintle-diameter <mm>     Pintle post diameter (default 15)
                  --pintle-protrusion <mm>   Pintle post length above bulkhead (default 20)
                  --propane-channel-diam <mm> Central propane bore diameter (default 9)
                  --propane-ports <count>    Number of radial propane ports (default 8)
                  --propane-port-diam <mm>   Radial propane port diameter (default 3.1)
                  --gox-annulus-id <mm>      GOX annulus inner diameter (default 15)
                  --gox-annulus-od <mm>      GOX annulus outer diameter (default 23.3)

                PDE combustor / Shchelkin spiral flags:
                  --chamber-id <mm>          Combustor tube inner diameter (default 50)
                  --chamber-length <mm>      Combustor tube axial length (default 300)
                  --chamber-wall-thickness <mm> Tube wall thickness (default 3)
                  --spiral-length <mm>       Shchelkin wire section length (default 300)
                  --spiral-pitch <mm>        Helix pitch (default: chamber inner diameter)
                  --spiral-blocking-ratio <0-1> Target blockage ratio (default 0.45)
                  --spiral-clearance-after-pintle <mm> Gap after pintle tip before spiral (default 5)
                  --no-pintle                Omit pintle from PDE assembly export
                  --no-nozzle                Export without divergent nozzle
                  (Pintle injector flags above also apply to --schelkin-spiral when pintle is included.)

                Divergent nozzle flags:
                  --nozzle-exit-diameter <mm>  Nozzle exit inner diameter (default 80)
                  --nozzle-length <mm>       Diverging section length (default 80)
                  --nozzle-wall-thickness <mm> Nozzle structural wall (default 4)
                  --nozzle-sleeve-length <mm>  Sleeve overlap on combustor OD (default 20)

                Examples:
                  dotnet run -- --headless --propane-ports 12 --pintle-diameter 16
                  dotnet run -- --headless --schelkin-spiral --chamber-id 50 --chamber-wall-thickness 4
                """);
        }

        public static bool TryParse(string[] args, out HeadlessCliOptions? options, out string? error)
        {
            options = new HeadlessCliOptions();
            error = null;

            bool bHeadless = false;
            bool bPintle = false;
            bool bSchelkin = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (arg is "--help" or "-h")
                {
                    PrintUsage();
                    options = null;
                    return true;
                }

                if (arg == "--headless")
                {
                    bHeadless = true;
                    continue;
                }

                if (arg == "--pintle-injector")
                {
                    bPintle = true;
                    continue;
                }

                if (arg == "--schelkin-spiral")
                {
                    bSchelkin = true;
                    continue;
                }

                if (arg == "--no-nozzle")
                {
                    options.IncludeDivergentNozzle = false;
                    continue;
                }

                if (arg == "--no-pintle")
                {
                    options.IncludePintleInjector = false;
                    continue;
                }

                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    error = $"Unknown argument '{arg}'. Use --help for usage.";
                    options = null;
                    return false;
                }

                if (!TryGetFlagValue(args, ref i, arg, out string value))
                {
                    error = $"Missing value for '{arg}'. Use --help for usage.";
                    options = null;
                    return false;
                }

                if (!TryApplyFlag(options, arg, value, out string? applyError))
                {
                    error = applyError;
                    options = null;
                    return false;
                }
            }

            if (!bHeadless)
            {
                options = null;
                return true;
            }

            if (bPintle && bSchelkin)
            {
                error = "Choose only one mode: --pintle-injector or --schelkin-spiral.";
                options = null;
                return false;
            }

            if (bSchelkin)
                options.Component = HeadlessComponent.SchelkinSpiral;

            if (!Validate(options, out error))
            {
                options = null;
                return false;
            }

            return true;
        }

        static bool TryGetFlagValue(string[] args, ref int index, string flag, out string value)
        {
            value = string.Empty;

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                return false;

            value = args[++index];
            return true;
        }

        static bool TryApplyFlag(HeadlessCliOptions options, string flag, string value, out string? error)
        {
            error = null;

            switch (flag)
            {
                case "--tube-id":
                    if (!TryParseFloat(value, out float tubeId)) { error = GetParseError(flag, value); return false; }
                    options.TubeIdMm = tubeId;
                    return true;
                case "--bulkhead-thickness":
                    if (!TryParseFloat(value, out float bulkhead)) { error = GetParseError(flag, value); return false; }
                    options.BulkheadThicknessMm = bulkhead;
                    return true;
                case "--pintle-diameter":
                    if (!TryParseFloat(value, out float pintleDiam)) { error = GetParseError(flag, value); return false; }
                    options.PintleDiameterMm = pintleDiam;
                    return true;
                case "--pintle-protrusion":
                    if (!TryParseFloat(value, out float protrusion)) { error = GetParseError(flag, value); return false; }
                    options.PintleProtrusionMm = protrusion;
                    return true;
                case "--propane-channel-diam":
                    if (!TryParseFloat(value, out float channelDiam)) { error = GetParseError(flag, value); return false; }
                    options.PropaneChannelDiamMm = channelDiam;
                    return true;
                case "--propane-ports":
                    if (!TryParseInt(value, out int ports)) { error = GetParseError(flag, value); return false; }
                    options.PropanePorts = ports;
                    return true;
                case "--propane-port-diam":
                    if (!TryParseFloat(value, out float portDiam)) { error = GetParseError(flag, value); return false; }
                    options.PropanePortDiamMm = portDiam;
                    return true;
                case "--gox-annulus-id":
                    if (!TryParseFloat(value, out float goxId)) { error = GetParseError(flag, value); return false; }
                    options.GoxAnnulusIdMm = goxId;
                    return true;
                case "--gox-annulus-od":
                    if (!TryParseFloat(value, out float goxOd)) { error = GetParseError(flag, value); return false; }
                    options.GoxAnnulusOdMm = goxOd;
                    return true;
                case "--chamber-id":
                    if (!TryParseFloat(value, out float chamberId)) { error = GetParseError(flag, value); return false; }
                    options.ChamberIdMm = chamberId;
                    return true;
                case "--chamber-length":
                    if (!TryParseFloat(value, out float chamberLength)) { error = GetParseError(flag, value); return false; }
                    options.ChamberLengthMm = chamberLength;
                    return true;
                case "--chamber-wall-thickness":
                    if (!TryParseFloat(value, out float wallThickness)) { error = GetParseError(flag, value); return false; }
                    options.ChamberWallThicknessMm = wallThickness;
                    return true;
                case "--spiral-length":
                    if (!TryParseFloat(value, out float spiralLength)) { error = GetParseError(flag, value); return false; }
                    options.SpiralLengthMm = spiralLength;
                    return true;
                case "--spiral-pitch":
                    if (!TryParseFloat(value, out float pitch)) { error = GetParseError(flag, value); return false; }
                    options.SpiralPitchSpecified = true;
                    options.SpiralPitchMm = pitch;
                    return true;
                case "--spiral-blocking-ratio":
                    if (!TryParseFloat(value, out float blockingRatio)) { error = GetParseError(flag, value); return false; }
                    options.SpiralBlockingRatio = blockingRatio;
                    return true;
                case "--spiral-clearance-after-pintle":
                    if (!TryParseFloat(value, out float clearance)) { error = GetParseError(flag, value); return false; }
                    options.SpiralClearanceAfterPintleMm = clearance;
                    return true;
                case "--nozzle-exit-diameter":
                    if (!TryParseFloat(value, out float exitDiameter)) { error = GetParseError(flag, value); return false; }
                    options.NozzleExitDiameterMm = exitDiameter;
                    return true;
                case "--nozzle-length":
                    if (!TryParseFloat(value, out float nozzleLength)) { error = GetParseError(flag, value); return false; }
                    options.NozzleLengthMm = nozzleLength;
                    return true;
                case "--nozzle-wall-thickness":
                    if (!TryParseFloat(value, out float nozzleWall)) { error = GetParseError(flag, value); return false; }
                    options.NozzleWallThicknessMm = nozzleWall;
                    return true;
                case "--nozzle-sleeve-length":
                    if (!TryParseFloat(value, out float sleeveLength)) { error = GetParseError(flag, value); return false; }
                    options.NozzleSleeveLengthMm = sleeveLength;
                    return true;
                case "--voxel-size":
                    if (!TryParseFloat(value, out float voxelSize)) { error = GetParseError(flag, value); return false; }
                    options.VoxelSizeMm = voxelSize;
                    return true;
                default:
                    error = $"Unknown option '{flag}'. Use --help for usage.";
                    return false;
            }
        }

        static bool Validate(HeadlessCliOptions options, out string? error)
        {
            if (options.VoxelSizeMm <= 0f)
            {
                error = "voxel-size must be greater than 0.";
                return false;
            }

            if (options.Component == HeadlessComponent.PintleInjector)
                return ValidatePintle(options, out error);

            if (options.ChamberIdMm <= 0f ||
                options.ChamberLengthMm <= 0f ||
                options.ChamberWallThicknessMm <= 0f)
            {
                error = "Chamber dimensions and wall thickness must be greater than 0.";
                return false;
            }

            if (options.SpiralLengthMm <= 0f)
            {
                error = "spiral-length must be greater than 0.";
                return false;
            }

            if (options.SpiralPitchSpecified && options.SpiralPitchMm <= 0f)
            {
                error = "spiral-pitch must be greater than 0.";
                return false;
            }

            if (options.SpiralBlockingRatio <= 0f || options.SpiralBlockingRatio >= 1f)
            {
                error = "spiral-blocking-ratio must be between 0 and 1.";
                return false;
            }

            if (options.SpiralClearanceAfterPintleMm < 0f)
            {
                error = "spiral-clearance-after-pintle must be non-negative.";
                return false;
            }

            if (options.IncludeDivergentNozzle)
            {
                if (options.NozzleExitDiameterMm <= 0f ||
                    options.NozzleLengthMm <= 0f ||
                    options.NozzleWallThicknessMm <= 0f ||
                    options.NozzleSleeveLengthMm <= 0f)
                {
                    error = "Nozzle dimensions must be greater than 0.";
                    return false;
                }

                if (options.NozzleExitDiameterMm <= options.ChamberIdMm)
                {
                    error = "nozzle-exit-diameter must exceed chamber-id.";
                    return false;
                }
            }

            try
            {
                CylindricalCombustionChamber oChamber = options.ToChamber();
                _ = options.ToSpiral(oChamber, options.ComputeSpiralStartZ());
                if (options.IncludeDivergentNozzle)
                    _ = options.ToNozzle(oChamber);
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }

            error = null;
            return true;
        }

        static bool ValidatePintle(HeadlessCliOptions options, out string? error)
        {
            if (options.TubeIdMm <= 0f ||
                options.BulkheadThicknessMm <= 0f ||
                options.PintleDiameterMm <= 0f ||
                options.PintleProtrusionMm <= 0f ||
                options.PropaneChannelDiamMm <= 0f ||
                options.PropanePortDiamMm <= 0f ||
                options.GoxAnnulusIdMm <= 0f ||
                options.GoxAnnulusOdMm <= 0f)
            {
                error = "All dimensional inputs must be greater than 0.";
                return false;
            }

            if (options.PropanePorts < 1)
            {
                error = "propane-ports must be at least 1.";
                return false;
            }

            if (options.GoxAnnulusIdMm >= options.GoxAnnulusOdMm)
            {
                error = "gox-annulus-id must be less than gox-annulus-od.";
                return false;
            }

            error = null;
            return true;
        }

        static bool TryParseFloat(string value, out float result) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result);

        static bool TryParseInt(string value, out int result) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result);

        static string GetParseError(string flag, string value) =>
            $"Could not parse '{value}' for '{flag}'. Use invariant numbers like 70 or 3.1.";
    }
}
