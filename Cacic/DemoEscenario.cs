using System;
using System.Collections.Generic;
using System.Text;
using TgcViewer.Example;
using TgcViewer;
using Microsoft.DirectX.Direct3D;
using System.Drawing;
using Microsoft.DirectX;
using TgcViewer.Utils.Modifiers;
using TgcViewer.Utils.TgcSceneLoader;
using TgcViewer.Utils.TgcGeometry;
using Examples.Shaders;
using TgcViewer.Utils.Shaders;
using TgcViewer.Utils;
using TgcViewer.Utils.Terrain;
using Examples.GpuOcclusion.ParalellOccludee;
using TgcViewer.Utils._2D;
using TgcViewer.Utils.Interpolation;

namespace Examples.GpuOcclusion.Cacic
{
    /// <summary>
    /// Demo GPU occlusion Culling
    /// GIGC - UTN-FRBA
    /// </summary>
    public class DemoEscenario : TgcExample
    {

        Effect effect;
        OcclusionEngineParalellOccludee occlusionEngine;
        TgcSkyBox skyBox;
        CacicFpsCamera camera;
        TgcSprite cuadroNegro;
        List<OccluderData> occluderData;
        TgcMeshShader piso;

        //Sol
        TgcSprite sunSprite;
        InterpoladorVaiven interpoladorSol;
        TgcMeshShader sunMesh;

        //Puerta principal
        OccluderData puertaOccluder;
        bool animatingDoor;
        float animatingDoorDir;

        //Disparo de misil
        const int MISSILE_COUNT = 20;
        MissileData[] missilesData;

        //HDR
        float adaptedLum;

        //ZBuffer render
        TgcSimpleSprite zBufferSprite;

        //ShadowMap
        readonly int SHADOWMAP_SIZE = 1024;
        Texture shadowMapTex; //Texture to which the shadow map is rendered
        Surface depthStencilShadowMap; // Depth-stencil buffer for rendering to shadow map
        Matrix shadowProj; //Projection matrix for shadow map


        public override string getCategory()
        {
            return "Cacic";
        }

        public override string getName()
        {
            return "Demo Escenario";
        }

        public override string getDescription()
        {
            return "Demo Escenario";
        }

        public override void init()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            GuiController.Instance.CustomRenderEnabled = true;

            //Camara
            camera = new CacicFpsCamera();
            camera.Enable = true;
            camera.setCamera(new Vector3(1567.827f, 50f, -820.0528f), new Vector3(1566.829f, 50.0564f, -820.0107f));
            //camera.setCamera(new Vector3(-1103.807f, 773.228f, -165.853f), new Vector3(-1102.96f, 773.0295f, -166.345f));
            camera.MovementSpeed = 200f;
            camera.JumpSpeed = 200f;

      
            //Engine de Occlusion
            occlusionEngine = new OcclusionEngineParalellOccludee();


            //Cargar shader para render de meshes (mas info de occlusion)
            effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\Cacic\\CacicOccludees.fx");
            effect.Technique = "DIFFUSE_MAP";


            //Cargar ciudad
            TgcSceneLoader loader = new TgcSceneLoader();
            loader.MeshFactory = new CustomMeshShaderFactory();
            TgcScene scene = loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Escenario2\\Escenario2-TgcScene.xml");

            //Separar occluders y occludees
            occluderData = new List<OccluderData>();
            for (int i = 0; i < scene.Meshes.Count; i++)
            {
                TgcMeshShader mesh = (TgcMeshShader)scene.Meshes[i];

                //Occluders
                if (mesh.Layer.Contains("Occluder"))
                {
                    OccluderData ocData = new OccluderData();
                    ocData.mesh = null;
                    ocData.vecinos = new List<OccluderData>();
                    ocData.enabled = true;

                    //Crear occluder
                    Occluder occluder = new Occluder(mesh.BoundingBox.clone());
                    occluder.update();
                    occlusionEngine.Occluders.Add(occluder);
                    ocData.occluder = occluder;

                    //Box de debug de occluder
                    ocData.occluderBox = TgcBox.fromExtremes(occluder.Aabb.PMin, occluder.Aabb.PMax, Color.Green);

                    //Mesh de puerta
                    if (mesh.Name == "OccluderPuertaPrincipal")
                    {
                        puertaOccluder = ocData;
                    }

                    occluderData.Add(ocData);
                    mesh.dispose();
                }

                //Meshes
                else
                {
                    mesh.Effect = effect;
                    occlusionEngine.Occludees.Add(mesh);

                    //Mesh de piso
                    if (mesh.Name == "Piso")
                    {
                        piso = mesh;
                    }

                    //Invertir normales
                    mesh.computeNormals();
                    if (mesh.Layer.Contains("Piso"))
                    {
                        mesh.invertNormals();
                    }

                }
            }


