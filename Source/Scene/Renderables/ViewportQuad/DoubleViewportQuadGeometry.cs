using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using OpenGlobe.Core;
using OpenGlobe.Renderer;

namespace OpenGlobe.Scene
{
    internal sealed class DoubleViewportQuadGeometry : IDisposable
    {
        public DoubleViewportQuadGeometry()
        {
            _positionBuffer = Device.CreateVertexBuffer(BufferHint.StaticDraw, 12 * SizeInBytes<Vector2F>.Value);
            _textureCoordinatesBuffer = Device.CreateVertexBuffer(BufferHint.StaticDraw, 12 * SizeInBytes<Vector2H>.Value);
            _indexBuffer = Device.CreateIndexBuffer(BufferHint.StaticDraw, 12 * SizeInBytes<ushort>.Value);
        }

        internal void Update(Context context, ShaderProgram sp)
        {
            if(_va == null)
            {
                VertexBufferAttribute positionAttribute = new VertexBufferAttribute(
                    _positionBuffer, ComponentDatatype.Float, 2);
                VertexBufferAttribute textureCoordinatesAttribute = new VertexBufferAttribute(
                    _textureCoordinatesBuffer, ComponentDatatype.HalfFloat, 2);

                _va = context.CreateVertexArray();
                _va.Attributes[sp.VertexAttributes["position"].Location] = positionAttribute;
                _va.Attributes[sp.VertexAttributes["textureCoordinates"].Location] = textureCoordinatesAttribute;
                _va.IndexBuffer = _indexBuffer;
            }

            // 检测当前视口变换并重新计算视口矩形
            if (_viewport != context.Viewport)
            {
                //
                // Bottom and top swapped:  MS -> OpenGL
                //
                float left = context.Viewport.Left;
                float bottom = context.Viewport.Top;
                float right = context.Viewport.Right;
                float top = context.Viewport.Bottom;

                Vector2F[] positions = new Vector2F[]
                {
                    // left eye verts
                    new Vector2F(left, bottom),                 // -1, -1
                    new Vector2F((left+right)/2.0f, bottom),    // 0, -1
                    new Vector2F(left, top),                    // -1, 1
                    new Vector2F((left+right)/2.0f, top),       // 0, 1

                    // right eye verts
                    new Vector2F((left+right)/2.0f, bottom),    // 0, -1
                    new Vector2F(right, bottom),                // 1, -1
                    new Vector2F((left+right)/2.0f, top),       // 0, 1
                    new Vector2F(right, top)                    // 1, 1
                };
                _positionBuffer.CopyFromSystemMemory(positions);

                Vector2H[] textureCoordinates = new Vector2H[]
                {
                    // left eye tex coord
                    new Vector2H(0, 1),
                    new Vector2H(1, 1),
                    new Vector2H(0, 0),
                    new Vector2H(1, 0),

                    // right eye tex coord
                    new Vector2H(0, 1),
                    new Vector2H(1, 1),
                    new Vector2H(0, 0),
                    new Vector2H(1, 0),
                };
                _textureCoordinatesBuffer.CopyFromSystemMemory(textureCoordinates);

                ushort[] index = new ushort[] { 0, 1, 3, 0, 3, 2, 4, 5, 7, 4, 7, 6 };
                _indexBuffer.CopyFromSystemMemory(index);

                _viewport = context.Viewport;
            }
        }

        internal VertexArray VertexArray
        {
            get { return _va; }
        }

        #region IDisposable Members

        public void Dispose()
        {
            _positionBuffer.Dispose();
            _textureCoordinatesBuffer.Dispose();

            if(_va != null)
            {
                _va.Dispose();
            }
        }

        #endregion

        private Rectangle _viewport;
        private readonly VertexBuffer _positionBuffer;
        private readonly VertexBuffer _textureCoordinatesBuffer;
        private readonly IndexBuffer _indexBuffer;
        private VertexArray _va;
    }
}
