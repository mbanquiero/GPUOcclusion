using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.DirectX;
using Microsoft.DirectX.Direct3D;
using Microsoft.DirectX.DirectInput;
using TgcViewer.Utils.TgcSceneLoader;
using TgcViewer.Utils.TgcGeometry;
using TgcViewer.Utils.Input;
using TgcViewer;

namespace Examples.GpuOcclusion.Cacic
{
    /// <summary>
    /// Cámara en primera persona para Cacic
    /// Basado en: http://www.toymaker.info/Games/html/camera.html
    /// </summary>
    public class CacicFpsCamera : TgcCamera
    {
        //Constantes de movimiento
        const float DEFAULT_ROTATION_SPEED = 2f;
        const float DEFAULT_MOVEMENT_SPEED = 1f;
        const float DEFAULT_JUMP_SPEED = 1f;

        //Rotacion actual acumulada
        float yaw;
        float pitch;

        //Matriz final de view
        Matrix viewMatrix;


        #region Getters y Setters

        bool enable;
        /// <summary>
        /// Habilita o no el uso de la camara
        /// </summary>
        public bool Enable
        {
            get { return enable; }
            set
            {
                enable = value;

                //Si se habilito la camara, cargar como la cámara actual
                if (value)
                {
                    GuiController.Instance.CurrentCamera = this;
                }
            }
        }

        float movementSpeed;
        /// <summary>
        /// Velocidad de desplazamiento de los ejes XZ de la cámara
        /// </summary>
        public float MovementSpeed
        {
            get { return movementSpeed; }
            set { movementSpeed = value; }
        }

        float jumpSpeed;
        /// <summary>
        /// Velocidad de desplazamiento del eje Y de la cámara
        /// </summary>
        public float JumpSpeed
        {
            get { return jumpSpeed; }
            set { jumpSpeed = value; }
        }

        float rotationSpeed;
        /// <summary>
        /// Velocidad de rotacion de la cámara
        /// </summary>
        public float RotationSpeed
        {
            get { return rotationSpeed; }
            set { rotationSpeed = value; }
        }

        Vector3 position;
        /// <summary>
        /// Posicion actual de la camara
        /// </summary>
        public Vector3 Position
        {
            get { return position; }
            set { position = value; }
        }

        Vector3 lookAt;
        /// <summary>
        /// Punto hacia donde mira la cámara
        /// </summary>
        public Vector3 LookAt
        {
            get { return lookAt; }
        }

        bool moveAlongY;
        /// <summary>
        /// En True la camara se mueve en Y tambien segun la inclinacion del mouse
        /// </summary>
        public bool MoveAlongY
        {
            get { return moveAlongY; }
            set { moveAlongY = value; }
        }


        #endregion


        /// <summary>
        /// Crea la cámara con valores iniciales.
        /// Aceleración desactivada por Default
        /// </summary>
        public CacicFpsCamera()
        {
            resetValues();
        }

        /// <summary>
        /// Carga los valores default de la camara
        /// </summary>
        public void resetValues()
        {
            rotationSpeed = DEFAULT_ROTATION_SPEED;
	        movementSpeed = DEFAULT_MOVEMENT_SPEED;
	        jumpSpeed = DEFAULT_JUMP_SPEED;
	        position = new Vector3(0, 0, 0);
	        lookAt = new Vector3(0, 0, 0);
	        yaw = 0;
            pitch = 0;
            moveAlongY = false;
        }

        /// <summary>
        /// Configura la posicion de la cámara
        /// </summary>
        public void setCamera(Vector3 pos, Vector3 lookAt)
        {
            this.position = pos;
            this.lookAt = lookAt;

            //Calcular base ortonormal
            Vector3 look = Vector3.Normalize(lookAt - pos);
            //Vector3 look = new Vector3(0, 0, 1);
            Vector3 right = Vector3.Cross(new Vector3(0, 1, 0), look);
            Vector3 up = Vector3.Cross(look, right);

            //Calcular diferencia con la base original
            //Vector3 lookDiff = Vector3.Normalize(look - new Vector3(0, 0, 1));
            //Vector3 rightDiff = Vector3.Normalize(right - new Vector3(1, 0, 0));
            //Vector3 upDiff = Vector3.Normalize(up - new Vector3(0, 1, 0));

            
            Vector3 lookOrig = new Vector3(0, 0, 1);
            Vector3 lookXZ = Vector3.Normalize(new Vector3(look.X, 0, look.Z));
            this.yaw = -FastMath.Acos(Vector3.Dot(lookOrig, lookXZ));

            Vector3 upOrig = new Vector3(0, 1, 0);
            Vector3 upY = Vector3.Normalize(new Vector3(0, up.Y, 0));
            this.pitch = FastMath.Acos(Vector3.Dot(upOrig, upY));
            
            //Completar View Matrix
            updateViewMatrix(look, up, right, position);
        }


