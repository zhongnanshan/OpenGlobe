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
            _sceneState = new SceneState();
            _camera = new CameraLookAtPoint(_sceneState.Camera, _window, globeShape);
            _clearState = new ClearState();

            _window.Keyboard.KeyDown += delegate(object sender, KeyboardKeyEventArgs e)
            {
                if (e.Key == KeyboardKey.P)
                {
                    CenterCameraOnPoint();
                }
                else if (e.Key == KeyboardKey.C)
                {
                    CenterCameraOnGlobeCenter();
                }
            };

            _frameBuffer = _window.Context.CreateFramebuffer();

            _doubleViewportQuad = new DoubleViewportQuad(_window.Context);

            Bitmap bitmap = new Bitmap("NE2_50M_SR_W_4096.jpg");
            _texture = Device.CreateTexture2D(bitmap, TextureFormat.RedGreenBlue8, false);

            _globe = new RayCastedGlobe(_window.Context);
            _globe.Shape = globeShape;
            _globe.Texture = _texture;
            _globe.ShowWireframeBoundingBox = true;

            _sceneState.Camera.ZoomToTarget(globeShape.MaximumRadius);
        }

        private void UpdateFramebufferAttachments()
        {
            DisposeFramebufferAttachments();

            _colorTexture = Device.CreateTexture2D(
                new Texture2DDescription(_window.Width, _window.Height, TextureFormat.RedGreenBlueAlpha8, false));
            _depthTexture = Device.CreateTexture2D(
                new Texture2DDescription(_window.Width, _window.Height, TextureFormat.Depth24, false));

            _frameBuffer.ColorAttachments[0] = _colorTexture;
            _frameBuffer.DepthAttachment = _depthTexture;

            _doubleViewportQuad.LeftEyeTexture = _colorTexture;
            _doubleViewportQuad.RightEyeTexture = _colorTexture;
        }

        private void OnResize()
        {
            _window.Context.Viewport = new Rectangle(0, 0, _window.Width, _window.Height);
            _sceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;

            UpdateFramebufferAttachments();
        }

        private void OnRenderFrame()
        {
            Context context = _window.Context;

            context.Framebuffer = _frameBuffer;
            context.Clear(_clearState);

            _globe.Render(context, _sceneState);

            context.Framebuffer = null;
            _doubleViewportQuad.Render(context, _sceneState);
        }

        private void CenterCameraOnPoint()
        {
            _camera.ViewPoint(_globe.Shape, new Geodetic3D(Trig.ToRadians(-75.697), Trig.ToRadians(40.039), 0.0));
            _camera.Azimuth = 0.0;
            _camera.Elevation = Math.PI / 4.0;
            _camera.Range = _globe.Shape.MaximumRadius * 3.0;
        }

        private void CenterCameraOnGlobeCenter()
        {
            _camera.CenterPoint = Vector3D.Zero;
            _camera.FixedToLocalRotation = Matrix3D.Identity;
            _camera.Azimuth = 0.0;
            _camera.Elevation = 0.0;
            _camera.Range = _globe.Shape.MaximumRadius * 3.0;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _texture.Dispose();
            _globe.Dispose();
            _camera.Dispose();

            _doubleViewportQuad.Dispose();
            DisposeFramebufferAttachments();
            _frameBuffer.Dispose();

            _window.Dispose();
        }

        #endregion

        private void DisposeFramebufferAttachments()
        {
            if(_colorTexture != null)
            {
                _colorTexture.Dispose();
                _colorTexture = null;
            }

            if(_depthTexture != null)
            {
                _depthTexture.Dispose();
                _depthTexture = null;
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
        private readonly SceneState _sceneState;
        private readonly CameraLookAtPoint _camera;
        private readonly ClearState _clearState;
        private readonly RayCastedGlobe _globe;
        private readonly Texture2D _texture;

        private Framebuffer _frameBuffer;
        private Texture2D _colorTexture;
        private Texture2D _depthTexture;
        private DoubleViewportQuad _doubleViewportQuad;
    }
}