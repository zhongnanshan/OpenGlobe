using System;
using System.Diagnostics;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace OpenTKTest1
{
    class Program :GameWindow
    {
        #region "initial"

        int _vaoHnd;
        int _shdHnd;
        int _pLoc;
        int _mvLoc;
        int[] _inds;
        float _fov;

        Matrix4 _pMtx;
        Matrix4 _mvMtx;

        #endregion

        public Program() : base(640, 320, new GraphicsMode(), "OpenTKTest1", GameWindowFlags.Default, 
            DisplayDevice.Default, 3, 3, GraphicsContextFlags.ForwardCompatible | GraphicsContextFlags.Debug)
        {
            Keyboard.KeyDown += OpenTKKeyDown;
        }

        private void CreateWinVao()
        {
            Vector2[] vert = new Vector2[]
            {
                new Vector2(-1, -1),    // left dwon
                new Vector2(1, -1),     // right down
                new Vector2(-1, 1),     // left up
                new Vector2(1, 1),      // right up
            };

            Vector2[] texCoord = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
            };

            // 通过索引复用顶点
            _inds = new int[] 
            {
                0, 1, 2,
                1, 3, 2
            };

            int vertBufHnd;
            int texCoordBufHnd;
            int indBufHnd;

            // create VAO
            GL.GenVertexArrays(1, out _vaoHnd);
            // 绑定VAO，以下操作将被记入VAO对象中，便于后续“回放”
            GL.BindVertexArray(_vaoHnd);

            // create vertex buffer
            GL.GenBuffers(1, out vertBufHnd);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertBufHnd);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, vert.Length * Vector2.SizeInBytes, vert, BufferUsageHint.StaticDraw);

            // 需要在前面绑定关系未破坏前指定顶点属性
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, true, 0, 0);

            // create tex coordinate buffer
            GL.GenBuffers(1, out texCoordBufHnd);
            GL.BindBuffer(BufferTarget.ArrayBuffer, texCoordBufHnd);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, texCoord.Length * Vector2.SizeInBytes, texCoord, BufferUsageHint.StaticDraw);

            // 需要在前面绑定关系未破坏前指定顶点属性
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, true, 0, 0);

            GL.GenBuffers(1, out indBufHnd);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, indBufHnd);
            GL.BufferData<int>(BufferTarget.ElementArrayBuffer, _inds.Length * sizeof(int), _inds, BufferUsageHint.StaticDraw);

            // 解除VAO绑定，之后的操作不会影响VAO状态
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
        }

        private int CompileShaders(string vertShaderSrc, string fragShaderSrc)
        {
            int vertexShaderHandle = GL.CreateShader(ShaderType.VertexShader);
            int fragmentShaderHandle = GL.CreateShader(ShaderType.FragmentShader);

            GL.ShaderSource(vertexShaderHandle, vertShaderSrc);
            GL.ShaderSource(fragmentShaderHandle, fragShaderSrc);

            GL.CompileShader(vertexShaderHandle);
            GL.CompileShader(fragmentShaderHandle);

            Debug.WriteLine(GL.GetShaderInfoLog(vertexShaderHandle));
            Debug.WriteLine(GL.GetShaderInfoLog(fragmentShaderHandle));

            // Create program
            int shaderProgramHnd = GL.CreateProgram();

            GL.AttachShader(shaderProgramHnd, vertexShaderHandle);
            GL.AttachShader(shaderProgramHnd, fragmentShaderHandle);

            //GL.BindAttribLocation(shaderProgramHandle, 0, "in_position");
            //GL.BindAttribLocation(shaderProgramHandle, 1, "in_normal");

            GL.LinkProgram(shaderProgramHnd);
            Debug.WriteLine(GL.GetProgramInfoLog(shaderProgramHnd));

            return shaderProgramHnd;
        }

        private void CreateShaders()
        {
            string vertShdSrc = @"
#version 330

uniform mat4 projection_matrix;
uniform mat4 modelview_matrix;

layout(location = 0) in vec2 in_pos;
layout(location = 1) in vec2 in_texcoord;

out vec2 texcoord;

void main()
{
    gl_Position = projection_matrix * modelview_matrix * vec4(in_pos, 0, 1);
    texcoord = in_texcoord;
}";

            string fragShdSrc = @"
#version 330

in vec2 texcoord;

out vec4 out_frag_color;

void main()
{
    out_frag_color = vec4(1, 0, 0, 1);
}";

            _shdHnd = CompileShaders(vertShdSrc, fragShdSrc);

            GL.UseProgram(_shdHnd);

            _pLoc = GL.GetUniformLocation(_shdHnd, "projection_matrix");
            _mvLoc = GL.GetUniformLocation(_shdHnd, "modelview_matrix");

            float aspectRatio = Width / (float)Height;
            // 注意近裁剪面值表示到照相机的距离，比这个距离小的因为太近而被剔除
            Matrix4.CreatePerspectiveFieldOfView(_fov, aspectRatio, 1, 100, out _pMtx);
            // 注意相机的位置正好在近裁剪面上（若在向原点移动一点将看不到图像）
            _mvMtx = Matrix4.LookAt(new Vector3(0, 0, 1), new Vector3(0, 0, 0), new Vector3(0, 1, 0));

            GL.UniformMatrix4(_pLoc, false, ref _pMtx);
            GL.UniformMatrix4(_mvLoc, false, ref _mvMtx);

            GL.UseProgram(0);
        }

        private void OpenTKKeyDown(object sender, OpenTK.Input.KeyboardKeyEventArgs e)
        {
            switch (e.Key)
            {
                case OpenTK.Input.Key.Escape:
                    Exit();
                    break;
                default:
                    break;
            }
        }

        private void DebugCallback(DebugSource src, DebugType typ, int id, DebugSeverity svr, int len, IntPtr msg, IntPtr usrprm)
        {
            Console.WriteLine(msg);
        }

        private void DebugOpenGL()
        {
            GL.DebugMessageCallback(DebugCallback, IntPtr.Zero);
            int ids = 0;
            GL.DebugMessageControl(DebugSourceControl.DontCare, DebugTypeControl.DontCare, DebugSeverityControl.DontCare, 0, ref ids, true);
            GL.Enable(EnableCap.DebugOutputSynchronous);
        }

        protected override void OnLoad(EventArgs e)
        {
            VSync = VSyncMode.On;

            _fov = (float)Math.PI * 2f / 3f;

            // 开启OpenGL调试
            DebugOpenGL();

            // 创建vaoHnd对象
            CreateWinVao();

            // 创建Shaders对象
            CreateShaders();

            // 开启深度测试（绘制前需要先清深度缓冲）
            GL.Enable(EnableCap.DepthTest);
            // 开启背面剔除（相机移到(0,0,-1)将看不到图像）
            // 只有面法线与照相机视线夹角大于90度的面才可见
            GL.Enable(EnableCap.CullFace);
            // 设备背景默认颜色
            GL.ClearColor(0, 0, 0, 0);
        }

        protected override void OnResize(EventArgs e)
        {
            float aspectRatio = Width / (float)Height;
            Matrix4.CreatePerspectiveFieldOfView(_fov, aspectRatio, 1, 100, out _pMtx);
            GL.UniformMatrix4(_pLoc, false, ref _pMtx);

            GL.Viewport(0, 0, Width, Height);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {

        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            //GL.Viewport(0, 0, Width, Height);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.UseProgram(_shdHnd);
            GL.BindVertexArray(_vaoHnd);
            //GL.BindBuffer(BufferTarget.ElementArrayBuffer, vaoHnd);
            GL.DrawElements(PrimitiveType.Triangles, _inds.Length, DrawElementsType.UnsignedInt, 0);

            SwapBuffers();
        }

        [STAThread]
        static void Main(string[] args)
        {
            using (Program test1Win = new Program())
            {
                test1Win.Run(30);
            }
        }
    }
}
