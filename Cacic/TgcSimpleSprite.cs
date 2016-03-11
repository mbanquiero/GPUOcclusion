using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX.Direct3D;
using TgcViewer.Utils.TgcSceneLoader;
using Microsoft.DirectX;
using System.Drawing;
using TgcViewer;
using TgcViewer.Utils;

namespace Examples.GpuOcclusion.Cacic
{
    /// <summary>
    /// Sprite 2D hecho a mano
    /// </summary>
    public class TgcSimpleSprite
    {

        VertexBuffer vertexBuffer;
        IndexBuffer indexBuffer;

        TgcTexture texture;
        /// <summary>
        /// Textura
        /// </summary>
        public TgcTexture Texture
        {
            get { return texture; }
            set { texture = value; }
        }

        Vector2 position;
        /// <summary>
        /// Posicion
        /// </summary>
        public Vector2 Position
        {
            get { return position; }
            set { position = value; }
        }

        Vector2 size;
        /// <summary>
        /// Tamaño en pixels
        /// </summary>
        public Vector2 Size
        {
            get { return size; }
            set { size = value; }
        }

        Color color;
        /// <summary>
        /// Color del Billboard
        /// </summary>
        public Color Color
        {
            get { return color; }
            set { color = value; }
        }

        bool enabled;
        /// <summary>
        /// Habilitar
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        Effect effect;
        /// <summary>
        /// Shader
        /// </summary>
        public Effect Effect
        {
          get { return effect; }
          set { effect = value; }
        }

        /// <summary>
        /// Crear nuevo
        /// </summary>
        public TgcSimpleSprite()
        {
            position = new Vector2(0, 0);
            size = new Vector2(100, 100);
            color = Color.White;
            enabled = true;

            vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionTextured), 4, GuiController.Instance.D3dDevice,
                Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionTextured.Format, Pool.Default);

            //Index buffer
            indexBuffer = new IndexBuffer(typeof(short), 6, GuiController.Instance.D3dDevice, Usage.WriteOnly, Pool.Default);
            short[] indexData = new short[6];
            indexData[0] = 3;
            indexData[1] = 0;
            indexData[2] = 1;
            indexData[3] = 1;
            indexData[4] = 2;
            indexData[5] = 3;
            indexBuffer.SetData(indexData, 0, LockFlags.None);
        }

        /// <summary>
        /// Actualizar varlores del Billboard
        /// </summary>
        public void updateValues()
        {
            CustomVertex.PositionTextured[] vertices = new CustomVertex.PositionTextured[6];

            int w = GuiController.Instance.D3dDevice.Viewport.Width;
            int h = GuiController.Instance.D3dDevice.Viewport.Height;

            //The 4 corners
            float left = position.X;
            float right = left + size.X;
            float top = position.Y;
            float bottom = top + size.Y;

            //UV coords
            Vector2 uvMin = new Vector2(0, 0);
            Vector2 uvMax = new Vector2(1, 1);

            //Normalize to projected values: X left/right: [-1, 1], Y top/bottom: [1, -1]
            left = -1 + left * 2 / w;
            right = -1 + right * 2 / w;
            top = 1 - top * 2 / h;
            bottom = 1 - bottom * 2 / h;

            //Updates vertexBuffer data
            vertices[0] = new CustomVertex.PositionTextured(new Vector3(left, top, 1), uvMin.X, uvMin.Y);
            vertices[1] = new CustomVertex.PositionTextured(new Vector3(right, top, 1), uvMax.X, uvMin.Y);
            vertices[2] = new CustomVertex.PositionTextured(new Vector3(right, bottom, 1), uvMax.X, uvMax.Y);
            vertices[3] = new CustomVertex.PositionTextured(new Vector3(left, bottom, 1), uvMin.X, uvMax.Y);

            //Cargar vertexBuffer
            vertexBuffer.SetData(vertices, 0, LockFlags.None);
        }

        /// <summary>
        /// Renderizar Billboard
        /// </summary>
        public void render()
        {
            if (!enabled)
                return;

            Device d3dDevice = GuiController.Instance.D3dDevice;
            TgcTexture.Manager texturesManager = GuiController.Instance.TexturesManager;

            d3dDevice.RenderState.AlphaTestEnable = true;
            d3dDevice.RenderState.AlphaBlendEnable = true;

            d3dDevice.Material = TgcD3dDevice.DEFAULT_MATERIAL;
            texturesManager.shaderSet(effect, "diffuseMap_Tex", texture);
            texturesManager.clear(1);

            effect.SetValue("color", ColorValue.FromColor(color));

            d3dDevice.VertexFormat = CustomVertex.PositionTextured.Format;
            d3dDevice.SetStreamSource(0, vertexBuffer, 0);
            d3dDevice.Indices = indexBuffer;
            effect.Begin(0);
            effect.BeginPass(0);
            d3dDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, 4, 0, 2);
            effect.EndPass();
            effect.End();

            d3dDevice.RenderState.AlphaTestEnable = false;
            d3dDevice.RenderState.AlphaBlendEnable = false;
        }

        /// <summary>
        /// Liberar recursos
        /// </summary>
        public void dispose()
        {
            texture.dispose();
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
        }

    }
}
