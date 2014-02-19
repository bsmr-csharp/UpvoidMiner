// Copyright (C) by Upvoid Studios
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>

using System;
using System.Diagnostics;
using Engine;
using Engine.Universe;
using Engine.Resources;
using Engine.Rendering;
using Engine.Physics;
using System.Collections.Generic;

namespace UpvoidMiner
{
    public class DiggingController
    {
        private static Random random = new Random();

        /// <summary>
        /// Singleton for this controller.
        /// </summary>
        private static DiggingController instance;

        public enum DigMode
        {
            Substract,
            Add
        }

        /// <summary>
        /// Backref to world.
        /// </summary>
        World world;
        /// <summary>
        /// Backref to player.
        /// </summary>
        Player player;

		/// <summary>
		/// Cached CSG Sphere.
		/// </summary>
        CsgExpression sphereNode;

        /// <summary>
        /// Cached CSG Cube.
        /// </summary>
        CsgExpression boxNode;

        /// <summary>
        /// Cached CSG Cylinder.
        /// </summary>
        CsgExpression cylinderNode;

        /// <summary>
        /// Cached Player safety margin.
        /// </summary>
        CsgExpression playerNode;

        /// <summary>
        /// Particle system for 3D stones due to digging.
        /// </summary>
        class StoneParticles
        {
            public SolidTerrainResource resource;
            public CpuParticleSystemBase particlesStones;

            public StoneParticles(SolidTerrainResource res, World world)
            {
                resource = res;

                particlesStones = CpuParticleSystem.Create3D(new vec3(0, -9.81f, 0), world);
                LocalScript.ParticleEntity.AddComponent(new CpuParticleComponent(particlesStones, mat4.Identity));
                LocalScript.ParticleEntity.AddComponent(new RenderComponent(
                                                        (new CpuParticleRenderJob(particlesStones,
                                                             Renderer.Opaque.CpuParticles,
                                                             res.DigParticleMaterial,
                                                             Resources.UseMesh("::Particles/Rock", null),
                                                             mat4.Identity)),
                                                        mat4.Identity,
                                                        true));
                LocalScript.ParticleEntity.AddComponent(new RenderComponent(
                  (new CpuParticleRenderJob(particlesStones,
                         Renderer.Shadow.CpuParticles,
                         Resources.UseMaterial("Particles/Shadow/Mesh", UpvoidMiner.ModDomain),
                         Resources.UseMesh("::Particles/Rock", null),
                                      mat4.Identity)),
                    mat4.Identity,
                      true));
            }
        };
        private Dictionary<int, StoneParticles> stoneParticles = new Dictionary<int, StoneParticles>();

        public DiggingController(World world, Player player)
        {
            Debug.Assert(instance == null, "Singleton is violated");
            instance = this;
            this.world = world;
            this.player = player;

            string digParas = "digRadius:float, digPosition:vec3, digDirX:vec3, digDirY:vec3, digDirZ:vec3";

			string sphereExpression = "-digRadius + distance(vec3(x,y,z), digPosition)";
            sphereNode = new CsgExpression(1, sphereExpression, UpvoidMiner.ModDomain, digParas);

            string boxExpression = @"p = vec3(x,y,z) - digPosition;
                dx = abs(dot(p, digDirX));
                dy = abs(dot(p, digDirY));
                dz = abs(dot(p, digDirZ));
                -digRadius + max(dx, max(dy, dz))";
            boxNode = new CsgExpression(1, boxExpression, UpvoidMiner.ModDomain, digParas);

            string cylinderExpression = @"p = vec3(x,y,z) - digPosition;
                dx = abs(dot(p, digDirX));
                dy = abs(dot(p, digDirY));
                dz = abs(dot(p, digDirZ));
                -digRadius + max(dy, length(vec2(dx, dz)))";
            cylinderNode = new CsgExpression(1, cylinderExpression, UpvoidMiner.ModDomain, digParas);

            string playerExpression = @"p = vec3(x,y,z) - playerPosition;
                dx = abs(dot(p, digDirX));
                dy = abs(dot(p, digDirY));
                dz = abs(dot(p, digDirZ));
                -playerRadius + max(dy, length(vec2(dx, dz)))";
            playerNode = new CsgExpression(1, playerExpression, UpvoidMiner.ModDomain, "playerRadius:float, playerPosition:vec3, digDirX:vec3, digDirY:vec3, digDirZ:vec3");
        }

