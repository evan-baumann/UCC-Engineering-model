using System;
using System.Collections.Generic;
using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

namespace Tintreach.Engines.Combustors
{
    /// <summary>
    /// Parametric Shchelkin spiral for pulse detonation engine DDT enhancement.
    /// Models a single wire wound helically inside a round tube; the wire center
    /// follows a helix tangent to the tube inner wall.
    /// </summary>
    public class ShchelkinSpiral
    {
        // --- VARIABLE PARAMETERS ---
        public float m_fTubeInnerRadius;
        public float m_fSpiralLength;
        public float m_fPitch;
        public float m_fWireRadius;
        public float m_fBlockingRatio;
        public float m_fStartOffsetZ;

        /// <summary>Helix path radius: tube inner radius minus wire radius.</summary>
        public float HelixPathRadiusMm => m_fTubeInnerRadius - m_fWireRadius;

        /// <summary>Wire outer diameter derived from the target blocking ratio.</summary>
        public float WireDiameterMm => 2f * m_fWireRadius;

        /// <summary>Approximate number of full turns over the spiral length.</summary>
        public float TurnCount => m_fSpiralLength / m_fPitch;

        public ShchelkinSpiral(
            CylindricalCombustionChamber oChamber,
            float fSpiralLength,
            float fBlockingRatio = 0.45f,
            float? fPitchMm = null,
            float fStartOffsetZ = 0f)
            : this(
                oChamber.InnerRadiusMm,
                fSpiralLength,
                fPitchMm ?? oChamber.DefaultPitchMm,
                fBlockingRatio,
                fStartOffsetZ)
        {
        }

        public ShchelkinSpiral(
            float fTubeInnerRadius,
            float fSpiralLength,
            float fPitch,
            float fBlockingRatio,
            float fStartOffsetZ = 0f)
        {
            if (fTubeInnerRadius <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fTubeInnerRadius), "Tube inner radius must be positive.");
            if (fSpiralLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fSpiralLength), "Spiral length must be positive.");
            if (fPitch <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fPitch), "Pitch must be positive.");
            if (fBlockingRatio <= 0f || fBlockingRatio >= 1f)
                throw new ArgumentOutOfRangeException(nameof(fBlockingRatio), "Blocking ratio must be between 0 and 1.");
            if (fStartOffsetZ < 0f)
                throw new ArgumentOutOfRangeException(nameof(fStartOffsetZ), "Start offset must be non-negative.");

            m_fTubeInnerRadius = fTubeInnerRadius;
            m_fSpiralLength    = fSpiralLength;
            m_fPitch           = fPitch;
            m_fBlockingRatio   = fBlockingRatio;
            m_fStartOffsetZ    = fStartOffsetZ;

            float fWireDiam = ComputeWireDiameterFromBlockingRatio(fTubeInnerRadius, fBlockingRatio);
            m_fWireRadius   = fWireDiam / 2f;

            if (HelixPathRadiusMm <= 0f)
                throw new ArgumentException(
                    $"Wire diameter ({WireDiameterMm:0.###} mm) is too large for tube ID ({2f * fTubeInnerRadius:0.###} mm).");
        }

        /// <summary>
        /// Relates blockage ratio to wire diameter for a round wire in a round tube:
        /// BR = 1 - ((R_tube - d_wire) / R_tube)^2
        /// => d_wire = R_tube * (1 - sqrt(1 - BR))
        /// </summary>
        public static float ComputeWireDiameterFromBlockingRatio(float fTubeInnerRadius, float fBlockingRatio) =>
            fTubeInnerRadius * (1f - MathF.Sqrt(1f - fBlockingRatio));

        public Voxels voxConstruct()
        {
            List<Vector3> aHelixPoints = BuildHelixPoints(out float fThetaStep);

            Lattice oLattice = new Lattice();
            for (int i = 1; i < aHelixPoints.Count; i++)
            {
                oLattice.AddBeam(
                    aHelixPoints[i - 1], m_fWireRadius,
                    aHelixPoints[i],     m_fWireRadius);
            }

            return new Voxels(oLattice);
        }

        List<Vector3> BuildHelixPoints(out float fThetaStep)
        {
            float fRadiusOfPath = HelixPathRadiusMm;
            float fMaxTheta = (m_fSpiralLength / m_fPitch) * MathF.Tau;

            float fArcStepMm = MathF.Max(m_fWireRadius * 2f, 0.5f);
            fThetaStep = fArcStepMm / MathF.Max(fRadiusOfPath, 0.001f);

            var aPoints = new List<Vector3>();
            for (float fTheta = 0f; fTheta <= fMaxTheta + fThetaStep * 0.5f; fTheta += fThetaStep)
            {
                float fZ = (m_fPitch / MathF.Tau) * fTheta;
                if (fZ > m_fSpiralLength)
                    break;

                aPoints.Add(new Vector3(
                    fRadiusOfPath * MathF.Cos(fTheta),
                    fRadiusOfPath * MathF.Sin(fTheta),
                    m_fStartOffsetZ + fZ));
            }

            if (aPoints.Count == 0 || aPoints[^1].Z < m_fStartOffsetZ + m_fSpiralLength - 0.001f)
            {
                aPoints.Add(new Vector3(
                    fRadiusOfPath * MathF.Cos(fMaxTheta),
                    fRadiusOfPath * MathF.Sin(fMaxTheta),
                    m_fStartOffsetZ + m_fSpiralLength));
            }

            return aPoints;
        }
    }
}
