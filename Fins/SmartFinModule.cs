// =============================================================================
//  Tintreach Propulsion — SmartFinModule (Solid, Fast, Crash-Proof)
// =============================================================================

using System;
using System.Collections.Generic;
using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

namespace Tintreach.Fins
{
    public sealed class SmartFinModule
    {
        public static class FinStructuralLimits
        {
            public const float MaxSemiSpanCapMm         = 450.0f;
            public const float MinPeakThicknessMm       = RocketParameters.FinModuleMinPeakThicknessMm;
            public const float MinEdgeThicknessMm       = RocketParameters.FinModuleMinEdgeThicknessMm;
        }

        public const float BodyAxisWorldY        = -300f;
        public const float SleeveWallThicknessMm = 2.0f;
        public const float WeepHoleRadiusMm      = 1.5f;
        public const float WeepHoleLengthMm      = 72.0f;
        public const float WeepHoleInboardRadialMm = 10.0f;
        public const int   WeepHoleCount         = 4;

        public static float TtwTabChordFraction => RocketParameters.FinModuleTtwTabChordFraction;

        private readonly RocketParameters m_rp;
        private readonly float            m_bodyRadiusMm;
        private readonly float            m_bodyLengthMm;
        private readonly float            m_tabDepthMm;

        public float OptimalSemiSpanMm       { get; private set; }
        public float LastStaticMarginCalib   { get; private set; }
        public float EstimatedFinMassKg      { get; private set; }
        public float CombinedCgMmFromNose    { get; private set; }
        public float FinCpMmFromNose         { get; private set; }
        public float CombinedCpMmFromNose    { get; private set; }
        public float LastFinSetCNa           { get; private set; }
        public float EstimatedTotalFinMassKg => 4f * EstimatedFinMassKg;

        public Vector3 FinAssemblyWorldTranslationMm()
        {
            float cRoot = RootChordMm;
            float zInternalRootLe = m_bodyLengthMm - cRoot;
            float zTargetWorld    = m_rp.fBodyLengthMm - cRoot;
            return new Vector3(0f, 0f, zTargetWorld - zInternalRootLe);
        }

        public SmartFinModule(RocketParameters rp)
        {
            m_rp           = rp;
            m_bodyRadiusMm = rp.OuterRadiusMm;
            m_bodyLengthMm = rp.fBodyLengthMm;
            m_tabDepthMm   = RocketParameters.FinModuleTtwTabRadialDepthMm;
        }

        float Mmax => m_rp.fMaxMach;
        FinAirfoilRegime Regime => m_rp.FinAirfoilRegimeForMach();
        bool IsRoundedSubsonic => Regime == FinAirfoilRegime.SubsonicRounded;

        float ThicknessRatioBase => Regime switch {
            FinAirfoilRegime.SubsonicRounded     => 0.08f,
            FinAirfoilRegime.TransonicHexagonal  => 0.06f,
            FinAirfoilRegime.SupersonicHexagonal => 0.05f,
            _                                    => 0.06f,
        };

        float XPeakInFrac  => RocketParameters.FinModuleAirfoilThickLocFrac;
        float XPeakOutFrac => XPeakInFrac + Regime switch {
            FinAirfoilRegime.SubsonicRounded     => 0.30f,
            FinAirfoilRegime.TransonicHexagonal  => RocketParameters.FinModuleAirfoilFlatTopFracTransonic,
            FinAirfoilRegime.SupersonicHexagonal => RocketParameters.FinModuleAirfoilFlatTopFracSupersonic,
            _                                    => 0.30f,
        };

        /// <summary>Root chord floor: OD × <see cref="RocketParameters.fMinRootChordRatio"/> (diameter-based, not radius).</summary>
        public float MinRootChordMm => m_rp.fOuterDiameterMm * m_rp.fMinRootChordRatio;

        /// <summary>Minimum exposed semi-span floor: OD × <see cref="RocketParameters.fMinSemiSpanRatio"/>.</summary>
        public float MinSemiSpanAeroMm => m_rp.fOuterDiameterMm * m_rp.fMinSemiSpanRatio;

        public float RootChordMm => MinRootChordMm;

        public float EffectiveThicknessRatio(float cRootMm)
        {
            float tauBase  = ThicknessRatioBase;
            float tauFloor = FinStructuralLimits.MinPeakThicknessMm / MathF.Max(cRootMm, MinRootChordMm);
            return MathF.Max(tauBase, tauFloor);
        }

        static float CpFractionAlongChord(float mach)
        {
            if (mach <= 0.8f) return 0.25f;
            if (mach >= 1.0f) return 0.50f;
            return 0.25f + (mach - 0.8f) / 0.2f * (0.50f - 0.25f);
        }

