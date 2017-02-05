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
using System.Collections.Generic;

using OpenGlobe.Core;
using OpenGlobe.Renderer;
using OpenGlobe.Scene;

namespace OpenGlobe.Examples
{
    sealed class EllipsoidSurfaceNormals : IDisposable
    {
        public EllipsoidSurfaceNormals()
        {
            _globeShape = new Ellipsoid(1, 1, _semiMinorAxis);

            _window = Device.CreateWindow(800, 600, "Chapter 2:  Ellipsoid Surface Normals");
            _window.Resize += OnResize;
            _window.RenderFrame += OnRenderFrame;
            _window.Keyboard.KeyDown += OnKeyDown;
            _sceneState = new SceneState();
            _camera = new CameraLookAtPoint(_sceneState.Camera, _window, _globeShape);

            _instructions = new HeadsUpDisplay();
            _instructions.Texture = Device.CreateTexture2D(
                Device.CreateBitmapFromText("Up - Increase semi-minor axis\nDown - Decrease semi-minor axis", 
                    new Font("Arial", 24)),
                TextureFormat.RedGreenBlueAlpha8, false);
            _instructions.Color = Color.Black;

            _clearState = new ClearState();

            CreateScene();
            
            ///////////////////////////////////////////////////////////////////

            _sceneState.Camera.Eye = Vector3D.UnitY;
            _sceneState.Camera.ZoomToTarget(2 * _globeShape.MaximumRadius);
        }

        private void CreateScene()
        {
            DisposeScene();

            _ellipsoid = new TessellatedGlobe();
            _ellipsoid.Shape = _globeShape;
            _ellipsoid.NumberOfSlicePartitions = 64;
            _ellipsoid.NumberOfStackPartitions = 32;

            ///////////////////////////////////////////////////////////////////

            // 计算椭球网格
            Mesh mesh = GeographicGridEllipsoidTessellator.Compute(_globeShape,
                64, 32, GeographicGridEllipsoidVertexAttributes.Position);

            // 创建椭球网格线框显示对象
            _wireframe = new Wireframe(_window.Context, mesh);
            _wireframe.Width = 2;

            ///////////////////////////////////////////////////////////////////

            // 创建坐标轴显示对象
            _axes = new Axes();
            _axes.Length = 1.5;
            _axes.Width = 3;

            ///////////////////////////////////////////////////////////////////

            // 计算p点椭球表面坐标
            Vector3D p = _globeShape.ToVector3D(new Geodetic3D(0, Trig.ToRadians(45), 0));
            // 计算p点地理法线向量
            Vector3D deticNormal = _globeShape.GeodeticSurfaceNormal(p);
            // 计算p点地心法线向量
            Vector3D centricNormal = Ellipsoid.CentricSurfaceNormal(p);

            double normalLength = _globeShape.MaximumRadius;
            // 计算地理法线末端点三维坐标
            Vector3D pDetic = p + (normalLength * deticNormal);
            Vector3D pCentric = p + (normalLength * centricNormal);

            VertexAttributeFloatVector3 positionAttribute = new VertexAttributeFloatVector3("position", 4);
            // 创建地理法线段顶点
            positionAttribute.Values.Add(p.ToVector3F());
            positionAttribute.Values.Add(pDetic.ToVector3F());
            // 创建地心法线段顶点
            positionAttribute.Values.Add(p.ToVector3F());
            positionAttribute.Values.Add(pCentric.ToVector3F());

            VertexAttributeRGBA colorAttribute = new VertexAttributeRGBA("color", 4);
            // 创建地理法线段顶点颜色
            colorAttribute.AddColor(Color.DarkGreen);
            colorAttribute.AddColor(Color.DarkGreen);
            // 创建地心法线段顶点颜色
            colorAttribute.AddColor(Color.DarkCyan);
            colorAttribute.AddColor(Color.DarkCyan);

            // 创建地理/地心发现段图形显示元语
            Mesh polyline = new Mesh();
            polyline.PrimitiveType = PrimitiveType.Lines;
            polyline.Attributes.Add(positionAttribute);
            polyline.Attributes.Add(colorAttribute);

            _normals = new Polyline();
            _normals.Set(_window.Context, polyline);
            _normals.Width = 3;

            ///////////////////////////////////////////////////////////////////
            Font font = new Font("Arial", 24);
            IList<Bitmap> labelBitmaps = new List<Bitmap>(2);
            labelBitmaps.Add(Device.CreateBitmapFromText("Geodetic", font));
            labelBitmaps.Add(Device.CreateBitmapFromText("Geocentric", font));
            font.Dispose();

            // 创建纹理压缩对象？
            TextureAtlas atlas = new TextureAtlas(labelBitmaps);

            _labels = new BillboardCollection(_window.Context, 2);
            _labels.Texture = Device.CreateTexture2D(atlas.Bitmap, TextureFormat.RedGreenBlueAlpha8, false);
            _labels.Add(new Billboard()
            {
                Position = pDetic,
                // 取第一个纹理
                TextureCoordinates = atlas.TextureCoordinates[0],
                Color = Color.DarkGreen,
                HorizontalOrigin = HorizontalOrigin.Right,
                VerticalOrigin = VerticalOrigin.Bottom
            });
            _labels.Add(new Billboard()
            {
                Position = pCentric,
                // 取第二个纹理
                TextureCoordinates = atlas.TextureCoordinates[1],
                Color = Color.DarkCyan,
                HorizontalOrigin = HorizontalOrigin.Right,
                VerticalOrigin = VerticalOrigin.Bottom
            });

            atlas.Dispose();

            ///////////////////////////////////////////////////////////////////
            Vector3D east = Vector3D.UnitZ.Cross(deticNormal);
            Vector3D north = deticNormal.Cross(east);

            // 创建p点切平面
            _tangentPlane = new Plane(_window.Context);
            _tangentPlane.Origin = p;
            _tangentPlane.XAxis = east;
            _tangentPlane.YAxis = north;
            _tangentPlane.OutlineWidth = 3;
        }