        public void Dig(CsgNode shape, BoundingSphere shapeBoundary, DigMode digMode, IEnumerable<int> materialFilter)
        {
            CsgNode digShape = null;

            // constraintDiffNode performs the constraint as a CSG operation
            // by cutting away anything of thge digging shape not inside the allowed area.
            CsgOpDiff constraintDiffNode = new CsgOpDiff();
            // Assemble difference operation by applying all drone constraints.
            player.AddDroneConstraints(constraintDiffNode, shapeBoundary.Center);

            // When placing material, add a safety margin around the player to prevent it from physically glitching trough the terrain
            if (digMode == DigMode.Add)
            {
                playerNode.SetParameterFloat("playerRadius", player.Character.CharacterDiameter * 0.5f + 0.2f);
                playerNode.SetParameterVec3("playerPosition", player.Character.Position);
                playerNode.SetParameterVec3("digDirX", new vec3(1, 0, 0));
                playerNode.SetParameterVec3("digDirZ", new vec3(0, 0, 1));
                playerNode.SetParameterVec3("digDirY", new vec3(0, 0, player.Character.BodyHeight * 0.5f + 0.2f));
                constraintDiffNode.AddNode(playerNode);
            }

            // We apply the constraint by substracting it from the given shape.
            CsgOpConcat constraintedShape = new CsgOpConcat();
            constraintedShape.AddNode(shape);
            constraintedShape.AddNode(constraintDiffNode);
            digShape = constraintedShape;

            CsgNode digNode = null;
            // Depending on the digging mode, we either add or substract the digging shape from the terrain.
            if (digMode == DigMode.Substract)
            {
                digNode = new CsgOpDiff(digShape);
            }
            else
            {
                digNode = new CsgOpUnion(digShape);
            }

            // Filter for tools
            CsgNode filterNode = digNode;
            if ( materialFilter != null )
            {
                CsgFilterNode filter = new CsgFilterNode(true, digNode);
                    foreach (int mat in materialFilter)
                        filter.AddMaterial(mat);
                filter.AddMaterial(0); // Air must be white-listed, too!
                filterNode = filter;
            }

            // Float elimination
            CsgOpConcat collapser = new CsgOpConcat(filterNode);
            collapser.AddNode(new CsgCollapseNode());

            // Callback for statistical purposes.
            CsgStatCallback finalNode = new CsgStatCallback(collapser, 4, 4);
            finalNode.AddSimpleVolumeCallback("UpvoidMiner", UpvoidMiner.ModDomain, "UpvoidMiner.DiggingController", "StatCallback");
            finalNode.AddVolumeChangePointCallback("UpvoidMiner", UpvoidMiner.ModDomain, "UpvoidMiner.DiggingController", "PointCallback");

            world.Terrain.ModifyTerrain(shapeBoundary, finalNode);
        }

        public void DigSphere(vec3 worldNormal, vec3 position, float radius, IEnumerable<int> filterMaterials, int terrainMaterialId = 1, DigMode digMode = DigMode.Substract)
		{
			sphereNode.MaterialIndex = terrainMaterialId;
            sphereNode.SetParameterFloat("digRadius", radius);
            sphereNode.SetParameterVec3("digPosition", position);

            vec3 dx, dy, dz;
            player.AlignmentSystem(worldNormal, out dx, out dy, out dz);
            sphereNode.SetParameterVec3("digDirX", dx);
            sphereNode.SetParameterVec3("digDirY", dy);
            sphereNode.SetParameterVec3("digDirZ", dz);

			Dig(sphereNode, new BoundingSphere(position, radius * 1.25f), digMode, filterMaterials);
		}

