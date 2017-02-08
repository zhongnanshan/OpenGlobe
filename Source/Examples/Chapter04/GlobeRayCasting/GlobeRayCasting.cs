#region License
//
// (C) Copyright 2010 Patrick Cozzi and Deron Ohlarik
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using System.Drawing;

using OpenGlobe.Core;
using OpenGlobe.Renderer;
using OpenGlobe.Scene;

namespace OpenGlobe.Examples
{
    sealed class GlobeRayCasting : IDisposable
    {
        public GlobeRayCasting()
        {
            Ellipsoid globeShape = Ellipsoid.ScaledWgs84;

            _window = Device.CreateWindow(800, 600, "Chapter 4:  Globe Ray Casting");
            _window.Resize += OnResize;
            _window.RenderFrame += OnRenderFrame;
            //_sceneState = new SceneState();
            //_camera = new CameraLookAtPoint(_sceneState.Camera, _window, globeShape);
            SetupVR(globeShape);
            _clearState = new ClearState();

            //_window.Keyboard.KeyDown += delegate(object sender, KeyboardKeyEventArgs e)
            //{
            //    if (e.Key == KeyboardKey.P)
            //    {
            //        CenterCameraOnPoint();
            //    }
            //    else if (e.Key == KeyboardKey.C)
            //    {
            //        CenterCameraOnGlobeCenter();
            //    }
            //};

            _leftEyeFrameBuffer = _window.Context.CreateFramebuffer();
            _rightEyeFrameBuffer = _window.Context.CreateFramebuffer();

            _doubleViewportQuad = new DoubleViewportQuad(_window.Context);

            Bitmap bitmap = new Bitmap("NE2_50M_SR_W_4096.jpg");
            _texture = Device.CreateTexture2D(bitmap, TextureFormat.RedGreenBlue8, false);

            _globe = new RayCastedGlobe(_window.Context);
            _globe.Shape = globeShape;
            _globe.Texture = _texture;
            _globe.ShowWireframeBoundingBox = true;

            _leftEyeSceneState.Camera.ZoomToTarget(globeShape.MaximumRadius);
            // 相机参数变换需要更新SceneState的内部变换矩阵
            UpdateLeftEyeSceneState();
            _rightEyeSceneState.Camera.ZoomToTarget(globeShape.MaximumRadius);
            // 相机参数变换需要更新SceneState的内部变换矩阵
            UpdateRightEyeSceneState();
        }

        private void SetupVR(Ellipsoid globeShape)
        {
            // 创建一个默认相机以方便计算SceneState对象在VR模式下创建时需要外部提供的变换矩阵
            Camera camera = new Camera();
            Matrix4D perspectiveMatrix = Matrix4D.CreatePerspectiveFieldOfView(camera.FieldOfViewY, 
                camera.AspectRatio, camera.PerspectiveNearPlaneDistance, camera.PerspectiveFarPlaneDistance);
            Matrix4D viewMatrix = Matrix4D.LookAt(camera.Eye, camera.Target, camera.Up);

            _leftEyeSceneState = new SceneState(VREye.LeftEye, perspectiveMatrix, viewMatrix);
            _leftEyeCamera = new CameraLookAtPoint(_leftEyeSceneState.Camera, _window, globeShape);
            _leftEyeSceneState.HmdPoseMatrix = Matrix4D.Identity;

            _rightEyeSceneState = new SceneState(VREye.RightEye, perspectiveMatrix, viewMatrix);
            _rightEyeCamera = new CameraLookAtPoint(_rightEyeSceneState.Camera, _window, globeShape);
            _rightEyeSceneState.HmdPoseMatrix = Matrix4D.Identity;
        }

        private void UpdateLeftEyeSceneState()
        {
            Matrix4D perspectiveMatrix = Matrix4D.CreatePerspectiveFieldOfView(_leftEyeSceneState.Camera.FieldOfViewY,
                _leftEyeSceneState.Camera.AspectRatio, _leftEyeSceneState.Camera.PerspectiveNearPlaneDistance,
                _leftEyeSceneState.Camera.PerspectiveFarPlaneDistance);
            Matrix4D viewMatrix = Matrix4D.LookAt(_leftEyeSceneState.Camera.Eye, _leftEyeSceneState.Camera.Target,
                _leftEyeSceneState.Camera.Up);

            // 外部更新SceneState的内部变换矩阵以方便VR设备变换矩阵更新
            _leftEyeSceneState.PerspectiveMatrix = perspectiveMatrix;
            _leftEyeSceneState.ViewMatrix = viewMatrix;
            _leftEyeSceneState.HmdPoseMatrix = Matrix4D.Identity;
        }

        private void UpdateRightEyeSceneState()
        {
            Matrix4D perspectiveMatrix = Matrix4D.CreatePerspectiveFieldOfView(_rightEyeSceneState.Camera.FieldOfViewY,
                _rightEyeSceneState.Camera.AspectRatio, _rightEyeSceneState.Camera.PerspectiveNearPlaneDistance,
                _rightEyeSceneState.Camera.PerspectiveFarPlaneDistance);
            Matrix4D viewMatrix = Matrix4D.LookAt(_rightEyeSceneState.Camera.Eye, _rightEyeSceneState.Camera.Target,
                _rightEyeSceneState.Camera.Up);

            // 外部更新SceneState的内部变换矩阵以方便VR设备变换矩阵更新
            _rightEyeSceneState.PerspectiveMatrix = perspectiveMatrix;
            _rightEyeSceneState.ViewMatrix = viewMatrix;
            _rightEyeSceneState.HmdPoseMatrix = Matrix4D.Identity;
        }

