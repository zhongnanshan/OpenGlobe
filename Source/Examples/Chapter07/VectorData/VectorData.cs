﻿#region License
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
using System.IO;

namespace OpenGlobe.Examples
{
    sealed class VectorData : IDisposable
    {
        public VectorData()
        {
            Ellipsoid globeShape = Ellipsoid.ScaledWgs84;

            _window = Device.CreateWindow(800, 600, "Chapter 7:  Vector Data");
            _window.Resize += OnResize;
            _window.RenderFrame += OnRenderFrame;
            _window.Keyboard.KeyDown += OnKeyDown;
            _window.Keyboard.KeyUp += OnKeyUp; 
            _sceneState = new SceneState();
            _camera = new CameraLookAtPoint(_sceneState.Camera, _window, globeShape);

            Context context = _window.Context;

            // 创建帧缓冲对象
            _framebuffer = context.CreateFramebuffer();

            _clearBlack = new ClearState();
            _clearBlack.Color = Color.Black;

            _clearWhite = new ClearState();
            _clearWhite.Color = Color.White;

            // 创建视口矩形
            _quad = new DayNightViewportQuad(context);

            _globe = new DayNightGlobe(context);
            _globe.Shape = globeShape;
            _globe.UseAverageDepth = true;
            _globe.DayTexture = Device.CreateTexture2D(new Bitmap("NE2_50M_SR_W_4096.jpg"), TextureFormat.RedGreenBlue8, false);
            _globe.NightTexture = Device.CreateTexture2D(new Bitmap("land_ocean_ice_lights_2048.jpg"), TextureFormat.RedGreenBlue8, false);

            _countries = new ShapefileRenderer("110m_admin_0_countries.shp", context, globeShape,
                new ShapefileAppearance()
                {
                    PolylineWidth = 1.0,
                    PolylineOutlineWidth = 1.0
                });
            _states = new ShapefileRenderer("110m_admin_1_states_provinces_lines_shp.shp", context, globeShape,
                new ShapefileAppearance()
                {
                    PolylineWidth = 1.0,
                    PolylineOutlineWidth = 1.0
                });
            _rivers = new ShapefileRenderer("50m-rivers-lake-centerlines.shp", context, globeShape,
                new ShapefileAppearance()
                {
                    PolylineColor = Color.LightBlue,
                    PolylineOutlineColor = Color.LightBlue,
                    PolylineWidth = 1.0,
                    PolylineOutlineWidth = 0.0
                });
            
            _populatedPlaces = new ShapefileRenderer("110m_populated_places_simple.shp", context, globeShape,
                new ShapefileAppearance() { Bitmap = new Bitmap("032.png") });
            _airports = new ShapefileRenderer("airprtx020.shp", context, globeShape, 
                new ShapefileAppearance() { Bitmap = new Bitmap("car-red.png") });
            _amtrakStations = new ShapefileRenderer("amtrakx020.shp", context, globeShape, 
                new ShapefileAppearance() { Bitmap = new Bitmap("paper-plane--arrow.png") });

            _hudFont = new Font("Arial", 16);
            _hud = new HeadsUpDisplay();
            _hud.Color = Color.Blue;

            _showVectorData = true;

            _sceneState.DiffuseIntensity = 0.5f;
            _sceneState.SpecularIntensity = 0.1f;
            _sceneState.AmbientIntensity = 0.4f;
            _sceneState.Camera.ZoomToTarget(globeShape.MaximumRadius);

            UpdateHUD();
        }

        private static string DayNightOutputToString(DayNightOutput dayNightOutput)
        {
            switch (dayNightOutput)
            {
                case DayNightOutput.Composite:
                    return "Composited Buffers";
                case DayNightOutput.DayBuffer:
                    return "Day Buffer";
                case DayNightOutput.NightBuffer:
                    return "Night Buffer";
                case DayNightOutput.BlendBuffer:
                    return "Blend Buffer";
            }

            return string.Empty;
        }

