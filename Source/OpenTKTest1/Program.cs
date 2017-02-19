using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace OpenTKTest1
{
    class Program :GameWindow
    {
        #region "initial"

        struct FramebufferDesc
        {
            public int m_nDepthBufferId;
            public int m_nRenderTextureId;
            public int m_nRenderFramebufferId;
            public int m_nResolveTextureId;
            public int m_nResolveFramebufferId;
        };
        FramebufferDesc _leftEyeDesc;
        FramebufferDesc _rightEyeDesc;

        int _vaoHnd;
        int _shdHnd;
        int _pLoc;
        int _mvLoc;
        int _texLoc;
        int[] _inds;
        float _fov;
        int _testTexture;

        int _vaoObjHnd;
        int[] _indsObj;
        int _shdObjHnd;
        int _pObjLoc;
        int _mvObjLoc;

        // 立体设备输出渲染尺寸
        int _renderWidth;
        int _renderHeight;

        Matrix4 _pMtx;
        Matrix4 _mvMtx;
        Matrix4 _pObjMtx;
        Matrix4 _mvObjMtx;

        #endregion

        public Program() : base(640, 320, new GraphicsMode(), "OpenTKTest1", GameWindowFlags.Default, 
            DisplayDevice.Default, 3, 3, GraphicsContextFlags.ForwardCompatible | GraphicsContextFlags.Debug)
        {
            Keyboard.KeyDown += OpenTKKeyDown;

            _renderWidth = Width;
            _renderHeight = Height;
        }

        private void CreateWinVao()
        {
            Vector2[] vert = new Vector2[]
            {
                new Vector2(-1, -1),    // left dwon
                new Vector2(0, -1),     // middle down
                new Vector2(-1, 1),     // left up
                new Vector2(0, 1),      // middle up

                new Vector2(0, -1),     // middle down
                new Vector2(1, -1),     // right down
                new Vector2(0, 1),      // middle up
                new Vector2(1, 1),      // right up
            };

            Vector2[] texCoord = new Vector2[]
            {
                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),

                new Vector2(0, 0),
                new Vector2(1, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
            };

            // 通过索引复用顶点
            _inds = new int[] 
            {
                0, 1, 2, 
                1, 3, 2,
                4, 5, 6,
                5, 7, 6,
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

        private void CreateTestTexture()
        {
            Bitmap bitmap = new Bitmap("NE2_50M_SR_W_4096.jpg");
            BitmapData data = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

            GL.GenTextures(1, out _testTexture);
            GL.BindTexture(TextureTarget.Texture2D, _testTexture);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb8, bitmap.Width, bitmap.Height,
                0, OpenTK.Graphics.OpenGL.PixelFormat.Bgr, PixelType.UnsignedByte, data.Scan0);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);

            //float largest;
            //GL.GetFloat(All.MaxTextureMaxAnisotropyExt, out largest);

            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void CreateFramebuffer(int width, int height, ref FramebufferDesc whichEye)
        {
            GL.GenFramebuffers(1, out whichEye.m_nRenderFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, whichEye.m_nRenderFramebufferId);

            GL.GenRenderbuffers(1, out whichEye.m_nDepthBufferId);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, whichEye.m_nDepthBufferId);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, 4, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, whichEye.m_nDepthBufferId);

            GL.GenTextures(1, out whichEye.m_nRenderTextureId);
            GL.BindTexture(TextureTarget.Texture2DMultisample, whichEye.m_nRenderTextureId);
            GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, 4, PixelInternalFormat.Rgb8, width, height, true);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2DMultisample, whichEye.m_nRenderTextureId, 0);

            GL.GenFramebuffers(1, out whichEye.m_nResolveFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, whichEye.m_nResolveFramebufferId);

            GL.GenTextures(1, out whichEye.m_nResolveTextureId);
            GL.BindTexture(TextureTarget.Texture2D, whichEye.m_nResolveTextureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb8, width, height, 0, OpenTK.Graphics.OpenGL.PixelFormat.Rgb, PixelType.UnsignedByte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, whichEye.m_nResolveTextureId, 0);

            FramebufferErrorCode errCode = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (errCode != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("framebuffer consistency check is failure!");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
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

uniform sampler2D texture1;

in vec2 texcoord;

out vec4 out_frag_color;

void main()
{
    out_frag_color = texture(texture1, texcoord);
}";

            _shdHnd = CompileShaders(vertShdSrc, fragShdSrc);

            GL.UseProgram(_shdHnd);

            _pLoc = GL.GetUniformLocation(_shdHnd, "projection_matrix");
            _mvLoc = GL.GetUniformLocation(_shdHnd, "modelview_matrix");
            _texLoc = GL.GetUniformLocation(_shdHnd, "texture1");

            float aspectRatio = Width / (float)Height;
            // 注意近裁剪面值表示到照相机的距离，比这个距离小的因为太近而被剔除
            //Matrix4.CreatePerspectiveFieldOfView(_fov, aspectRatio, 1, 100, out _pMtx);
            Matrix4.CreateOrthographicOffCenter(-1, 1, -1, 1, -1, 1, out _pMtx);
            // 注意相机的位置正好在近裁剪面上（若在向原点移动一点将看不到图像）
            //_mvMtx = Matrix4.LookAt(new Vector3(0, 0, 0), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
            _mvMtx = Matrix4.Identity;

            GL.UniformMatrix4(_pLoc, false, ref _pMtx);
            GL.UniformMatrix4(_mvLoc, false, ref _mvMtx);

            GL.UseProgram(0);
        }

        private void CreateObjVao()
        {
            Vector3[] verts = new Vector3[]
            {
                // XY面前面
                new Vector3(-1, -1, 1), // 0 left down before
                new Vector3(1, -1, 1),  // 1 right down before
                new Vector3(-1, 1, 1),  // 2 left up before
                new Vector3(1, 1, 1),   // 3 right up before

                // XY面后面
                new Vector3(-1, -1, -1),// 4 left down after
                new Vector3(1, -1, -1), // 5 right down after
                new Vector3(-1, 1, -1), // 6 left up after
                new Vector3(1, 1, -1),  // 7 right up after
            };

            _indsObj = new int[]
            {
                0, 1, 2, 1, 3, 2,       // face before
                4, 6, 5, 5, 6, 7,       // face after
                0, 2, 4, 2, 6, 4,       // face left
                1, 5, 3, 5, 7, 3,       // face right
                2, 3, 6, 3, 7, 6,       // face up
                0, 4, 1, 1, 4, 5,       // face down
            };

            int vertBufHnd;
            int indBufHnd;

            GL.GenVertexArrays(1, out _vaoObjHnd);
            GL.BindVertexArray(_vaoObjHnd);

            GL.GenBuffers(1, out vertBufHnd);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertBufHnd);
            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer, verts.Length * Vector3.SizeInBytes, verts, BufferUsageHint.StaticDraw);

            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, true, 0, 0);

            GL.GenBuffers(1, out indBufHnd);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, indBufHnd);
            GL.BufferData<int>(BufferTarget.ElementArrayBuffer, _indsObj.Length * sizeof(int), _indsObj, BufferUsageHint.StaticDraw);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            GL.DisableVertexAttribArray(0);
        }

        private void CreateObjShaders()
        {
            string vertShdSrc = @"
#version 330

uniform mat4 projection_matrix;
uniform mat4 modelview_matrix;

layout(location = 0) in vec3 in_pos;

void main()
{
    gl_Position = projection_matrix * modelview_matrix * vec4(in_pos, 1);
}";

            string fragShdSrc = @"
#version 330

out vec4 out_frag_color;

void main()
{
    out_frag_color = vec4(1, 0, 0, 1);
}";

            _shdObjHnd = CompileShaders(vertShdSrc, fragShdSrc);

            GL.UseProgram(_shdObjHnd);

            _pObjLoc = GL.GetUniformLocation(_shdHnd, "projection_matrix");
            _mvObjLoc = GL.GetUniformLocation(_shdHnd, "modelview_matrix");

            float aspectRatio = _renderWidth / (float)_renderHeight;
            // 注意近裁剪面值表示到照相机的距离，比这个距离小的因为太近而被剔除
            Matrix4.CreatePerspectiveFieldOfView(_fov, aspectRatio, 1, 100, out _pObjMtx);
            // 注意相机的位置正好在近裁剪面上（若在向原点移动一点将看不到图像）
            _mvObjMtx = Matrix4.LookAt(new Vector3(0, 0, 3), new Vector3(0, 0, 0), new Vector3(0, 1, 0));

            GL.UniformMatrix4(_pObjLoc, false, ref _pObjMtx);
            GL.UniformMatrix4(_mvObjLoc, false, ref _mvObjMtx);

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

        private void CalcCurrentVPMatrix()
        {
            float aspectRatio = _renderWidth / (float)_renderHeight;
            // 注意近裁剪面值表示到照相机的距离，比这个距离小的因为太近而被剔除
            Matrix4.CreatePerspectiveFieldOfView(_fov, aspectRatio, 1, 100, out _pObjMtx);
            // 注意相机的位置正好在近裁剪面上（若在向原点移动一点将看不到图像）
            _mvObjMtx = Matrix4.LookAt(new Vector3(1.5f, 1.6f, 2.5f), new Vector3(0, 0, 0), new Vector3(0, 1, 0));
        }

        private void RenderObjScene()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Disable(EnableCap.DepthTest);

            GL.UseProgram(_shdObjHnd);
            GL.BindVertexArray(_vaoObjHnd);

            CalcCurrentVPMatrix();
            GL.UniformMatrix4(_pObjLoc, false, ref _pObjMtx);
            GL.UniformMatrix4(_mvObjLoc, false, ref _mvObjMtx);

            GL.DrawElements(PrimitiveType.Triangles, _indsObj.Length, DrawElementsType.UnsignedInt, 0);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        private void RenderStereoTargets()
        {
            GL.ClearColor(0, 0, 0, 1);

            // left eye
            GL.Enable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _leftEyeDesc.m_nRenderFramebufferId);
            GL.Viewport(0, 0, _renderWidth, _renderHeight);
            RenderObjScene();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.Disable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _leftEyeDesc.m_nRenderFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _leftEyeDesc.m_nResolveFramebufferId);

            GL.BlitFramebuffer(0, 0, _renderWidth, _renderHeight, 0, 0, _renderWidth, _renderHeight, 
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            // right eye
            GL.Enable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, _rightEyeDesc.m_nRenderFramebufferId);
            GL.Viewport(0, 0, _renderWidth, _renderHeight);
            RenderObjScene();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.Disable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, _rightEyeDesc.m_nRenderFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, _rightEyeDesc.m_nResolveFramebufferId);

            GL.BlitFramebuffer(0, 0, _renderWidth, _renderHeight, 0, 0, _renderWidth, _renderHeight, 
                ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        }

        private void RenderWinScene()
        {
            GL.Disable(EnableCap.DepthTest);
            GL.Viewport(0, 0, Width, Height);

            //GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_shdHnd);
            GL.BindVertexArray(_vaoHnd);

            GL.BindTexture(TextureTarget.Texture2D, _leftEyeDesc.m_nResolveTextureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.DrawElements(PrimitiveType.Triangles, _inds.Length / 2, DrawElementsType.UnsignedInt, 0);

            GL.BindTexture(TextureTarget.Texture2D, _rightEyeDesc.m_nResolveTextureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.DrawElements(PrimitiveType.Triangles, _inds.Length / 2, DrawElementsType.UnsignedInt, _inds.Length / 2 * sizeof(int));

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        private void RenderWinSceneTest()
        {
            GL.Disable(EnableCap.DepthTest);
            GL.Viewport(0, 0, Width, Height);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            GL.UseProgram(_shdHnd);
            GL.BindVertexArray(_vaoHnd);

            //GL.ActiveTexture(0);
            GL.BindTexture(TextureTarget.Texture2D, _testTexture);
            //GL.Uniform1(_texLoc, 0);
            GL.DrawElements(PrimitiveType.Triangles, _inds.Length / 2, DrawElementsType.UnsignedInt, 0);

            GL.BindTexture(TextureTarget.Texture2D, _testTexture);
            //GL.Uniform1(_texLoc, 0);
            GL.DrawElements(PrimitiveType.Triangles, _inds.Length / 2, DrawElementsType.UnsignedInt, _inds.Length / 2 * sizeof(int));

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        protected override void OnLoad(EventArgs e)
        {
            VSync = VSyncMode.On;

            _fov = (float)Math.PI * 2f / 3f;

            // 开启OpenGL调试
            DebugOpenGL();

            // 创建vaoHnd对象
            CreateWinVao();

            // 加载纹理
            CreateTestTexture();

            // 创建Shaders对象
            CreateShaders();

            // 创建主渲染场景
            CreateObjVao();

            // 创建主渲染场景shaders
            CreateObjShaders();

            // 创建帧缓冲对象
            CreateFramebuffer(_renderWidth, _renderHeight, ref _leftEyeDesc);
            CreateFramebuffer(_renderWidth, _renderHeight, ref _rightEyeDesc);

            // 开启深度测试（绘制前需要先清深度缓冲）
            //GL.Enable(EnableCap.DepthTest);
            // 开启背面剔除（相机移到(0,0,-1)将看不到图像）
            // 只有面法线与照相机视线夹角大于90度的面才可见
            //GL.Enable(EnableCap.CullFace);
            // 设备背景默认颜色
            GL.ClearColor(0, 0, 0, 0);
        }

        protected override void OnResize(EventArgs e)
        {
            float aspectRatio = Width / (float)Height;
            //Matrix4.CreatePerspectiveFieldOfView(_fov, aspectRatio, 1, 100, out _pMtx);
            Matrix4.CreateOrthographicOffCenter(-1, 1, -1, 1, -1, 1, out _pMtx);
            //GL.UniformMatrix4(_pLoc, false, ref _pMtx);

            //GL.Viewport(0, 0, Width, Height);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {

        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            // 渲染主场景
            RenderStereoTargets();
            //RenderObjScene();

            // 渲染伴随窗口
            RenderWinScene();
            //RenderWinSceneTest();

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