            //Buscar el mesh al que pertenece cada occluder
            TgcBoundingBox smallerAABB = new TgcBoundingBox();
            foreach (OccluderData ocData in occluderData)
            {
                //Usar un AABB muy chiquito del occluder para garantizar que realmente es el que corresponde a ese mesh
                Vector3 c = ocData.occluder.Aabb.calculateBoxCenter();
                smallerAABB.setExtremes(c - new Vector3(0.1f, 0.1f, 0.1f), c + new Vector3(0.1f, 0.1f, 0.1f));
                foreach (TgcMeshShader mesh in occlusionEngine.Occludees)
                {
                    //Que sea un mesh de los que tienen occluders
                    if (mesh.Layer == "Casas" || mesh.Layer == "Atalaya" || mesh.Layer == "Puertas" || mesh.Layer == "ParedesExterioresCastillo" || mesh.Layer == "ParedesInterioresCastillo")
                    {
                        if (TgcCollisionUtils.testAABBAABB(smallerAABB, mesh.BoundingBox))
                        {
                            if (ocData.mesh != null)
                            {
                                throw new Exception("Occluder con mas de un mesh. Mesh: " + mesh.Name + " and " + ocData.mesh.Name);
                            }
                            ocData.mesh = mesh;
                        }
                    }
                }
                if (ocData.mesh == null)
                {
                    throw new Exception("Occluder que no pertenece a ningun mesh");
                }
            }

            //Relacionar occluders que pertenecen al mismo mesh
            foreach (OccluderData ocData1 in occluderData)
            {
                foreach (OccluderData ocData2 in occluderData)
                {
                    if (ocData1 != ocData2)
                    {
                        if (ocData1.mesh == ocData2.mesh)
                        {
                            //Relacionar
                            ocData1.vecinos.Add(ocData2);
                            ocData2.vecinos.Add(ocData1);
                        }
                    }
                }
            }


            //Iniciar engine de occlusion
            occlusionEngine.init(occlusionEngine.Occludees.Count);


            //Crear SkyBox
            skyBox = new TgcSkyBox();
            skyBox.Center = new Vector3(0, 0, 0);
            skyBox.Size = new Vector3(10000, 10000, 10000);
            skyBox.SkyEpsilon = 20;
            string texturesPath = GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\SkyBoxMountain\\";
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Up, texturesPath + "Up.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Down, texturesPath + "Down.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Left, texturesPath + "Right.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Right, texturesPath + "Left.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Front, texturesPath + "Back.jpg");
            skyBox.setFaceTexture(TgcSkyBox.SkyFaces.Back, texturesPath + "Front.jpg");
            skyBox.updateValues();

            //Modifiers
            GuiController.Instance.Modifiers.addBoolean("occlusionCull", "occlusionCull", true);
            GuiController.Instance.Modifiers.addBoolean("zbuffer", "zbuffer", false);
            GuiController.Instance.Modifiers.addBoolean("drawMeshes", "drawMeshes", true);
            GuiController.Instance.Modifiers.addBoolean("drawOccluders", "drawOccluders", false);
            GuiController.Instance.Modifiers.addBoolean("wireframe", "wireframe", false);
            GuiController.Instance.Modifiers.addBoolean("drawHidden", "drawHidden", false);
            GuiController.Instance.Modifiers.addFloat("hdrExposure", 0.05f, 1f, 0.72f);
            GuiController.Instance.Modifiers.addBoolean("shadows", "shadows", false);

            adaptedLum = 0.5f;
            GuiController.Instance.Modifiers.addFloat("specular", 1f, 1000f, 50f);



            //Efecto de sol
            sunSprite = new TgcSprite();
            sunSprite.Texture = TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Imagenes\\sunEffect.png");
            sunSprite.Scaling = new Vector2(2, 2);
            interpoladorSol = new InterpoladorVaiven();
            interpoladorSol.Min = -1000f;
            interpoladorSol.Max = 3000f;
            interpoladorSol.Speed = 25f;
            interpoladorSol.reset();