        private void UpdateHUD()
        {
            string text = "Output: " + DayNightOutputToString(_quad.DayNightOutput) + " ('o' + left/right)\n";
            text += "Vector Data: " + (_showVectorData ? "on" : "off") + " ('v')\n";
            text += "Wireframe: " + (_wireframe ? "on" : "off") + " ('w')\n";

            if (_hud.Texture != null)
            {
                _hud.Texture.Dispose();
                _hud.Texture = null;
            }
            _hud.Texture = Device.CreateTexture2D(
                Device.CreateBitmapFromText(text, _hudFont),
                TextureFormat.RedGreenBlueAlpha8, false);
        }

        private void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if (e.Key == KeyboardKey.O)
            {
                _oKeyDown = true;
            }
            else if (_oKeyDown && ((e.Key == KeyboardKey.Left) || (e.Key == KeyboardKey.Right)))
            {
                // 设置DayNightOutput Uniform变量值控制fragment shader输出
                _quad.DayNightOutput += (e.Key == KeyboardKey.Right) ? 1 : -1;

                if (_quad.DayNightOutput < DayNightOutput.Composite)
                {
                    _quad.DayNightOutput = DayNightOutput.BlendBuffer;
                }
                else if (_quad.DayNightOutput > DayNightOutput.BlendBuffer)
                {
                    _quad.DayNightOutput = DayNightOutput.Composite;
                }
            }
            else if (e.Key == KeyboardKey.V)
            {
                _showVectorData = !_showVectorData;
            }
            else if (e.Key == KeyboardKey.W)
            {
                _wireframe = !_wireframe;

                _countries.Wireframe = _wireframe;
                _states.Wireframe = _wireframe;
                _rivers.Wireframe = _wireframe;
                _populatedPlaces.Wireframe = _wireframe;
                _airports.Wireframe = _wireframe;
                _amtrakStations.Wireframe = _wireframe;
            }

            UpdateHUD();
        }

        private void OnKeyUp(object sender, KeyboardKeyEventArgs e)
        {
            if (e.Key == KeyboardKey.O)
            {
                _oKeyDown = false;
            }
        }

        private void OnResize()
        {
            _window.Context.Viewport = new Rectangle(0, 0, _window.Width, _window.Height);
            _sceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;

            UpdateFramebufferAttachments();
        }

        private void UpdateFramebufferAttachments()
        {
            DisposeFramebufferAttachments();
            _dayTexture = Device.CreateTexture2D(new Texture2DDescription(_window.Width, _window.Height, TextureFormat.RedGreenBlueAlpha8, false));
            _nightTexture = Device.CreateTexture2D(new Texture2DDescription(_window.Width, _window.Height, TextureFormat.RedGreenBlueAlpha8, false));
            // 混合因子纹理只需要一个32位浮点数格式存储
            _blendTexture = Device.CreateTexture2D(new Texture2DDescription(_window.Width, _window.Height, TextureFormat.Red32f, false));
            // 深度纹理采用24位定点数格式存储
            _depthTexture = Device.CreateTexture2D(new Texture2DDescription(_window.Width, _window.Height, TextureFormat.Depth24, false));
            
            // 绑定各纹理与视口矩形（与相应frag shader对应）
            _quad.DayTexture = _dayTexture;
            _quad.NightTexture = _nightTexture;
            _quad.BlendTexture = _blendTexture;
        }