        static float MinLeadingEdgeSweepRad(float mach)
        {
            if (mach < 1.0001f) return 12f * (MathF.PI / 180f);
            float inv = MathF.Min(1f, 1f / mach);
            return MathF.Asin(inv) + 0.035f;
        }

        // ============================================================
        //  MDO Stability Loop
        // ============================================================
        public void OptimizeFinDimensions()
        {
            float R       = m_bodyRadiusMm;
            float Lref    = m_bodyLengthMm;
            float cRoot   = RootChordMm;
            float cTip    = 0.52f * cRoot;
            float sweep   = MinLeadingEdgeSweepRad(Mmax);
            float tau     = EffectiveThicknessRatio(cRoot);
            float cpFrac  = CpFractionAlongChord(Mmax);
            float D       = m_rp.fOuterDiameterMm;
            float zNose   = m_rp.EffectiveNoseLengthMm;
            float mBody   = MathF.Max(m_rp.fBodyNoFinsMassKg, 1e-6f);
            float zBodyCg = m_rp.fBodyNoFinsCgMm;
            float zBodyCp = m_rp.fBodyNoFinsCpMm;
            float rho     = m_rp.fFinMaterialDensityKgM3;
            float target  = m_rp.fTargetStaticMargin;

            float s = MinSemiSpanAeroMm;

            for (int i = 0; i < 512; i++)
            {
                EvaluateAtSpan(R, Lref, cRoot, cTip, sweep, tau, cpFrac, mBody, zBodyCg, zBodyCp, rho, zNose, D, s);
                if (LastStaticMarginCalib >= target) break;
                s += 1.5f;
                if (s > FinStructuralLimits.MaxSemiSpanCapMm) break;
            }

            EvaluateAtSpan(R, Lref, cRoot, cTip, sweep, tau, cpFrac, mBody, zBodyCg, zBodyCp, rho, zNose, D, MathF.Max(MinSemiSpanAeroMm, OptimalSemiSpanMm));
        }

        void EvaluateAtSpan(float R, float Lref, float cRoot, float cTip, float sweep, float tau, float cpFrac, float mBody, float zBodyCg, float zBodyCp, float rho, float zNose, float D, float sAeroMm)
        {
            float spanTotal = sAeroMm + m_tabDepthMm;
            float zLeMid = Lref - cRoot + 0.5f * spanTotal * MathF.Tan(sweep);
            float cMidLocal = 0.5f * (cRoot + cTip);
            
            float mFin4 = 4f * EstimateFinVolumeMm3(spanTotal, cRoot, cTip, tau) * rho * 1e-9f;
            float zFinCg = zNose + zLeMid + 0.5f * cMidLocal;
            float zFinCp = zNose + zLeMid + cpFrac * cMidLocal;
            float combinedCg = (mBody * zBodyCg + mFin4 * zFinCg) / MathF.Max(mBody + mFin4, 1e-9f);

            float sOverD = spanTotal / MathF.Max(D, 1e-3f);
            float lMid = MathF.Sqrt(spanTotal * spanTotal + MathF.Pow(spanTotal * MathF.Tan(sweep) + 0.5f * (cTip - cRoot), 2));
            float finCNa = 16f * sOverD * sOverD / (1f + MathF.Sqrt(1f + MathF.Pow(2f * lMid / MathF.Max(cRoot + cTip, 1e-3f), 2)));

            float combinedCp = (m_rp.fBodyNoFinsCNa * zBodyCp + finCNa * zFinCp) / MathF.Max(m_rp.fBodyNoFinsCNa + finCNa, 1e-6f);

            OptimalSemiSpanMm = sAeroMm;
            LastStaticMarginCalib = (combinedCp - combinedCg) / MathF.Max(D, 1e-3f);
            EstimatedFinMassKg = mFin4 / 4f;
            CombinedCgMmFromNose = combinedCg;
            CombinedCpMmFromNose = combinedCp;
            FinCpMmFromNose     = zFinCp;
            LastFinSetCNa       = finCNa;
        }

        float EstimateFinVolumeMm3(float semiSpanTotalMm, float cRoot, float cTip, float tau)
        {
            float edgeFrac = FinStructuralLimits.MinEdgeThicknessMm / MathF.Max(FinStructuralLimits.MinPeakThicknessMm, 1e-3f);
            float areaCoef = XPeakInFrac * (edgeFrac + 1f) * 0.5f + (XPeakOutFrac - XPeakInFrac) + (1f - XPeakOutFrac) * (1f + edgeFrac) * 0.5f;
            if (IsRoundedSubsonic) areaCoef = MathF.Max(areaCoef, 2f / MathF.PI);

            float tpRoot = MathF.Max(tau * cRoot, FinStructuralLimits.MinPeakThicknessMm);
            float tpTip  = MathF.Max(tau * cTip,  FinStructuralLimits.MinPeakThicknessMm);
            return 0.5f * (cRoot * tpRoot * areaCoef + cTip * tpTip * areaCoef) * semiSpanTotalMm;
        }