            //Mesh de sol
            sunMesh = (TgcMeshShader)loader.loadSceneFromFile(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Sun\\Sun-TgcScene.xml").Meshes[0];
            sunMesh.Effect = effect;
            sunMesh.Scale = new Vector3(0.7f, 0.7f, 0.7f);

            //Cuadro negro para metricas
            cuadroNegro = new TgcSprite();
            cuadroNegro.Position = new Vector2(0, 0);
            cuadroNegro.Texture = TgcTexture.createTexture(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Imagenes\\Black.png");
            cuadroNegro.Scaling = new Vector2(0.8f, 0.75f);

            

            //Para disparos de misil
            missilesData = new MissileData[MISSILE_COUNT];
            for (int i = 0; i < missilesData.Length; i++)
            {
                missilesData[i] = new MissileData();
                missilesData[i].reset();

                //Misil
                TgcBoxLine missile = new TgcBoxLine();
                missile = new TgcBoxLine();
                missile.Color = Color.Gray;
                missile.Thickness = 1;
                missilesData[i].missile = missile;

                //Billboard de explosion
                TgcAnimatedBillboard expotionBillboard = new TgcAnimatedBillboard(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Imagenes\\Explosion.png", new Size(102, 102), 23, 23);
                expotionBillboard.Size = new Vector2(102 * 2, 102 * 2);
                expotionBillboard.Enabled = false;
                expotionBillboard.Loop = false;
                missilesData[i].explotion = expotionBillboard;

                //Billboard de humo
                TgcAnimatedBillboard smokeBillboard = new TgcAnimatedBillboard(GuiController.Instance.ExamplesMediaDir + "ModelosTgc\\Imagenes\\smoke.png", new Size(128, 128), 38, 20);
                smokeBillboard.Size = new Vector2(128 * 2, 128 * 2);
                smokeBillboard.Enabled = false;
                smokeBillboard.Loop = true;
                missilesData[i].smoke = smokeBillboard;
            }


            //Animacion de puerta
            animatingDoor = false;
            animatingDoorDir = 1;


            //Render de ZBuffer
            zBufferSprite = new TgcSimpleSprite();
            zBufferSprite.Position = new Vector2(0, d3dDevice.Viewport.Height / 2);
            zBufferSprite.Size = new Vector2(d3dDevice.Viewport.Width / 2, d3dDevice.Viewport.Height / 2);
            zBufferSprite.Texture = new TgcTexture("zBufferTexture", "zBufferTexture", occlusionEngine.ZBufferTex, false);
            //zBufferSprite.Texture = new TgcTexture("zBufferTexture", "zBufferTexture", shadowMapTex, false);
            zBufferSprite.Effect = ShaderUtils.loadEffect(GuiController.Instance.ExamplesMediaDir + "Shaders\\Cacic\\DrawZBuffer.fx");
            zBufferSprite.Effect.Technique = "DefaultTechnique";
            zBufferSprite.updateValues();


            //ShadowMap
            shadowMapTex = new Texture(d3dDevice, SHADOWMAP_SIZE, SHADOWMAP_SIZE, 1, Usage.RenderTarget, Format.R32F, Pool.Default);
            depthStencilShadowMap = d3dDevice.CreateDepthStencilSurface(SHADOWMAP_SIZE, SHADOWMAP_SIZE, DepthFormat.D24S8, MultiSampleType.None, 0, true);
            shadowProj = Matrix.PerspectiveFovLH(FastMath.ToRad(80), (float)d3dDevice.Viewport.Width / (float)d3dDevice.Viewport.Height, 50, 5000);

        }




        public override void render(float elapsedTime)
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Abrir/Cerrar puerta principal
            openCloseMainDoor();


            //Activar culling
            occlusionEngine.FrustumCullingEnabled = true;
            bool occlusionCullEnabled = (bool)GuiController.Instance.Modifiers["occlusionCull"];
            occlusionEngine.OcclusionCullingEnabled = occlusionCullEnabled;

            //Actualizar visibilidad
            occlusionEngine.updateVisibility();



            //Clear
            d3dDevice.BeginScene();
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.Black, 1.0f, 0);

            //Dibujar recuadro para metricas
            GuiController.Instance.Drawer2D.beginDrawSprite();
            cuadroNegro.render();
            GuiController.Instance.Drawer2D.endDrawSprite();

            //FPS counter
            GuiController.Instance.Text3d.drawText("FPS: " + HighResolutionTimer.Instance.FramesPerSecond, 0, 0, Color.White);




            //Skybox
            skyBox.render();



            //Cargar variables de luz
            sunMesh.Position = new Vector3(interpoladorSol.update(), 1000, -800);
            effect.SetValue("lightPosition", TgcParserUtils.vector3ToFloat4Array(sunMesh.Position));
            effect.SetValue("eyePosition", TgcParserUtils.vector3ToFloat4Array(camera.getPosition()));
            const float lightIntensity = 4f;
            effect.SetValue("lightIntensity", lightIntensity);

            //Hdr
            Vector3 viewVec = Vector3.Normalize(camera.LookAt - camera.Position);
            Vector3 sunVec = Vector3.Normalize(sunMesh.Position - camera.Position);
            float viewSunDot = FastMath.Max(0, Vector3.Dot(viewVec, sunVec));
            float currentLum = viewSunDot > 0.9f ? lightIntensity : lightIntensity / 3;
            effect.SetValue("hdrExposure", (float)GuiController.Instance.Modifiers["hdrExposure"]);
            adaptedLum = adaptedLum + (currentLum - adaptedLum) * (1 - FastMath.Pow(0.98f, 30 * elapsedTime));
            effect.SetValue("avgHdrSceneLum", adaptedLum);
            //GuiController.Instance.Text3d.drawText("adaptedLum: " + adaptedLum, 0, 65, Color.White);

            //Cargar variables de Material
            effect.SetValue("materialAmbientColor", ColorValue.FromColor(Color.DarkGray));
            effect.SetValue("materialDiffuseColor", ColorValue.FromColor(Color.White));
            effect.SetValue("materialSpecularColor", ColorValue.FromColor(Color.White));
            effect.SetValue("materialSpecularExp", (float)GuiController.Instance.Modifiers["specular"]);

            //Matriz de view de la luz
            Matrix lightViewMat = Matrix.LookAtLH(sunMesh.Position, /*new Vector3(100, 0, -800)*/camera.Position, new Vector3(0, 0, 1));
            Matrix lightViewProj = lightViewMat * shadowProj;
            effect.SetValue("lightViewProj", lightViewProj);

            //Render de mesh de sol
            effect.Technique = "SUN_TECHNIQUE";
            sunMesh.render();


            //Render de Meshes
            bool drawMeshes = (bool)GuiController.Instance.Modifiers["drawMeshes"];
            bool shadows = (bool)GuiController.Instance.Modifiers["shadows"];
            if (drawMeshes)
            {
                if (shadows)
                {
                    //Generar shadowMap
                    renderShadowMap();
                    
                    //Dibujar meshes con shadow
                    effect.Technique = "DIFFUSE_MAP_SHADOW_MAP";
                    effect.SetValue("shadowMap_Tex", shadowMapTex);
                    renderMeshes();
                }
                else
                {
                    //Render sin shadows
                    effect.Technique = "DIFFUSE_MAP";
                    renderMeshes();
                }
            }

            //Render de occluders
            bool drawOccluders = (bool)GuiController.Instance.Modifiers["drawOccluders"];
            if (drawOccluders)
            {
                renderOccluders();
            }
            

            //Disparar misil
            shootMissile();



            //Efecto de sol
            bool wireframe = (bool)GuiController.Instance.Modifiers["wireframe"];
            if (!wireframe)
            {
                GpuOcclusionUtils.BoundingBox2D sunRect2d;
                bool sunScreenPosResult = !GpuOcclusionUtils.projectBoundingBox(sunMesh.BoundingBox, d3dDevice.Viewport, out sunRect2d);
                if (sunScreenPosResult)
                {
                    Vector2 sunScreenPos = sunRect2d.min + (sunRect2d.max - sunRect2d.min) * 0.5f;
                    sunSprite.Position = sunScreenPos - new Vector2((sunSprite.Texture.Width / 2) * sunSprite.Scaling.X + 5, (sunSprite.Texture.Height / 2) * sunSprite.Scaling.Y + 15);
                    //sunSprite.Color = Color.FromArgb((int)(255 * FastMath.Max(0, sunDot)), 255, 255, 255);
                    GuiController.Instance.Drawer2D.beginDrawSprite();
                    sunSprite.render();
                    GuiController.Instance.Drawer2D.endDrawSprite();
                }
            }
            

            


            //Contar la cantidad de objetos occluidos (es lento pero lo necesitamos para ver los resultados)
            bool[] occlusionResult = occlusionEngine.getVisibilityData();
            int modelosVisibles = 0;
            for (int i = 0; i < occlusionResult.Length; i++)
            {
                if (!occlusionResult[i])
                {
                    modelosVisibles++;
                }
            }

            //Dibujar wireframe (en realidad solo dibujamos AABB, es mas rapido que en wireframe)
            if (wireframe)
            {
                for (int i = 0; i < occlusionResult.Length; i++)
                {
                    if (occlusionResult[i])
                    {
                        occlusionEngine.EnabledOccludees[i].BoundingBox.render();
                    }
                }
            }


            //Dibujar los objetos oclutados por occlusion sin depth-test
            bool drawHidden = (bool)GuiController.Instance.Modifiers["drawHidden"];
            if (drawHidden)
            {
                d3dDevice.RenderState.ZBufferEnable = false;
                for (int i = 0; i < occlusionResult.Length; i++)
                {
                    if (!occlusionResult[i])
                    {
                        occlusionEngine.EnabledOccludees[i].BoundingBox.render();
                    }
                }
                d3dDevice.RenderState.ZBufferEnable = true;
            }



            
            //Render de ZBuffer
            bool zbufferEnabled = (bool)GuiController.Instance.Modifiers["zbuffer"];
            if(zbufferEnabled && occlusionCullEnabled)
            {
                zBufferSprite.render();
            }



            //Estadisticas
            GuiController.Instance.Text3d.drawText("Modelos dibujados: " + modelosVisibles + " de " + occlusionEngine.Occludees.Count, 0, 30, Color.White);
            GuiController.Instance.Text3d.drawText("Ganancia: " + (100 * (1 - ((float)modelosVisibles / occlusionEngine.Occludees.Count))) + "%", 0, 50, Color.White);





            d3dDevice.EndScene();
        }