        private void OnRenderFrame()
        {
            Context context = _window.Context;

            /////////////////////////////////////
            //_window.Context.Clear(_clearWhite);
            //_globe.Render(context, _sceneState);

            //_countries.Render(context, _sceneState);
            //_rivers.Render(context, _sceneState);
            //_states.Render(context, _sceneState);
            //_populatedPlaces.Render(context, _sceneState);
            //_airports.Render(context, _sceneState);
            //_amtrakStations.Render(context, _sceneState); 
            //return;
            /////////////////////////////////////

            //
            // Render to frame buffer
            // 设置OpenGL上下文帧缓冲为用户创建帧缓冲
            //
            context.Framebuffer = _framebuffer;

            // 清除椭球外边缘矢量图形残留像素
            // 关联纹理与帧缓冲绑定点
            // 这里的_dayTexture/_nightTexture将由frag shader输出片元填充
            SetFramebufferAttachments(_dayTexture, _nightTexture, null);
            // 用黑色清除颜色缓冲（黑色是（0，0，0））
            _window.Context.Clear(_clearBlack);
            // 没有这一步则矢量图形的像素会残留在椭球外边缘

            // 绑定混合因子纹理与帧缓冲对象
            SetFramebufferAttachments(null, null, _blendTexture);
            // 用白色清除颜色缓冲（白色是（255，255，255））
            _window.Context.Clear(_clearWhite);
            // 没有这一步则窗口中一片黑

            //
            // Render globe to day, night, and blend buffers
            // 绑定各纹理与帧缓冲各绑定点
            //
            SetFramebufferAttachments(_dayTexture, _nightTexture, _blendTexture);
            // 把椭球渲染到帧缓冲中
            _globe.Render(context, _sceneState);

            if (_showVectorData)
            {
                // 绑定白天纹理
                SetFramebufferAttachments(_dayTexture, null, null);

                //
                // Render vector data, layered bottom to top, to the day buffer only
                // 在白天纹理上渲染矢量图形（渲染顺序自底向上，底部的会被覆盖）
                //
                _countries.Render(context, _sceneState);
                _rivers.Render(context, _sceneState);
                _states.Render(context, _sceneState);
                _populatedPlaces.Render(context, _sceneState);
                _airports.Render(context, _sceneState);
                _amtrakStations.Render(context, _sceneState);
            }

            //
            // Render viewport quad to composite buffers
            // 复位帧缓冲为默认窗口缓冲并渲染视口矩形（包括前面帧缓冲生成的纹理）
            //
            context.Framebuffer = null;
            _quad.Render(context, _sceneState);
            _hud.Render(context, _sceneState);
        }

        private void SetFramebufferAttachments(Texture2D day, Texture2D night, Texture2D blend)
        {
            _framebuffer.ColorAttachments[_globe.FragmentOutputs("dayColor")] = day;
            _framebuffer.ColorAttachments[_globe.FragmentOutputs("nightColor")] = night;
            _framebuffer.ColorAttachments[_globe.FragmentOutputs("blendAlpha")] = blend;
            _framebuffer.DepthAttachment = _depthTexture;
        }

        #region IDisposable Members

        public void Dispose()
        {
            _camera.Dispose();
            DisposeFramebufferAttachments();
            _framebuffer.Dispose();
            _quad.Dispose();
            _globe.DayTexture.Dispose();
            _globe.NightTexture.Dispose();
            _globe.Dispose();
            _countries.Dispose();
            _states.Dispose();
            _rivers.Dispose();
            _populatedPlaces.Dispose();
            _airports.Dispose();
            _amtrakStations.Dispose();
            _hudFont.Dispose();
            _hud.Texture.Dispose();
            _hud.Dispose();
            _window.Dispose();
        }

        #endregion

        private void DisposeFramebufferAttachments()
        {
            if (_dayTexture != null)
            {
                _dayTexture.Dispose();
                _dayTexture = null;
            }

            if (_nightTexture != null)
            {
                _nightTexture.Dispose();
                _nightTexture = null;
            }

            if (_blendTexture != null)
            {
                _blendTexture.Dispose();
                _blendTexture = null;
            }

            if (_depthTexture != null)
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
            using (VectorData example = new VectorData())
            {
                example.Run(30.0);
            }
        }

        private readonly GraphicsWindow _window;
        private readonly SceneState _sceneState;
        private readonly CameraLookAtPoint _camera;
        private readonly ClearState _clearBlack;
        private readonly ClearState _clearWhite;

        private Texture2D _dayTexture;
        private Texture2D _nightTexture;
        private Texture2D _blendTexture;
        private Texture2D _depthTexture;
        private readonly Framebuffer _framebuffer;
        private readonly DayNightViewportQuad _quad;

        private readonly DayNightGlobe _globe;
        private readonly ShapefileRenderer _countries;
        private readonly ShapefileRenderer _states;
        private readonly ShapefileRenderer _rivers;
        private readonly ShapefileRenderer _populatedPlaces;
        private readonly ShapefileRenderer _airports;
        private readonly ShapefileRenderer _amtrakStations;
        
        private readonly Font _hudFont;
        private readonly HeadsUpDisplay _hud;

        private bool _showVectorData;
        private bool _wireframe;

        private bool _oKeyDown;
    }
}