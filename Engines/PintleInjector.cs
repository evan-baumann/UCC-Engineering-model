using System;
using System.Numerics;
using PicoGK;
using Leap71.ShapeKernel;

namespace UCCRocketry.Engines
{
    public class PintleInjector
    {
        // --- VARIABLE PARAMETERS ---
        // 1. Bulkhead Dimensions
        public float m_fTubeID;
        public float m_fBulkheadThickness;

        // 2. Pintle Post Dimensions
        public float m_fPintleDiameter;
        public float m_fPintleProtrusion;

        // 3. Propane (Fuel) Dimensions
        public float m_fPropaneChannelDiam;
        public int   m_nPropanePorts;
        public float m_fPropanePortDiam;

        // 4. GOX (Oxidizer) Dimensions
        public float m_fGoxAnnulusID;
        public float m_fGoxAnnulusOD;

        // --- CONSTRUCTOR ---
        public PintleInjector(
            float fTubeID = 70.0f,
            float fBulkheadThickness = 35.0f, // Thick enough to hold 100 bar
            float fPintleDiameter = 15.0f,
            float fPintleProtrusion = 20.0f,
            float fPropaneChannelDiam = 9.0f,
            int   nPropanePorts = 8,
            float fPropanePortDiam = 3.1f,
            float fGoxAnnulusID = 15.0f,
            float fGoxAnnulusOD = 23.3f)
        {
            m_fTubeID = fTubeID;
            m_fBulkheadThickness = fBulkheadThickness;
            m_fPintleDiameter = fPintleDiameter;
            m_fPintleProtrusion = fPintleProtrusion;
            m_fPropaneChannelDiam = fPropaneChannelDiam;
            m_nPropanePorts = nPropanePorts;
            m_fPropanePortDiam = fPropanePortDiam;
            m_fGoxAnnulusID = fGoxAnnulusID;
            m_fGoxAnnulusOD = fGoxAnnulusOD;
        }