        public void DigBox(vec3 worldNormal, vec3 position, float radius, IEnumerable<int> filterMaterials, int terrainMaterialId = 1, DigMode digMode = DigMode.Substract)
        {
            boxNode.MaterialIndex = terrainMaterialId;
            boxNode.SetParameterFloat("digRadius", radius);
            boxNode.SetParameterVec3("digPosition", position);

            vec3 dx, dy, dz;
            player.AlignmentSystem(worldNormal, out dx, out dy, out dz);
            boxNode.SetParameterVec3("digDirX", dx);
            boxNode.SetParameterVec3("digDirY", dy);
            boxNode.SetParameterVec3("digDirZ", dz);

            Dig(boxNode, new BoundingSphere(position, radius * 1.5f), digMode, filterMaterials);
        }

        public void DigCylinder(vec3 worldNormal, vec3 position, float radius, IEnumerable<int> filterMaterials, int terrainMaterialId = 1, DigMode digMode = DigMode.Substract)
        {
            cylinderNode.MaterialIndex = terrainMaterialId;
            cylinderNode.SetParameterFloat("digRadius", radius);
            cylinderNode.SetParameterVec3("digPosition", position);

            vec3 dx, dy, dz;
            player.AlignmentSystem(worldNormal, out dx, out dy, out dz);
            cylinderNode.SetParameterVec3("digDirX", dx);
            cylinderNode.SetParameterVec3("digDirY", dy);
            cylinderNode.SetParameterVec3("digDirZ", dz);

            Dig(cylinderNode, new BoundingSphere(position, radius * 1.5f), digMode, filterMaterials);
        }

        /// <summary>
        /// This callback is called once per changed material in a chunk and reports the amount of volume changed (in m^3).
        /// </summary>
        public static void StatCallback(int mat, float volume, int lod)
        {
            if (mat != 0)
            {
                // Resolve terrain material.
                TerrainResource material = TerrainResource.FromIndex(mat);
                Debug.Assert(material != null, "Invalid terrain material");

                // Add proper amount of material to player inventory.
                // If the material changed by a negative volume we want to collect a positive amount.
                instance.player.Inventory.AddResource(material, -volume);
            }
        }

        /// <summary>
        /// Returns a random direction (currently biased towards cube edges).
        /// </summary>
        private static vec3 RandomDir()
        {
            return new vec3(
                (float)random.NextDouble() - .5f,
                (float)random.NextDouble() - .5f,
                (float)random.NextDouble() - .5f
            ).Normalized;
        }

        /// <summary>
        /// This callback is called for every point that changes materials in a digging operation.
        /// </summary>
        public static void PointCallback(float x, float y, float z, int matPrev, int matNow, int lod)
        {
            // If material was changed from non-air to air: add a particle animation.
            if (matPrev != 0 && matNow == 0)
            {
                // Create particle systems on demand.
                if (!instance.stoneParticles.ContainsKey(matPrev))
                {
                    SolidTerrainResource res = TerrainResource.FromIndex(matPrev) as SolidTerrainResource;
                    if (res != null)
                        instance.stoneParticles.Add(matPrev, new StoneParticles(res, LocalScript.world));
                    else
                        instance.stoneParticles.Add(matPrev, null);
                }

                // Add particle.
                StoneParticles particles = instance.stoneParticles[matPrev];
                if (particles != null)
                {
                    instance.stoneParticles[matPrev].particlesStones.AddParticle3D(
                        new vec3(x, y, z) + RandomDir() * (float)random.NextDouble() * .3f,
                        RandomDir() * (float)random.NextDouble() * .4f,
                        new vec4(1),
                        .4f + (float)random.NextDouble() * .3f,
                        .2f + (float)random.NextDouble() * .3f,
                        RandomDir(),
                        RandomDir(),
                        new vec3(0));
                }
            }
        }

        /*
        public void construct(vec3 position, CsgNode diggingShape, float influencingRadius)
        {
            CsgOpUnion digDiffNode = new CsgOpUnion(diggingShape);
            world.Terrain.ModifyTerrain(new BoundingSphere(position, influencingRadius), digDiffNode);
        }

        public void constructSphere(vec3 position, float radius)
        {
            dig(position, new CsgExpression("-"+radius.ToString()+" + sqrt(distance2(vec3(x,y,z), vec3"+position.ToString()+"))"));
        }
        */
    }
}

