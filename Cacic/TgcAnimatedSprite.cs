using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using TgcViewer.Utils.TgcSceneLoader;
using TgcViewer;

namespace Examples.GpuOcclusion.Cacic
{
    /// <summary>
    /// Billboard animado
    /// </summary>
    public class TgcAnimatedBillboard
    {
        Size frameSize;
        int totalFrames;
        float currentTime;
        float animationTimeLenght;
        int framesPerRow;
        float textureWidth;
        float textureHeight;

        protected bool enabled;
        /// <summary>
        /// Indica si el billboard esta habilitado para ser renderizada
        /// </summary>
        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }

        protected bool playing;
        /// <summary>
        /// Arrancar o parar avance de animacion
        /// </summary>
        public bool Playing
        {
            get { return playing; }
            set { playing = value; }
        }

        TgcBillboard billboard;
        /// <summary>
        /// billboard con toda la textura a animar
        /// </summary>
        public TgcBillboard Billboard
        {
            get { return billboard; }
        }

        protected float frameRate;
        /// <summary>
        /// Velocidad de la animacion medida en cuadros por segundo.
        /// </summary>
        public float FrameRate
        {
            get { return frameRate; }
        }

        protected int currentFrame;
        /// <summary>
        /// Frame actual de la textura animada
        /// </summary>
        public int CurrentFrame
        {
            get { return currentFrame; }
            set { currentFrame = value; }
        }

        /// <summary>
        /// Posicion del billboard
        /// </summary>
        public Vector3 Position
        {
            get { return billboard.Position; }
            set { billboard.Position = value; }
        }

        /// <summary>
        /// LookAt del billboard
        /// </summary>
        public Vector3 LookAt
        {
            get { return billboard.LookAt; }
            set { billboard.LookAt = value; }
        }

        /// <summary>
        /// Tamaño del billboard
        /// </summary>
        public Vector2 Size
        {
            get { return billboard.Size; }
            set { billboard.Size = value; }
        }

        bool loop;
        /// <summary>
        /// Hacer loop al terminar
        /// </summary>
        public bool Loop
        {
            get { return loop; }
            set { loop = value; }
        }


        /// <summary>
        /// Crear un nuevo Billboard animado
        /// </summary>
        /// <param name="texturePath">path de la textura animada</param>
        /// <param name="frameSize">tamaño de un tile de la animacion</param>
        /// <param name="totalFrames">cantidad de frames que tiene la animacion</param>
        /// <param name="frameRate">velocidad en cuadros por segundo</param>
        public TgcAnimatedBillboard(string texturePath, Size frameSize, int totalFrames, float frameRate)
        {
            this.enabled = true;
            this.loop = true;
            this.frameSize = frameSize;
            this.totalFrames = totalFrames;
            reset();            
            

            //Crear textura
            Device d3dDevice = GuiController.Instance.D3dDevice;
            TgcTexture texture = TgcTexture.createTexture(d3dDevice, texturePath);

            //Sprite
            billboard = new TgcBillboard();
            billboard.Texture = texture;

            //Calcular valores de frames de la textura
            textureWidth = texture.Width;
            textureHeight = texture.Height;
            framesPerRow = (int)textureWidth / frameSize.Width;
            int framesPerCol = (int)textureHeight / frameSize.Height;
            int realTotalFrames = framesPerRow * framesPerCol;
            if (totalFrames > realTotalFrames)
            {
                throw new Exception("Error en AnimatedBillboard. No coinciden la cantidad de frames y el tamaño de la textura: " + totalFrames);
            }

            setFrameRate(frameRate);
        }

        /// <summary>
        /// Resetear animacion
        /// </summary>
        public void reset()
        {
            this.currentTime = 0;
            this.currentFrame = 0;
            this.playing = true;
        }

        /// <summary>
        /// Cambiar la velocidad de animacion
        /// </summary>
        public void setFrameRate(float frameRate)
        {
            this.frameRate = frameRate;
            animationTimeLenght = (float)totalFrames / frameRate;
        }

        /// <summary>
        /// Actualizar frame de animacion
        /// </summary>
        public void update()
        {
            if (!enabled)
                return;

            //Avanzar tiempo
            if (playing)
            {
                currentTime += GuiController.Instance.ElapsedTime;
                if (currentTime > animationTimeLenght)
                {
                    if (loop)
                    {
                        //Reiniciar al llegar al final
                        currentTime = 0;
                    }
                    else
                    {
                        playing = false;
                    }
                }
            }

            //Obtener cuadro actual
            currentFrame = (int)(currentTime * frameRate);

            //Obtener rectangulo de dibujado de la textura para este frame
            Rectangle srcRect = new Rectangle();
            srcRect.X = frameSize.Width * (currentFrame % framesPerRow);
            srcRect.Width = frameSize.Width;
            srcRect.Y = frameSize.Height * (currentFrame / framesPerRow);
            srcRect.Height = frameSize.Height;
            billboard.SrcRect = srcRect;

            billboard.updateValues();
        }

        /// <summary>
        /// Renderizar Sprite.
        /// Se debe llamar primero a update().
        /// Sino se dibuja el ultimo estado actualizado.
        /// </summary>
        public void render()
        {
            if (!enabled)
                return;

            //Dibujar sprite
            billboard.render();
        }

        /// <summary>
        /// Actualiza la animacion y dibuja el Sprite
        /// </summary>
        public void updateAndRender()
        {
            update();
            render();
        }

        /// <summary>
        /// Liberar recursos
        /// </summary>
        public void dispose()
        {
            billboard.dispose();
        }

    }
}