        /// <summary>
        /// Generar ShadowMap
        /// </summary>
        private void renderShadowMap()
        {
            Device d3dDevice = GuiController.Instance.D3dDevice;

            //Dibujar a renderTarget
            d3dDevice.EndScene();
            Surface pOldRT = d3dDevice.GetRenderTarget(0);
            Surface pShadowSurf = shadowMapTex.GetSurfaceLevel(0);
            d3dDevice.SetRenderTarget(0, pShadowSurf);
            Surface pOldDS = d3dDevice.DepthStencilSurface;
            d3dDevice.DepthStencilSurface = depthStencilShadowMap;

            d3dDevice.BeginScene();
            d3dDevice.Clear(ClearFlags.Target | ClearFlags.ZBuffer, Color.White, 1.0f, 0);

            //Dibujar escena para generar shadow
            effect.Technique = "SHADOW_MAP_GENERATION";
            renderMeshes();

            //Restaurar todo
            d3dDevice.EndScene();
            d3dDevice.DepthStencilSurface = pOldDS;
            d3dDevice.SetRenderTarget(0, pOldRT);
            pShadowSurf.Dispose();
            pOldDS.Dispose();
            d3dDevice.BeginScene();


            //TextureLoader.Save(GuiController.Instance.ExamplesMediaDir + "shadowmap.bmp", ImageFileFormat.Bmp, shadowMapTex);
        }