        private void UpdateFramebufferAttachments()
        {
            DisposeFramebufferAttachments();

            _leftEyeColorTexture = Device.CreateTexture2D(
                new Texture2DDescription(_window.Width, _window.Height, TextureFormat.RedGreenBlueAlpha8, false));
            _leftEyeDepthTexture = Device.CreateTexture2D(
                new Texture2DDescription(_window.Width, _window.Height, TextureFormat.Depth24, false));

            _leftEyeFrameBuffer.ColorAttachments[0] = _leftEyeColorTexture;
            _leftEyeFrameBuffer.DepthAttachment = _leftEyeDepthTexture;

            _rightEyeColorTexture = Device.CreateTexture2D(
                new Texture2DDescription(_window.Width, _window.Height, TextureFormat.RedGreenBlueAlpha8, false));
            _rightEyeDepthTexture = Device.CreateTexture2D(
                new Texture2DDescription(_window.Width, _window.Height, TextureFormat.Depth24, false));

            _rightEyeFrameBuffer.ColorAttachments[0] = _rightEyeColorTexture;
            _rightEyeFrameBuffer.DepthAttachment = _rightEyeDepthTexture;

            _doubleViewportQuad.LeftEyeTexture = _leftEyeColorTexture;
            _doubleViewportQuad.RightEyeTexture = _rightEyeColorTexture;
        }

        private void OnResize()
        {
            _window.Context.Viewport = new Rectangle(0, 0, _window.Width, _window.Height);
            _leftEyeSceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;
            UpdateLeftEyeSceneState();
            _rightEyeSceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;
            UpdateRightEyeSceneState();

            // 窗口尺寸改变需要更新
            UpdateFramebufferAttachments();
        }

        private void OnRenderFrame()
        {
            Context context = _window.Context;

            // 渲染左眼
            context.Framebuffer = _leftEyeFrameBuffer;
            context.Clear(_clearState);

            _globe.Render(context, _leftEyeSceneState);

            // 渲染右眼
            context.Framebuffer = _rightEyeFrameBuffer;
            context.Clear(_clearState);

            _globe.Render(context, _rightEyeSceneState);

            context.Framebuffer = null;
            _doubleViewportQuad.Render(context, _leftEyeSceneState, _rightEyeSceneState);
        }

        //private void CenterCameraOnPoint()
        //{
        //    _camera.ViewPoint(_globe.Shape, new Geodetic3D(Trig.ToRadians(-75.697), Trig.ToRadians(40.039), 0.0));
        //    _camera.Azimuth = 0.0;
        //    _camera.Elevation = Math.PI / 4.0;
        //    _camera.Range = _globe.Shape.MaximumRadius * 3.0;
        //}

        //private void CenterCameraOnGlobeCenter()
        //{
        //    _camera.CenterPoint = Vector3D.Zero;
        //    _camera.FixedToLocalRotation = Matrix3D.Identity;
        //    _camera.Azimuth = 0.0;
        //    _camera.Elevation = 0.0;
        //    _camera.Range = _globe.Shape.MaximumRadius * 3.0;
        //}

        #region IDisposable Members

        public void Dispose()
        {
            _texture.Dispose();
            _globe.Dispose();
            _leftEyeCamera.Dispose();
            _rightEyeCamera.Dispose();

            _doubleViewportQuad.Dispose();
            DisposeFramebufferAttachments();
            _leftEyeFrameBuffer.Dispose();
            _rightEyeFrameBuffer.Dispose();

            _window.Dispose();
        }

        #endregion

        private void DisposeFramebufferAttachments()
        {
            if (_leftEyeColorTexture != null)
            {
                _leftEyeColorTexture.Dispose();
                _leftEyeColorTexture = null;
            }

            if (_leftEyeDepthTexture != null)
            {
                _leftEyeDepthTexture.Dispose();
                _leftEyeDepthTexture = null;
            }

            if (_rightEyeColorTexture != null)
            {
                _rightEyeColorTexture.Dispose();
                _rightEyeColorTexture = null;
            }

            if (_rightEyeDepthTexture != null)
            {
                _rightEyeDepthTexture.Dispose();
                _rightEyeDepthTexture = null;
            }
        }

        private void Run(double updateRate)
        {
            _window.Run(updateRate);
        }

        static void Main()
        {
            using (GlobeRayCasting example = new GlobeRayCasting())
            {
                example.Run(30.0);
            }
        }

        private readonly GraphicsWindow _window;
        private SceneState _leftEyeSceneState;
        private SceneState _rightEyeSceneState;
        private CameraLookAtPoint _leftEyeCamera;
        private CameraLookAtPoint _rightEyeCamera;
        private readonly ClearState _clearState;
        private readonly RayCastedGlobe _globe;
        private readonly Texture2D _texture;

        private Framebuffer _leftEyeFrameBuffer;
        private Framebuffer _rightEyeFrameBuffer;
        private Texture2D _leftEyeColorTexture;
        private Texture2D _rightEyeColorTexture;
        private Texture2D _leftEyeDepthTexture;
        private Texture2D _rightEyeDepthTexture;

        private DoubleViewportQuad _doubleViewportQuad;
    }
}