        // We will build the geometry inside this method
        public Voxels voxConstruct()
        {
            // ---------------------------------------------------------
            // 1. POSITIVE SPACE: Build the solid metal body
            // ---------------------------------------------------------
            
            // A. The Base Bulkhead (Z=0 to Z=30)
            LocalFrame oBaseFrame = new LocalFrame();
            BaseCylinder oBulkhead = new BaseCylinder(oBaseFrame, m_fBulkheadThickness, m_fTubeID / 2f);
            Voxels voxMetal = oBulkhead.voxConstruct();

            // B. The Pintle Post (Z=0 to Z=50)
            BaseCylinder oPintlePost = new BaseCylinder(oBaseFrame, m_fBulkheadThickness + m_fPintleProtrusion, m_fPintleDiameter / 2f);
            voxMetal += oPintlePost.voxConstruct();

            // C. The "F-15" Thermal Shock Cap (Hemisphere at Z=50)
            LocalFrame oCapFrame = new LocalFrame(new Vector3(0, 0, m_fBulkheadThickness + m_fPintleProtrusion));
            BaseSphere oThermalCap = new BaseSphere(oCapFrame, m_fPintleDiameter / 2f);
            voxMetal += oThermalCap.voxConstruct();

            // ---------------------------------------------------------
            // 2. NEGATIVE SPACE: Primary Fluid Voids
            // ---------------------------------------------------------

            // Port row height (radial drills use this Z). Define before the propane core so the
            // central bore always extends past this plane — otherwise voids do not meet and voxels
            // often show a false ring at the cross-bore.
            float fPortZHeight = 47.5f;

// A. Propane main channel
            float fPropaneChannelTopZ = fPortZHeight + 2.5f;

            LocalFrame oPropaneFrame = new LocalFrame();
            BaseCylinder oPropaneCore = new BaseCylinder(oPropaneFrame, fPropaneChannelTopZ, m_fPropaneChannelDiam / 2f);
            
            // DELETED the Set...Steps lines here! Let the math be smooth.
            
            Voxels voxPropaneVoid = oPropaneCore.voxConstruct();

            // B. GOX Annulus (Z=10 to Z=40)
            // FIX: Length is 30f so it punches cleanly 10mm past the Z=30 bulkhead face.
            LocalFrame oGoxFrame = new LocalFrame(new Vector3(0f, 0f, 10f));
            BaseCylinder oGoxOuter = new BaseCylinder(oGoxFrame, 30f, m_fGoxAnnulusOD / 2f);
            BaseCylinder oGoxInner = new BaseCylinder(oGoxFrame, 30f, m_fGoxAnnulusID / 2f);
            Voxels voxGoxVoid = oGoxOuter.voxConstruct() - oGoxInner.voxConstruct();

            // C. GOX Side-Feed Port (18mm pipe)
            // FIX: Length is 27f. Starts at X=35, stops at X=8 (safely misses the 7.5mm pintle post).
            Vector3 vecSideFeedOrigin = new Vector3(45f, 0, 20f);
            Vector3 vecSideFeedDir = new Vector3(-1f, 0, 0f);
            LocalFrame oSideFeedFrame = new LocalFrame(vecSideFeedOrigin, vecSideFeedDir);
            BaseCylinder oGoxSideFeed = new BaseCylinder(oSideFeedFrame, 35f, 18f / 2f);

            voxGoxVoid += oGoxSideFeed.voxConstruct();

            // ---------------------------------------------------------
            // 3. NEGATIVE SPACE: Radial Propane Ports
            // ---------------------------------------------------------
            Voxels voxPorts = new Voxels();

            for (int i = 0; i < m_nPropanePorts; i++)
            {
                float fAngle = i * (MathF.PI * 2f / m_nPropanePorts);
                Vector3 vecOutwardDir = new Vector3(MathF.Cos(fAngle), MathF.Sin(fAngle), 0f);
                
                // FIX: Move the starting point of the drill bit 4mm outward from the center.
                // This ensures it starts inside the steel wall and drills outward, keeping the center clean.
                Vector3 vecPortOrigin = new Vector3(MathF.Cos(fAngle) * 4f, MathF.Sin(fAngle) * 4f, fPortZHeight);
                LocalFrame oPortFrame = new LocalFrame(vecPortOrigin, vecOutwardDir);

                // Length is 5f (starts at R=4, ends at R=9). Safely punches through the 7.5mm pintle wall.
                BaseCylinder oPort = new BaseCylinder(oPortFrame, 5f, m_fPropanePortDiam / 2f);
                
                // DELETED the Set...Steps lines here too!
                
                voxPorts += oPort.voxConstruct();
            }
// ---------------------------------------------------------
            // 4. MACHINING PORTS: Swagelok Threaded Counterbores
            // ---------------------------------------------------------
            Voxels voxMachiningPorts = new Voxels();

            // A. Propane Port (1/4" BSPP) - Bottom Face (Z=0)
            // Tap drill size = 11.8mm. Depth = 15mm.
            LocalFrame oPropaneThreadFrame = new LocalFrame();
            BaseCylinder oPropaneThread = new BaseCylinder(oPropaneThreadFrame, 15f, 11.8f / 2f);
            voxMachiningPorts += oPropaneThread.voxConstruct();

            // B. GOX Port (1/2" BSPP) - Side Wall
            // Tap drill size = 19.0mm. Depth = 15mm.
            // We use the exact same origin and direction as the GOX Side-Feed
            Vector3 vecGoxThreadOrigin = new Vector3(45f, 0, 20f); // Starts at outer wall
            Vector3 vecGoxThreadDir = new Vector3(-1f, 0, 0f);     // Points inward
            LocalFrame oGoxThreadFrame = new LocalFrame(vecGoxThreadOrigin, vecGoxThreadDir);
            
            BaseCylinder oGoxThread = new BaseCylinder(oGoxThreadFrame, 25f, 19.0f / 2f);
            voxMachiningPorts += oGoxThread.voxConstruct();
            // ---------------------------------------------------------
            // 4. THE MACHINING (Boolean Subtraction)
            // ---------------------------------------------------------
            Voxels voxFinalInjector = voxMetal;

            voxFinalInjector -= voxGoxVoid;

            // Union propane + radial ports, then one subtract — often cleans the cross-bore
            // ring better than metal -= propane -= ports (GOX stays separate; different region).
            Voxels voxPropaneAndPorts = voxPropaneVoid;
            voxPropaneAndPorts += voxPorts;
            voxFinalInjector -= voxPropaneAndPorts;
            // Subtract the Swagelok tap drill holes
            voxFinalInjector -= voxMachiningPorts;

            return voxFinalInjector;
        }
    }
}