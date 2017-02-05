#region License
//
// (C) Copyright 2010 Patrick Cozzi and Kevin Ring
//
// Distributed under the MIT License.
// See License.txt or http://www.opensource.org/licenses/mit-license.php.
//
#endregion

using System;
using System.Drawing;
using System.Collections.Generic;

using OpenGlobe.Core;
using OpenGlobe.Renderer;
using OpenGlobe.Scene;

namespace OpenGlobe.Examples
{
    sealed class Curves : IDisposable
    {
        public Curves()
        {
            _semiMinorAxis = Ellipsoid.ScaledWgs84.Radii.Z;
            // 根据椭球对象的尺寸计算表面设定点的三维坐标
            SetShape();

            // 创建主窗口
            _window = Device.CreateWindow(800, 600, "Chapter 2:  Curves");
            // 设置主窗口回调函数
            _window.Resize += OnResize;
            _window.RenderFrame += OnRenderFrame;
            _window.Keyboard.KeyDown += OnKeyDown;

            // 创建主场景OpenGL状态存储对象
            _sceneState = new SceneState();

            // 设置主场景照相机类型（固定观察一点的照相机）
            _camera = new CameraLookAtPoint(_sceneState.Camera, _window, _globeShape);

            // 创建状态（scissor/color/depth/stencil）清除对象
            _clearState = new ClearState();

            // 创建单色纹理对象（用于对椭球着色）
            // 因为RayCastedGlobe对象的颜色只能通过其Texture接口设置
            _texture = Device.CreateTexture2D(new Texture2DDescription(1, 1, TextureFormat.RedGreenBlue8));
            WritePixelBuffer pixelBuffer = Device.CreateWritePixelBuffer(PixelBufferHint.Stream, 3);
            pixelBuffer.CopyFromSystemMemory(new byte[] { 0, 255, 127 });
            _texture.CopyFromBuffer(pixelBuffer, ImageFormat.RedGreenBlue, ImageDatatype.UnsignedByte, 1);

            // 创建窗口表面的信息显示对象
            _instructions = new HeadsUpDisplay();
            // 设置信息显示前景色
            _instructions.Color = Color.Black;

            // 创建椭球表面采样点billboard显示集合对象
            _sampledPoints = new BillboardCollection(_window.Context);
            // 创建并设置billboard显示纹理为直径8个窗口像素的点
            _sampledPoints.Texture = Device.CreateTexture2D(Device.CreateBitmapFromPoint(8), TextureFormat.RedGreenBlueAlpha8, false);
            // 关闭采样点billboard集合显示对象的深度测试（使其在任何角度都可见）
            _sampledPoints.DepthTestEnabled = false;

            // 创建RayCastedGlobe椭球绘制对象并设置纹理
            _ellipsoid = new RayCastedGlobe(_window.Context);
            _ellipsoid.Texture = _texture;

            // 创建椭球表面曲线显示对象
            _polyline = new Polyline();
            // 设置线宽并关闭深度测试
            _polyline.Width = 3;
            _polyline.DepthTestEnabled = false;

            // 创建参考面显示对象
            _plane = new Plane(_window.Context);
            _plane.Origin = Vector3D.Zero;
            _plane.OutlineWidth = 3;

            // 创建主场景
            CreateScene();
            
            ///////////////////////////////////////////////////////////////////

            // 设置主照相机观察方向
            _sceneState.Camera.Eye = Vector3D.UnitY;
            // 根据视场重新调整相机位置
            _sceneState.Camera.ZoomToTarget(2 * _globeShape.MaximumRadius);
        }

        private void CreateScene()
        {
            // 设置窗口表面指令信息显示内容
            string text = "Granularity: " + _granularityInDegrees + " (left/right)\n";
            text += "Points: " + (_sampledPoints.Show ? "on" : "off") + " ('1')\n";
            text += "Polyline: " + (_polyline.Show ? "on" : "off") + " ('2')\n";
            text += "Plane: " + (_plane.Show ? "on" : "off") + " ('3')\n";
            text += "Semi-minor axis (up/down)\n";

            // 转换指令信息文字内容为图像纹理
            _instructions.Texture = Device.CreateTexture2D(
                Device.CreateBitmapFromText(text, new Font("Arial", 24)),
                TextureFormat.RedGreenBlueAlpha8, false);

            ///////////////////////////////////////////////////////////////////

            // 基于椭球形状计算表面采样点三维坐标
            IList<Vector3D> positions = _globeShape.ComputeCurve(
                _p, _q, Trig.ToRadians(_granularityInDegrees));

            // 设置椭球表面采样点billboard集合的位置和颜色
            _sampledPoints.Clear();
            _sampledPoints.Add(new Billboard() { Position = positions[0], Color = Color.Orange });
            _sampledPoints.Add(new Billboard() { Position = positions[positions.Count - 1], Color = Color.Orange });

            for (int i = 1; i < positions.Count - 1; ++i)
            {
                _sampledPoints.Add(new Billboard() 
                { 
                    Position = positions[i], 
                    Color = Color.Yellow 
                });
            }

            ///////////////////////////////////////////////////////////////////

            // 设置椭球的尺寸形状信息
            _ellipsoid.Shape = _globeShape;

            ///////////////////////////////////////////////////////////////////

            // 计算椭球表面曲线位置和颜色
            VertexAttributeFloatVector3 positionAttribute = new VertexAttributeFloatVector3("position", positions.Count);
            VertexAttributeRGBA colorAttribute = new VertexAttributeRGBA("color", positions.Count);

            for (int i = 0; i < positions.Count; ++i)
            {
                positionAttribute.Values.Add(positions[i].ToVector3F());
                colorAttribute.AddColor(Color.Red);
            }

            // 创建椭球表面曲线
            Mesh mesh = new Mesh();
            mesh.PrimitiveType = PrimitiveType.LineStrip;
            mesh.Attributes.Add(positionAttribute);
            mesh.Attributes.Add(colorAttribute);

            _polyline.Set(_window.Context, mesh);

            ///////////////////////////////////////////////////////////////////

            // 设置显示参考面缩放使其略大于椭球
            double scale = 1.25 * _globeShape.Radii.MaximumComponent;
            // 设置参考面X轴穿过p点
            _plane.XAxis = scale * _p.Normalize();
            _plane.YAxis = scale * _p.Cross(_q).Cross(_p).Normalize();
        }

