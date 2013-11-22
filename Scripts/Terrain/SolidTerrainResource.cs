using System;
using Engine.Universe;
using Engine.Rendering;
using Engine.Resources;
using Engine.Scripting;

namespace UpvoidMiner
{
    /// <summary>
    /// A simple solid terrain resource without decorations, e.g. stone or wood.
    /// </summary>
    public class SolidTerrainResource : TerrainResource
    {
        /// <summary>
        /// Material used for rendering the solid terrain.
        /// </summary>
        public readonly MaterialResource RenderMaterial;
        /// <summary>
        /// Material used for particle effect when digging.
        /// </summary>
        public readonly MaterialResource DigParticleMaterial;

        public SolidTerrainResource(string name, string renderMaterial, string particleMaterial, bool defaultPipeline = true) :
            base(name)
        {
            RenderMaterial = Resources.UseMaterial(renderMaterial, UpvoidMiner.ModDomain);
            DigParticleMaterial = Resources.UseMaterial(particleMaterial, UpvoidMiner.ModDomain);

            if (Scripting.IsHost)
            {
                // Add a default pipeline. (Solid material with zPre and Shadow pass)
                if (defaultPipeline)
                {
                    { // LoD 0-4
                        int pipe = Material.AddPipeline(Resources.UseGeometryPipeline("ColoredRock", UpvoidMiner.ModDomain), "Input", "Decimate", 0, 4);
                        Material.AddDefaultShadowAndZPre(pipe);
                        Material.AddMeshMaterial(pipe, "Output", RenderMaterial, Renderer.Opaque.Mesh);
                    }

                    { // LoD 5-max
                        int pipe = Material.AddPipeline(Resources.UseGeometryPipeline("ColoredRockLow", UpvoidMiner.ModDomain), "Input", "Decimate", 5);
                        Material.AddDefaultShadowAndZPre(pipe);
                        Material.AddMeshMaterial(pipe, "Output", RenderMaterial, Renderer.Opaque.Mesh);
                    }
                }
            }
        }
    }
}

