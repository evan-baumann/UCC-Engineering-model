using System;
using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

namespace Tintreach.Engines.Combustors
{
    /// <summary>
    /// Round PDE combustor tube: solid wall from Z=0 to chamber length.
    /// Supplies bore geometry to <see cref="ShchelkinSpiral"/>.
    /// </summary>
    public class CylindricalCombustionChamber
    {
        public float m_fTubeInnerDiameter;
        public float m_fChamberLength;
        public float m_fWallThickness;

        public float InnerRadiusMm => m_fTubeInnerDiameter / 2f;
        public float OuterRadiusMm => InnerRadiusMm + m_fWallThickness;
        public float DefaultPitchMm => m_fTubeInnerDiameter;

        public CylindricalCombustionChamber(
            float fTubeInnerDiameter = 50f,
            float fChamberLength = 300f,
            float fWallThickness = 3f)
        {
            if (fTubeInnerDiameter <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fTubeInnerDiameter), "Tube inner diameter must be positive.");
            if (fChamberLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fChamberLength), "Chamber length must be positive.");
            if (fWallThickness <= 0f)
                throw new ArgumentOutOfRangeException(nameof(fWallThickness), "Wall thickness must be positive.");

            m_fTubeInnerDiameter = fTubeInnerDiameter;
            m_fChamberLength     = fChamberLength;
            m_fWallThickness     = fWallThickness;
        }

        public Voxels voxConstruct()
        {
            LocalFrame oFrame = new LocalFrame();
            BaseCylinder oOuter = new BaseCylinder(oFrame, m_fChamberLength, OuterRadiusMm);
            BaseCylinder oInner = new BaseCylinder(oFrame, m_fChamberLength, InnerRadiusMm);
            return oOuter.voxConstruct() - oInner.voxConstruct();
        }

        /// <summary>Same as <see cref="voxConstruct"/> but shifted along +Z (combustor inlet plane).</summary>
        public Voxels voxConstructAt(float fOriginZ)
        {
            if (fOriginZ == 0f)
                return voxConstruct();

            Voxels voxChamber = voxConstruct();
            return MeshUtility.voxApplyTransformation(
                voxChamber,
                vecPt => vecPt + new Vector3(0f, 0f, fOriginZ));
        }
    }
}
