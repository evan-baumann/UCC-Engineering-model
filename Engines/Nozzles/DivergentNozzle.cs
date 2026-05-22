using System;
using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;
using Tintreach.Engines.Combustors;

namespace Tintreach.Engines.Nozzles
{
    /// <summary>
    /// Purely diverging PDE nozzle with a mounting sleeve that slides over the combustor tube OD.
    /// Local CAD frame: Z=0 at the nozzle inlet (combustor exit plane).
    /// </summary>
    public class DivergentNozzle
    {
        const uint LENGTH_STEPS = 400;
        const uint POLAR_STEPS  = 360;

        static readonly LineModulation s_oZeroRadius = new LineModulation(0f);

        public float m_fChamberInnerRadius;
        public float m_fChamberOuterRadius;
        public float m_fExitRadius;
        public float m_fNozzleLength;
        public float m_fWallThickness;
        public float m_fSleeveLength;

        public float TotalLengthMm => m_fNozzleLength + m_fSleeveLength;

        public DivergentNozzle(
            CylindricalCombustionChamber oChamber,
            float fExitRadius = 40f,
            float fNozzleLength = 80f,
            float fWallThickness = 4f,
            float fSleeveLength = 20f)
            : this(
                oChamber.InnerRadiusMm,
                oChamber.OuterRadiusMm,
                fExitRadius,
                fNozzleLength,
                fWallThickness,
                fSleeveLength)
        {
        }

        public DivergentNozzle(
            float fChamberInnerRadius,
            float fChamberOuterRadius,
            float fExitRadius,
            float fNozzleLength,
            float fWallThickness,
            float fSleeveLength)
        {
            if (fChamberInnerRadius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fChamberInnerRadius), "Chamber inner radius must be positive.");
            if (fChamberOuterRadius <= fChamberInnerRadius)
                throw new ArgumentOutOfRangeException(nameof(fChamberOuterRadius), "Chamber outer radius must exceed inner radius.");
            if (fExitRadius <= fChamberInnerRadius)
                throw new ArgumentOutOfRangeException(nameof(fExitRadius), "Exit radius must exceed chamber inner radius.");
            if (fNozzleLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fNozzleLength), "Nozzle length must be positive.");
            if (fWallThickness <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fWallThickness), "Wall thickness must be positive.");
            if (fSleeveLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fSleeveLength), "Sleeve length must be positive.");

            m_fChamberInnerRadius = fChamberInnerRadius;
            m_fChamberOuterRadius = fChamberOuterRadius;
            m_fExitRadius         = fExitRadius;
            m_fNozzleLength       = fNozzleLength;
            m_fWallThickness      = fWallThickness;
            m_fSleeveLength       = fSleeveLength;
        }

        /// <summary>Z placement so the sleeve overlaps the combustor open end.</summary>
        public float MountOriginZMm(CylindricalCombustionChamber oChamber) =>
            oChamber.m_fChamberLength - m_fSleeveLength;

        public Voxels voxConstruct()
        {
            LineModulation oFlowBore  = BuildFlowBoreModulation();
            LineModulation oStructOuter = BuildStructOuterModulation(oFlowBore);

            // Same pattern as CylindricalCombustionChamber: solid outer envelope minus bore void.
            Voxels voxOuterSolid = VoxRevolveSolid(s_oZeroRadius, oStructOuter);
            Voxels voxBoreVoid   = VoxRevolveSolid(s_oZeroRadius, oFlowBore);
            return voxOuterSolid - voxBoreVoid;
        }

        LineModulation BuildFlowBoreModulation()
        {
            float fTotalLength = TotalLengthMm;

            return new LineModulation(fLR =>
            {
                float fZ = fLR * fTotalLength;
                if (fZ <= m_fSleeveLength)
                    return m_fChamberInnerRadius;

                float fLocalLR = (fZ - m_fSleeveLength) / m_fNozzleLength;
                return m_fChamberInnerRadius + fLocalLR * (m_fExitRadius - m_fChamberInnerRadius);
            });
        }

        LineModulation BuildStructOuterModulation(LineModulation oFlowBore)
        {
            float fTotalLength = TotalLengthMm;
            float fSleeveOuterRadius = m_fChamberOuterRadius + m_fWallThickness;
            float fExitOuterRadius = m_fExitRadius + m_fWallThickness;

            return new LineModulation(fLR =>
            {
                float fZ = fLR * fTotalLength;
                if (fZ <= m_fSleeveLength)
                    return fSleeveOuterRadius;

                float fLocalLR = (fZ - m_fSleeveLength) / m_fNozzleLength;
                float fFlowRadius = oFlowBore.fGetModulation(fLR);
                float fTargetOuter = fSleeveOuterRadius + fLocalLR * (fExitOuterRadius - fSleeveOuterRadius);
                return MathF.Max(fFlowRadius + m_fWallThickness, fTargetOuter);
            });
        }

        Voxels VoxRevolveSolid(LineModulation oInnerRadius, LineModulation oOuterRadius)
        {
            LocalFrame oFrame = new LocalFrame();
            Frames aSpine = new Frames(TotalLengthMm, oFrame);

            BaseRevolve oRevolve = new BaseRevolve(oFrame, aSpine, 0f, 0f);
            oRevolve.SetRadius(oInnerRadius, oOuterRadius);
            oRevolve.SetLengthSteps(LENGTH_STEPS);
            oRevolve.SetPolarSteps(POLAR_STEPS);

            return oRevolve.voxConstruct();
        }

        public Voxels voxConstructMountedOn(CylindricalCombustionChamber oChamber, float fChamberOriginZ = 0f)
        {
            float fOriginZ = fChamberOriginZ + MountOriginZMm(oChamber);
            Voxels voxNozzle = voxConstruct();
            return MeshUtility.voxApplyTransformation(voxNozzle, vecPt => vecPt + new Vector3(0f, 0f, fOriginZ));
        }
    }
}