        private void OnResize()
        {
            // 重新设置视口大小
            _window.Context.Viewport = new Rectangle(0, 0, _window.Width, _window.Height);
            // 重新计算相机视景体宽高比
            _sceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;
        }

        private void OnRenderFrame()
        {
            Context context = _window.Context;
            // 绘制前清除OpenGL状态
            context.Clear(_clearState);

            // 调用各对象的绘制函数
            _ellipsoid.Render(context, _sceneState);
            _polyline.Render(context, _sceneState);
            _sampledPoints.Render(context, _sceneState);
            _plane.Render(context, _sceneState);
            // 为了保持窗口信息始终保持在最上面须要最后绘制
            _instructions.Render(context, _sceneState);
        }

        private void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if ((e.Key == KeyboardKey.Left) || (e.Key == KeyboardKey.Right) ||
                (e.Key == KeyboardKey.Up) || (e.Key == KeyboardKey.Down))
            {
                if (e.Key == KeyboardKey.Left)
                {
                    _granularityInDegrees = Math.Max(_granularityInDegrees - 1.0, 1.0);
                }
                else if (e.Key == KeyboardKey.Right)
                {
                    _granularityInDegrees = Math.Min(_granularityInDegrees + 1.0, 30.0);
                }
                else if (e.Key == KeyboardKey.Up)
                {
                    _semiMinorAxis = Math.Min(_semiMinorAxis + _semiMinorAxisDelta, 2.0);
                }
                else if (e.Key == KeyboardKey.Down)
                {
                    _semiMinorAxis = Math.Max(_semiMinorAxis - _semiMinorAxisDelta, 0.1);
                }
                SetShape();
            }
            else if ((e.Key == KeyboardKey.Number1) ||
                     (e.Key == KeyboardKey.Keypad1))
            {
                _sampledPoints.Show = !_sampledPoints.Show;
            }
            else if ((e.Key == KeyboardKey.Number2) ||
                     (e.Key == KeyboardKey.Keypad2))
            {
                _polyline.Show = !_polyline.Show;
            }
            else if ((e.Key == KeyboardKey.Number3) || 
                     (e.Key == KeyboardKey.Keypad3))
            {
                _plane.Show = !_plane.Show;
            }

            // 根据指令状态变化重新计算主场景
            CreateScene();
        }

        private void SetShape()
        {
            _globeShape = new Ellipsoid(
                Ellipsoid.ScaledWgs84.Radii.X,
                Ellipsoid.ScaledWgs84.Radii.Y,
                _semiMinorAxis);
            _p = _globeShape.ToVector3D(new Geodetic2D(Trig.ToRadians(40), Trig.ToRadians(40)));
            _q = _globeShape.ToVector3D(new Geodetic2D(Trig.ToRadians(120), Trig.ToRadians(-30)));
        }

        #region IDisposable Members

        public void Dispose()
        {
            _texture.Dispose();
            _camera.Dispose();
            _instructions.Dispose();
            _ellipsoid.Dispose();
            _sampledPoints.Dispose();
            _sampledPoints.Texture.Dispose();
            _polyline.Dispose();
            _plane.Dispose();
            _window.Dispose();
        }

        #endregion

        private void Run(double updateRate)
        {
            _window.Run(updateRate);
        }

        static void Main()
        {
            using (Curves example = new Curves())
            {
                example.Run(30.0);
            }
        }

        private readonly GraphicsWindow _window;
        private readonly SceneState _sceneState;
        private readonly CameraLookAtPoint _camera;
        private readonly ClearState _clearState;

        private readonly Texture2D _texture;
        private readonly HeadsUpDisplay _instructions;
        private readonly RayCastedGlobe _ellipsoid;
        private readonly BillboardCollection _sampledPoints;
        private readonly Polyline _polyline;
        private readonly Plane _plane;

        private Ellipsoid _globeShape;
        private Vector3D _p;
        private Vector3D _q;

        private double _semiMinorAxis;
        private const double _semiMinorAxisDelta = 0.025;
        private double _granularityInDegrees = 5.0;
    }
}