        /// <summary>
        /// Actualiza los valores de la camara
        /// </summary>
        public void updateCamera()
        {
            //Si la camara no está habilitada, no procesar el resto del input
            if (!enable)
            {
                return;
            }

            float elapsedTime = GuiController.Instance.ElapsedTime;
            TgcD3dInput d3dInput = GuiController.Instance.D3dInput;
            float forwardMovement = 0;
            float strafeMovement = 0;
            float jumpMovement = 0;
            float pitchRot = 0;
            float yawRot = 0;
            bool moving = false;
            bool rotating = false;


            //Imprimir por consola la posicion actual de la camara
            if ((d3dInput.keyDown(Key.LeftShift) || d3dInput.keyDown(Key.RightShift)) && d3dInput.keyPressed(Key.P))
            {
                GuiController.Instance.Logger.log(getPositionCode());
                return;
            }


            //Check keyboard input
            if(d3dInput.keyDown(Key.W)) 
	        {
                forwardMovement = this.movementSpeed;
                moving = true;
            } 
	        else if(d3dInput.keyDown(Key.S)) 
	        {
                forwardMovement = -this.movementSpeed;
                moving = true;
            }
            if(d3dInput.keyDown(Key.A)) 
	        {
                strafeMovement = -this.movementSpeed;
                moving = true;
            } 
	        else if(d3dInput.keyDown(Key.D)) 
	        {
                strafeMovement = this.movementSpeed;
                moving = true;
            }
            if(d3dInput.keyDown(Key.Space)) 
	        {
                jumpMovement = this.jumpSpeed;
		        moving = true;
	        } else if(d3dInput.keyDown(Key.LeftControl)) 
	        {
                jumpMovement = -this.jumpSpeed;
                moving = true;
            }
        
            //Check mouse input
            if(d3dInput.buttonDown(TgcD3dInput.MouseButtons.BUTTON_LEFT)) 
	        {
		        yawRot = d3dInput.XposRelative * this.rotationSpeed;
                pitchRot = d3dInput.YposRelative * this.rotationSpeed;
                rotating = true;
            }
        

            //Acumulate rotation
            this.yaw += yawRot * elapsedTime;
            this.pitch += pitchRot * elapsedTime;

            //Clamp pitch between [-PI/2, PI/2]
            this.pitch = this.pitch > FastMath.PI_HALF ? FastMath.PI_HALF : this.pitch;
            this.pitch = this.pitch < -FastMath.PI_HALF ? -FastMath.PI_HALF : this.pitch;
        
            /*
            //Clamp Y rotation between [0, 2PI]
            this.currentRotY = this.currentRotY > FastMath.TWO_PI ? this.currentRotY - FastMath.TWO_PI : this.currentRotY;
            this.currentRotY = this.currentRotY < 0 ? FastMath.TWO_PI + this.currentRotY : this.currentRotY;
            */

            //Vectores de base ortonormal
            Vector3 up = new Vector3(0, 1, 0);
            Vector3 look = new Vector3(0, 0, 1);
            Vector3 right = new Vector3(1, 0, 0);  

            //Yaw rotation
            Matrix mYaw = Matrix.RotationAxis(up, yaw);
            look = Vector3.TransformCoordinate(look, mYaw);
            right = Vector3.TransformCoordinate(right, mYaw);

            //Pitch rotation
            Matrix mPitch = Matrix.RotationAxis(right, pitch);
            look = Vector3.TransformCoordinate(look, mPitch);
            up = Vector3.TransformCoordinate(up, mPitch);


            float lastPosY = position.Y;

            //Forward-backward movement
            this.position += look * forwardMovement * elapsedTime;

            //Strafe movement
            this.position += right * strafeMovement * elapsedTime;

            //Jump-crouch movement
            if (moveAlongY)
            {
                this.position += up * jumpMovement * elapsedTime;
            }
            else
            {
                this.position.Y = lastPosY + jumpMovement * elapsedTime;
            }
            
            

            //Guardar lookAt para no calcularlo cada vez que lo pidan
            this.lookAt = position + look;



            //Completar View Matrix
            updateViewMatrix(look, up, right, position);
        }

        /// <summary>
        /// Completar matriz de view
        /// </summary>
        private void updateViewMatrix(Vector3 look, Vector3 up, Vector3 right, Vector3 pos)
        {
            viewMatrix = Matrix.Identity;

            //Right
            viewMatrix.M11 = right.X;
            viewMatrix.M21 = right.Y;
            viewMatrix.M31 = right.Z;

            //Up
            viewMatrix.M12 = up.X;
            viewMatrix.M22 = up.Y;
            viewMatrix.M32 = up.Z;

            //Look
            viewMatrix.M13 = look.X;
            viewMatrix.M23 = look.Y;
            viewMatrix.M33 = look.Z;

            //Position
            viewMatrix.M41 = -Vector3.Dot(pos, right);
            viewMatrix.M42 = -Vector3.Dot(pos, up);
            viewMatrix.M43 = -Vector3.Dot(pos, look);
        }

        /// <summary>
        /// Actualiza la ViewMatrix, si es que la camara esta activada
        /// </summary>
        public void updateViewMatrix(Microsoft.DirectX.Direct3D.Device d3dDevice)
        {
            if (!enable)
            {
                return;
            }

            d3dDevice.Transform.View = viewMatrix;
        }


        public Vector3 getPosition()
        {
            return this.position;
        }

        public Vector3 getLookAt()
        {
            return this.lookAt;
        }

        /// <summary>
        /// String de codigo para setear la camara desde GuiController, con la posicion actual y direccion de la camara
        /// </summary>
        public string getPositionCode()
        {
            //TODO ver de donde carajo sacar el LookAt de esta camara
            Vector3 lookAt = this.LookAt;

            return "GuiController.Instance.setCamera(new Vector3(" +
                TgcParserUtils.printFloat(position.X) + "f, " + TgcParserUtils.printFloat(position.Y) + "f, " + TgcParserUtils.printFloat(position.Z) + "f), new Vector3(" +
                TgcParserUtils.printFloat(lookAt.X) + "f, " + TgcParserUtils.printFloat(lookAt.Y) + "f, " + TgcParserUtils.printFloat(lookAt.Z) + "f));";
        }



    }
}