        // ============================================================
        //  Voxel Assembly
        // ============================================================
        public Voxels VoxConstruct()
        {
            Voxels vAsm = VoxHollowSleeveCan(RootChordMm) + VoxBuildAllFinsWithTabs();
            return SubtractWeepHolesFromAssembly(vAsm);
        }

        Voxels VoxHollowSleeveCan(float cRoot)
        {
            float len = MathF.Max(110f, 2.4f * cRoot);
            LocalFrame frame = new LocalFrame(new Vector3(0f, BodyAxisWorldY, m_bodyLengthMm - len));
            return new BaseCylinder(frame, len, m_bodyRadiusMm + SleeveWallThicknessMm).voxConstruct() - new BaseCylinder(frame, len, m_bodyRadiusMm).voxConstruct();
        }

        Voxels VoxBuildAllFinsWithTabs()
        {
            float cRoot = RootChordMm;
            float cTip = 0.52f * cRoot;
            float sweep = MinLeadingEdgeSweepRad(Mmax);
            float spanTotal = MathF.Max(MinSemiSpanAeroMm, OptimalSemiSpanMm) + m_tabDepthMm;
            float tau = EffectiveThicknessRatio(cRoot);
            float chordMargin = MathF.Max(0f, MathF.Min(0.45f, 0.5f * (1f - MathF.Abs(TtwTabChordFraction))));
            float xLE = MathF.Max(0.01f, FinStructuralLimits.MinEdgeThicknessMm / (2f * MathF.Max(cTip, 1e-3f)));

            Voxels vCombAcc = new Voxels();

            for (int k = 0; k < 4; k++)
            {
                float rad = k * 90f * (MathF.PI / 180f);
                Mesh mesh = MshFinWatertight(
                    m_bodyRadiusMm - m_tabDepthMm, m_bodyLengthMm, spanTotal, cRoot, cTip, sweep, tau,
                    new Vector3(MathF.Cos(rad), MathF.Sin(rad), 0f), new Vector3(0f, BodyAxisWorldY, 0f), m_tabDepthMm,
                    IsRoundedSubsonic, xLE, 1f - xLE, XPeakInFrac, XPeakOutFrac, chordMargin);

                vCombAcc += new Voxels(mesh);
            }
            return vCombAcc;
        }

        Voxels SubtractWeepHolesFromAssembly(Voxels vSolid)
        {
            float spanTotal = MathF.Max(MinSemiSpanAeroMm, OptimalSemiSpanMm) + m_tabDepthMm;
            float cTipChord = RootChordMm - spanTotal * (RootChordMm - 0.52f * RootChordMm) / MathF.Max(spanTotal, 1e-3f);
            float zMidChord = m_bodyLengthMm - RootChordMm + spanTotal * MathF.Tan(MinLeadingEdgeSweepRad(Mmax)) + 0.5f * cTipChord;

            Voxels vOut = vSolid;
            for (int k = 0; k < WeepHoleCount; k++)
            {
                float rad = k * 90f * (MathF.PI / 180f);
                Vector3 eSpan = new Vector3(MathF.Cos(rad), MathF.Sin(rad), 0f);
                Vector3 tipC = new Vector3(0f, BodyAxisWorldY, 0f) + eSpan * (m_bodyRadiusMm + spanTotal - m_tabDepthMm - WeepHoleInboardRadialMm) + Vector3.UnitZ * zMidChord;
                Vector3 eTh = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, eSpan));
                
