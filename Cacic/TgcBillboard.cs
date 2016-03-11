using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX;
using System.Drawing;
using TgcViewer.Utils.TgcSceneLoader;
using Microsoft.DirectX.Direct3D;
using TgcViewer;
using TgcViewer.Utils.TgcGeometry;
using TgcViewer.Utils;

namespace Examples.GpuOcclusion.Cacic
{
    /// <summary>
    /// Billboard
    /// </summary>
    public class TgcBillboard
    {

        const int VERTEX_COUNT = 6;
        static readonly Vector3 ORIGINAL_DIR = new Vector3(0, 0, -1);


        Rectangle srcRect;
        /// <summary>
        /// Region rectangular a dibujar de la textura
        /// </summary>
        public Rectangle SrcRect
        {
            get { return srcRect; }
            set { srcRect = value; }
        }

        TgcTexture texture;
        /// <summary>
        /// Textura del Sprite.
        /// </summary>
        public TgcTexture Texture
        {
            get { return texture; }
            set { texture = value; }
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

        protected Vector3 position;
        /// <summary>
        /// Posicion del Billboard
        /// </summary>
        public Vector3 Position
        {
            get { return position; }
            set { position = value; }
        }

        protected Vector3 lookAt;
        /// <summary>
        /// Hacia que punto mira el Billboard
        /// </summary>
        public Vector3 LookAt
        {
            get { return lookAt; }
            set { lookAt = value; }
        }

        protected Vector2 size;
        /// <summary>
        /// Tamaño del Billboard en Width y Height
        /// </summary>
        public Vector2 Size
        {
            get { return size; }
            set { size = value; }
        }

        bool enabled;
        /// <summary>
        /// Habilitar Billboard
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }





        VertexBuffer vertexBuffer;

        /// <summary>
        /// Crear un nuevo Billboard
        /// </summary>
        public TgcBillboard()
        {
            initialize();
        }

        /// <summary>
        /// Cargar valores iniciales
        /// </summary>
        public void initialize()
        {
            //Rectangulo vacio
            srcRect = Rectangle.Empty;

            //Propiedades de transformacion default
            position = new Vector3(0, 0, 0);
            size = new Vector2(100, 100);
            lookAt = new Vector3(0, 1, 0);

            ///color = Color.FromArgb(0, 0, 0, 0);
            color = Color.White;
            enabled = true;

            vertexBuffer = new VertexBuffer(typeof(CustomVertex.PositionColoredTextured), VERTEX_COUNT, GuiController.Instance.D3dDevice,
                Usage.Dynamic | Usage.WriteOnly, CustomVertex.PositionColoredTextured.Format, Pool.Default);
        }

        /// <summary>
        /// Actualizar varlores del Billboard
        /// </summary>
        public void updateValues()
        {
            CustomVertex.PositionColoredTextured[] vertices = new CustomVertex.PositionColoredTextured[6];

            //Crear un Quad con dos triángulos sobre XY con normal default (0, 0, 1)
            Vector3 min = new Vector3(-size.X / 2, -size.Y / 2, 0);
            Vector3 max = new Vector3(size.X / 2, size.Y / 2, 0);
            int c = color.ToArgb();

            //UV values
            Vector2 uvMin;
            Vector2 uvMax;
            if (srcRect == Rectangle.Empty)
            {
                uvMin = new Vector2(0, 0);
                uvMax = new Vector2(1, 1);
            }
            else
            {
                uvMin = new Vector2(srcRect.X / (float)texture.Width, srcRect.Y / (float)texture.Height);
                uvMax = new Vector2((srcRect.X + srcRect.Width) / (float)texture.Width, (srcRect.Y + srcRect.Height) / (float)texture.Height);
            }

            vertices[0] = new CustomVertex.PositionColoredTextured(min, c, uvMin.X, uvMax.Y);
            vertices[1] = new CustomVertex.PositionColoredTextured(min.X, max.Y, 0, c, uvMin.X, uvMin.Y);
            vertices[2] = new CustomVertex.PositionColoredTextured(max, c, uvMax.X, uvMin.Y);

            vertices[3] = new CustomVertex.PositionColoredTextured(min, c, uvMin.X, uvMax.Y);
            vertices[4] = new CustomVertex.PositionColoredTextured(max, c, uvMax.X, uvMin.Y);
            vertices[5] = new CustomVertex.PositionColoredTextured(max.X, min.Y, 0, c, uvMax.X, uvMax.Y);

            //A donde apuntar
            Vector3 normal = lookAt - position;
            normal.Y = 0;
            normal.Normalize();

            //Rotar en Y
            float angle = FastMath.Acos(Vector3.Dot(ORIGINAL_DIR, normal));
            Vector3 axis = Vector3.Cross(ORIGINAL_DIR, normal);
            axis.Normalize();
            Matrix t = Matrix.RotationAxis(axis, angle) * Matrix.Translation(position);

            //Transformar todos los puntos
            for (int i = 0; i < vertices.Length; i++)
            {
                vertices[i].Position = Vector3.TransformCoordinate(vertices[i].Position, t);
            }

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
            d3dDevice.Transform.World = Matrix.Identity;

            d3dDevice.SetTexture(0, texture.D3dTexture);
            texturesManager.clear(1);

            d3dDevice.VertexFormat = CustomVertex.PositionColoredTextured.Format;
            d3dDevice.SetStreamSource(0, vertexBuffer, 0);
            d3dDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, 2);

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
        }
    }

}