        private void OnResize()
        {
            _window.Context.Viewport = new Rectangle(0, 0, _window.Width, _window.Height);
            _sceneState.Camera.AspectRatio = _window.Width / (double)_window.Height;
        }

        private void OnRenderFrame()
        {
            Context context = _window.Context;
            context.Clear(_clearState);

            _ellipsoid.Render(context, _sceneState);
            _wireframe.Render(context, _sceneState);
            _axes.Render(context, _sceneState);
            _normals.Render(context, _sceneState);
            _labels.Render(context, _sceneState);
            _tangentPlane.Render(context, _sceneState);
            _instructions.Render(context, _sceneState);
        }

        private void OnKeyDown(object sender, KeyboardKeyEventArgs e)
        {
            if ((e.Key == KeyboardKey.Up) || (e.Key == KeyboardKey.Down))
            {
                if (e.Key == KeyboardKey.Up)
                {
                    _semiMinorAxis = Math.Min(_semiMinorAxis + _semiMinorAxisDelta, 1.0);
                }
                else
                {
                    _semiMinorAxis = Math.Max(_semiMinorAxis - _semiMinorAxisDelta, 0.1);
                }
                _globeShape = new Ellipsoid(1, 1, _semiMinorAxis);

                CreateScene();
            }
        }

        private void DisposeScene()
        {
            if (_ellipsoid != null)
            {
                _ellipsoid.Dispose();
            }

            if (_wireframe != null)
            {
                _wireframe.Dispose();
            }

            if (_axes != null)
            {
                _axes.Dispose();
            }

            if (_labels != null)
            {
                _labels.Texture.Dispose();
                _labels.Dispose();
            }

            if (_normals != null)
            {
                _normals.Dispose();
            }

            if (_tangentPlane != null)
            {
                _tangentPlane.Dispose();
            }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _camera.Dispose();
            _instructions.Dispose();
            DisposeScene();
            _window.Dispose();
        }

        #endregion

        private void Run(double updateRate)
        {
            _window.Run(updateRate);
        }

        static void Main()
        {
            using (EllipsoidSurfaceNormals example = new EllipsoidSurfaceNormals())
            {
                example.Run(30.0);
            }
        }

        private readonly GraphicsWindow _window;
        private readonly SceneState _sceneState;
        private readonly CameraLookAtPoint _camera;
        private readonly HeadsUpDisplay _instructions;
        private readonly ClearState _clearState;

        private TessellatedGlobe _ellipsoid;
        private Wireframe _wireframe;
        private Axes _axes;
        private BillboardCollection _labels;
        private Polyline _normals;
        private Plane _tangentPlane;

        private Ellipsoid _globeShape;
        private double _semiMinorAxis = 0.7;
        private const double _semiMinorAxisDelta = 0.025;
    }
}