        /// <summary>
        /// Dibujar meshes
        /// </summary>
        private void renderMeshes()
        {
            //Render de opacos
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                TgcMeshShader mesh = occlusionEngine.EnabledOccludees[i];
                if (!mesh.AlphaBlendEnable)
                {
                    //Cargar varibles de shader propias de Occlusion
                    occlusionEngine.setOcclusionShaderValues(effect, i);
                    mesh.render();
                }
            }
            //Render de alpha
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                TgcMeshShader mesh = occlusionEngine.EnabledOccludees[i];
                if (mesh.AlphaBlendEnable)
                {
                    //Cargar varibles de shader propias de Occlusion
                    occlusionEngine.setOcclusionShaderValues(effect, i);
                    mesh.render();
                }
            }
        }

        /// <summary>
        /// Dibujar occluders para debug
        /// </summary>
        private void renderOccluders()
        {
            for (int i = 0; i < occlusionEngine.Occluders.Count; i++)
            {
                Occluder occluder = occlusionEngine.Occluders[i];
                OccluderData ocData = occluderData[i];
                if (ocData.enabled)
                {
                    ocData.occluder.Aabb.render();
                    ocData.occluderBox.render();
                }
            }
        }

        /// <summary>
        /// Abrir o cerrar puerta principal
        /// </summary>
        private void openCloseMainDoor()
        {
            if (GuiController.Instance.D3dInput.keyPressed(Microsoft.DirectX.DirectInput.Key.E))
            {
                animatingDoorDir *= -1;
                animatingDoor = true;
                puertaOccluder.enabled = !puertaOccluder.enabled;
                if (animatingDoorDir == -1)
                {
                    puertaOccluder.disable(occlusionEngine);
                    puertaOccluder.mesh.Enabled = true;
                }
            }
            if (animatingDoor)
            {
                puertaOccluder.mesh.move(0, animatingDoorDir * 400 * GuiController.Instance.ElapsedTime, 0);
                if (animatingDoorDir == -1 && puertaOccluder.mesh.BoundingBox.PMax.Y <= 10)
                {
                    animatingDoor = false;
                } 
                else if(animatingDoorDir == 1 && puertaOccluder.mesh.BoundingBox.PMin.Y > -20)
                {
                    animatingDoor = false;
                    puertaOccluder.enable(occlusionEngine);
                }
            }
        }

        /// <summary>
        /// Disparar misil y actualizar estado de los ya disparados
        /// </summary>
        public void shootMissile()
        {
            const int missileLength = 30;
            const int missileSpeed = 2000;
            const float maxTime = 5;

            //Disparar nuevo misil
            if (GuiController.Instance.D3dInput.buttonPressed(TgcViewer.Utils.Input.TgcD3dInput.MouseButtons.BUTTON_RIGHT))
            {
                //Buscar misil disponible
                MissileData avaliableMissile = null;
                foreach (MissileData m in missilesData)
                {
                    if (!m.enabled)
                    {
                        avaliableMissile = m;
                        break;
                    }
                }

                //Arrancar misil
                if (avaliableMissile != null)
                {
                    avaliableMissile.reset();
                    avaliableMissile.enabled = true;
                    Vector3 start = new Vector3(camera.Position.X, camera.Position.Y - 10, camera.Position.Z);
                    Vector3 end = new Vector3(camera.LookAt.X, camera.LookAt.Y - 10, camera.LookAt.Z);
                    Vector3 dir = Vector3.Normalize(end - start);
                    avaliableMissile.missile.PStart = start;
                    avaliableMissile.missile.PEnd = start + dir * missileLength;

                    //Animacion de humo
                    avaliableMissile.smoke.Enabled = true;
                    avaliableMissile.smoke.reset();
                    avaliableMissile.smoke.Position = avaliableMissile.missile.PStart;
                }
            }

            //Avance de misiles disparados
            TgcBoundingBox missileAABB = new TgcBoundingBox();
            foreach (MissileData m in missilesData)
            {
                if (m.enabled)
                {
                    //Animacion de explosion
                    if (m.exploting)
                    {
                        m.explotion.LookAt = camera.Position;
                        m.explotion.updateAndRender();
                        if (!m.explotion.Playing)
                        {
                            //Se acabo la animacion
                            m.reset();
                        }
                    }
                    //Mover misil
                    else
                    {
                        //Mover y dibujar
                        float elapsedTime = GuiController.Instance.ElapsedTime;
                        m.currentTime += elapsedTime;
                        Vector3 dir = Vector3.Normalize(m.missile.PEnd - m.missile.PStart);
                        Vector3 movement = dir * missileSpeed * elapsedTime;
                        m.missile.PStart += movement;
                        m.missile.PEnd += movement;
                        m.missile.updateValues();
                        m.missile.render();

                        //Dibujar humo
                        m.smoke.Position = m.missile.PEnd;
                        m.smoke.LookAt = camera.Position;
                        m.smoke.updateAndRender();

                        //Tiempo maximo
                        if (m.currentTime > maxTime)
                        {
                            //Activar explosion
                            m.exploting = true;
                            m.explotion.Enabled = true;
                            m.explotion.reset();
                            m.explotion.Position = m.missile.PEnd;
                        }
                        else
                        {
                            //Calcular AABB del misil (aprox)
                            Vector3 missileCenter = m.missile.PStart + (m.missile.PStart - m.missile.PEnd) * 0.5f;
                            missileAABB.setExtremes(
                                missileCenter - new Vector3(missileLength / 4, missileLength / 4, missileLength / 4),
                                missileCenter + new Vector3(missileLength / 4, missileLength / 4, missileLength / 4));
                            //missileAABB.render();

                            //Buscar colision con el occluder mas cercano
                            OccluderData ocDataCollision = null;
                            float minDist = float.MaxValue;
                            for (int i = 0; i < occluderData.Count; i++)
                            {
                                OccluderData ocData = occluderData[i];
                                if (ocData.enabled)
                                {
                                    if (TgcCollisionUtils.testAABBAABB(ocData.occluder.Aabb, missileAABB))
                                    {
                                        float dist = Vector3.LengthSq(ocData.occluder.Aabb.calculateBoxCenter() - missileCenter);
                                        if (dist < minDist)
                                        {
                                            minDist = dist;
                                            ocDataCollision = ocData;
                                        }
                                        break;
                                    }
                                }
                            }

                            //Choco contra un occluder
                            if (ocDataCollision != null)
                            {
                                //Desactivar occluder
                                ocDataCollision.disable(occlusionEngine);

                                //Desactivar vecinos de este occluder
                                foreach (OccluderData vecino in ocDataCollision.vecinos)
                                {
                                    vecino.disable(occlusionEngine);
                                }

                                //Activar explosion
                                m.exploting = true;
                                m.explotion.Enabled = true;
                                m.explotion.reset();
                                m.explotion.Position = ocDataCollision.occluder.Aabb.calculateBoxCenter();
                            }
                            //Ver si choca contra el piso
                            else
                            {
                                if (TgcCollisionUtils.testAABBAABB(piso.BoundingBox, missileAABB))
                                {
                                    //Activar explosion
                                    m.exploting = true;
                                    m.explotion.Enabled = true;
                                    m.explotion.reset();
                                    m.explotion.Position = missileCenter;
                                }
                            }
                        }

                    }
                    

                }
            }
        }


        public override void close()
        {
            for (int i = 0; i < occlusionEngine.EnabledOccludees.Count; i++)
            {
                occlusionEngine.EnabledOccludees[i].dispose();
            }
            occlusionEngine.close();
            occlusionEngine = null;
            effect.Dispose();
            skyBox.dispose();
            sunMesh.dispose();
            foreach (OccluderData ocData in occluderData)
            {
                ocData.occluderBox.dispose();
            }
            sunSprite.dispose();
            foreach (MissileData data in missilesData)
            {
                data.explotion.dispose();
                data.missile.dispose();
            }
            zBufferSprite.dispose();
            depthStencilShadowMap.Dispose();
            shadowMapTex.Dispose();
        }


        /// <summary>
        /// Data aux para misiles
        /// </summary>
        private class MissileData
        {
            public TgcAnimatedBillboard explotion;
            public TgcAnimatedBillboard smoke;
            public TgcBoxLine missile;
            public bool enabled;
            public bool exploting;
            public float currentTime;

            public void reset()
            {
                enabled = false;
                exploting = false;
                currentTime = 0;
            }
        }

        /// <summary>
        /// Data aux para occluders
        /// </summary>
        private class OccluderData
        {
            public Occluder occluder;
            public TgcBox occluderBox;
            public TgcMeshShader mesh;
            public List<OccluderData> vecinos;
            public bool enabled;

            public void enable(OcclusionEngineParalellOccludee engine)
            {
                //Agregar al engine
                this.enabled = true;
                engine.Occluders.Add(occluder);

                //Activar todos los meshes que colisionan con este occluder
                mesh.Enabled = true;
            }

            public void disable(OcclusionEngineParalellOccludee engine)
            {
                //Quitar occluder del engine
                this.enabled = false;
                engine.Occluders.Remove(occluder);

                //Desactivar todos los meshes que colisionan con este occluder
                mesh.Enabled = false;
            }

        }

    }
}