                vOut -= new BaseCylinder(new LocalFrame(tipC - eTh * (WeepHoleLengthMm * 0.5f), eTh), WeepHoleLengthMm, WeepHoleRadiusMm).voxConstruct();
            }
            return vOut;
        }

        // ============================================================
        //  Mesh Generation Math
        // ============================================================
        static float ThicknessFraction(float u, float xLE, float xTE, float xPI, float xPO, float edgeFrac, bool rounded)
        {
            if (u <= xLE || u >= xTE) return edgeFrac;
            if (rounded) return MathF.Max(edgeFrac, MathF.Sin(MathF.PI * ((u - xLE) / MathF.Max(xTE - xLE, 1e-6f))));
            if (u <= xPI) return edgeFrac + (1f - edgeFrac) * (u - xLE) / MathF.Max(xPI - xLE, 1e-6f);
            if (u <= xPO) return 1f;
            return 1f - (1f - edgeFrac) * (u - xPO) / MathF.Max(xTE - xPO, 1e-6f);
        }

        static Mesh MshFinWatertight(
            float rootRadialMm, float zBaseTe, float semiSpanTotalMm, float cRoot, float cTip, float sweepRad, float tau,
            Vector3 radialDirWorld, Vector3 sceneOrigin, float tabBlendRadialMm, bool rounded, float xLE, float xTE, float xPI, float xPO, float chordMarginFrac)
        {
            Vector3 eSpan = Vector3.Normalize(radialDirWorld);
            Vector3 eTh = Vector3.Normalize(Vector3.Cross(Vector3.UnitZ, eSpan));
            float zRootLe = zBaseTe - cRoot;

            var uCanonical = new List<float>();
            if (rounded) { for (int j = 0; j <= 24; j++) uCanonical.Add(xLE + j * (xTE - xLE) / 24); }
            else { uCanonical.AddRange(new[] { xLE, xLE + (xPI - xLE)/4, xLE + 2*(xPI - xLE)/4, xLE + 3*(xPI - xLE)/4, xPI, xPI + (xPO - xPI)/4, xPI + 2*(xPO - xPI)/4, xPI + 3*(xPO - xPI)/4, xPO, xPO + (xTE - xPO)/4, xPO + 2*(xTE - xPO)/4, xPO + 3*(xTE - xPO)/4, xTE }); }

            var aTop = new List<List<Vector3>>();
            var aBot = new List<List<Vector3>>();

            for (int i = 0; i <= 24; i++)
            {
                float y = (i / 24f) * semiSpanTotalMm;
                bool inBn = y <= MathF.Min(MathF.Max(tabBlendRadialMm, 1e-3f), semiSpanTotalMm) + 1e-3f;
                float cLoc = cRoot - y * (cRoot - cTip) / MathF.Max(semiSpanTotalMm, 1e-3f);
                float tPeak = MathF.Max(tau * cLoc, FinStructuralLimits.MinPeakThicknessMm);
                float eF = MathF.Max(FinStructuralLimits.MinEdgeThicknessMm, 0.25f * tPeak) / MathF.Max(tPeak, 1e-3f);

                var rowT = new List<Vector3>();
                var rowB = new List<Vector3>();
                foreach (float uRaw in uCanonical)
                {
                    float u = MathF.Min(MathF.Max(uRaw, inBn ? chordMarginFrac : 0f), inBn ? 1f - chordMarginFrac : 1f);
                    float tL = ThicknessFraction(u, xLE, xTE, xPI, xPO, eF, rounded) * tPeak;
                    Vector3 p = sceneOrigin + eSpan * (rootRadialMm + y) + Vector3.UnitZ * (zRootLe + y * MathF.Tan(sweepRad) + u * cLoc);
                    rowT.Add(p + eTh * (0.5f * tL));
                    rowB.Add(p - eTh * (0.5f * tL));
                }
                aTop.Add(rowT);
                aBot.Add(rowB);
            }

            Mesh mesh = MeshUtility.mshFromGrid(aTop);
            var aBotRev = new List<List<Vector3>>();
            for (int ii = aBot.Count - 1; ii >= 0; ii--) aBotRev.Add(aBot[ii]);
            
            Mesh mBotG = MeshUtility.mshFromGrid(aBotRev);
            for (int i = 0; i < mBotG.nTriangleCount(); i++)
            {
                Triangle t = mBotG.oTriangleAt(i);
                mesh.nAddTriangle(mBotG.vecVertexAt(t.A), mBotG.vecVertexAt(t.B), mBotG.vecVertexAt(t.C));
            }

            for (int i = 0; i < 24; i++)
            {
                MeshUtility.AddQuad(ref mesh, aTop[i][0], aTop[i + 1][0], aBot[i + 1][0], aBot[i][0]);
                MeshUtility.AddQuad(ref mesh, aTop[i][uCanonical.Count - 1], aBot[i][uCanonical.Count - 1], aBot[i + 1][uCanonical.Count - 1], aTop[i + 1][uCanonical.Count - 1]);
            }
            for (int j = 0; j < uCanonical.Count - 1; j++)
            {
                MeshUtility.AddQuad(ref mesh, aTop[0][j], aTop[0][j + 1], aBot[0][j + 1], aBot[0][j]);
                MeshUtility.AddQuad(ref mesh, aTop[24][j + 1], aBot[24][j + 1], aBot[24][j], aTop[24][j]);
            }
            return mesh;
        }
    }
}