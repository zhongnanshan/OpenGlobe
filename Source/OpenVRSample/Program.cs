using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

namespace OpenVRSample
{
    class Program : GameWindow
    {
        #region "shader code members"

        string vertexShaderSource = @"
#version 410 core

precision highp float;

uniform mat4 projection_matrix;
uniform mat4 modelview_matrix;

layout(location = 0) in vec3 in_position;
layout(location = 1) in vec3 in_normal;

out vec3 normal;

void main(void)
{
  //works only for orthogonal modelview
  normal = (modelview_matrix * vec4(in_normal, 0)).xyz;
  
  gl_Position = projection_matrix * modelview_matrix * vec4(in_position, 1);
}";

        string fragmentShaderSource = @"
#version 410 core

precision highp float;

const vec3 ambient = vec3(0.1, 0.1, 0.1);
const vec3 lightVecNormalized = normalize(vec3(0.5, 0.5, 2.0));
const vec3 lightColor = vec3(0.9, 0.9, 0.7);

in vec3 normal;

out vec4 out_frag_color;

void main(void)
{
  float diffuse = clamp(dot(lightVecNormalized, normalize(normal)), 0.0, 1.0);
  out_frag_color = vec4(ambient + diffuse * lightColor, 1.0);
}";

        //int vertexShaderHandle;
        //int fragmentShaderHandle;
        int shaderProgramHandle;
        int modelviewMatrixLocation;
        int projectionMatrixLocation;

        string vertShaderSrc4Comp = @"
#version 410 core

layout(location = 0) in vec4 position;
layout(location = 1) in vec2 v2UVIn;

noperspective out vec2 v2UV;

void main()
{
	v2UV = v2UVIn;
	gl_Position = position;
}";

        string fragShaderSrc4Comp = @"
#version 410 core

uniform sampler2D mytexture;

noperspective in vec2 v2UV;

out vec4 outputColor;

void main()
{
		outputColor = texture(mytexture, v2UV);
}";

        int shdProgHnd4Comp;

        #endregion

        #region "members value"

        Matrix4 projectionMatrix, modelviewMatrix;

        struct FramebufferDesc
        {
            public int m_nDepthBufferId;
            public int m_nRenderTextureId;
            public int m_nRenderFramebufferId;
            public int m_nResolveTextureId;
            public int m_nResolveFramebufferId;
        };
        FramebufferDesc leftEyeDesc;
        FramebufferDesc rightEyeDesc;

        // 立体设备输出渲染尺寸
        int renderWidth;
        int renderHeight;

        int testTexture;

        #endregion

        #region "geomery members value"

        Vector3[] positionVboData = new Vector3[]{
            new Vector3(-1.0f, -1.0f,  1.0f),
            new Vector3( 1.0f, -1.0f,  1.0f),
            new Vector3( 1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f,  1.0f,  1.0f),
            new Vector3(-1.0f, -1.0f, -1.0f),
            new Vector3( 1.0f, -1.0f, -1.0f),
            new Vector3( 1.0f,  1.0f, -1.0f),
            new Vector3(-1.0f,  1.0f, -1.0f) };

        int[] indicesVboData = new int[]{
             // front face
                0, 1, 2, 2, 3, 0,
                // top face
                3, 2, 6, 6, 7, 3,
                // back face
                7, 6, 5, 5, 4, 7,
                // left face
                4, 0, 3, 3, 7, 4,
                // bottom face
                0, 1, 5, 5, 4, 0,
                // right face
                1, 5, 6, 6, 2, 1, };

        int vaoHandle;
        int positionVboHandle;
        int normalVboHandle;
        int eboHandle;

        Vector2[] posVboData4Comp = new Vector2[]{
            new Vector2(-1, -1),
            new Vector2(0, -1),
            new Vector2(-1, 1),
            new Vector2(0, 1),
            new Vector2(0, -1),
            new Vector2(1, -1),
            new Vector2(0, 1),
            new Vector2(1, 1)};

        Vector2[] texCoordVboData4Comp = new Vector2[]{
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(0, 0),
            new Vector2(1, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(0, 0),
            new Vector2(1, 0)};

        int[] indVboData4Comp = new int[]{
            0, 1, 3,
            0, 3, 2,
            4, 5, 7,
            4, 7, 6};

        int posVboHnd4Comp;
        int texCoordVboHnd4Comp;
        int vaoHnd4Comp;
        int eboHnd4Comp;

        #endregion

        public Program()
            : base(640, 320, new GraphicsMode(), "OpenGL 3 Example", 0, DisplayDevice.Default, 
                  3, 2, GraphicsContextFlags.ForwardCompatible | GraphicsContextFlags.Debug)
        {
            renderWidth = 2722;
            renderHeight = 3024;

            Keyboard.KeyDown += OpenTKKeyDown;
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

        private void CreateFramebuffer(int width, int height, ref FramebufferDesc eyeDesc)
        {
            GL.GenFramebuffers(1, out eyeDesc.m_nRenderFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, eyeDesc.m_nRenderFramebufferId);

            GL.GenRenderbuffers(1, out eyeDesc.m_nDepthBufferId);
            GL.BindRenderbuffer(RenderbufferTarget.Renderbuffer, eyeDesc.m_nDepthBufferId);
            GL.RenderbufferStorageMultisample(RenderbufferTarget.Renderbuffer, 4, RenderbufferStorage.DepthComponent24, width, height);
            GL.FramebufferRenderbuffer(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment, RenderbufferTarget.Renderbuffer, eyeDesc.m_nDepthBufferId);

            GL.GenTextures(1, out eyeDesc.m_nRenderTextureId);
            GL.BindTexture(TextureTarget.Texture2DMultisample, eyeDesc.m_nRenderTextureId);
            GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, 4, PixelInternalFormat.Rgba8, width, height, true);

            GL.GenFramebuffers(1, out eyeDesc.m_nResolveFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, eyeDesc.m_nResolveFramebufferId);

            GL.GenTextures(1, out eyeDesc.m_nResolveTextureId);
            GL.BindTexture(TextureTarget.Texture2D, eyeDesc.m_nResolveTextureId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, eyeDesc.m_nResolveTextureId, 0);

            FramebufferErrorCode errCode = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if(errCode != FramebufferErrorCode.FramebufferComplete)
            {
                throw new Exception("framebuffer consistency check is failure!");
            }

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        protected override void OnLoad(System.EventArgs e)
        {
            VSync = VSyncMode.On;

            CreateShaders();
            CreateVBOs();
            CreateVAOs();

            CreateVBOs4Comp();
            CreateVAOs4Comp();

            CreateFramebuffer(renderWidth, renderHeight, ref leftEyeDesc);
            CreateFramebuffer(renderWidth, renderHeight, ref rightEyeDesc);

            // Other state
            GL.Enable(EnableCap.DepthTest);
            GL.ClearColor(System.Drawing.Color.MidnightBlue);
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
            shaderProgramHandle = CompileShaders(vertexShaderSource, fragmentShaderSource);
            GL.UseProgram(shaderProgramHandle);

            // Set uniforms
            projectionMatrixLocation = GL.GetUniformLocation(shaderProgramHandle, "projection_matrix");
            modelviewMatrixLocation = GL.GetUniformLocation(shaderProgramHandle, "modelview_matrix");

            float aspectRatio = ClientSize.Width / (float)(ClientSize.Height);
            Matrix4.CreatePerspectiveFieldOfView((float)Math.PI / 4, aspectRatio, 1, 100, out projectionMatrix);
            modelviewMatrix = Matrix4.LookAt(new Vector3(0, 3, 5), new Vector3(0, 0, 0), new Vector3(0, 1, 0));

            GL.UniformMatrix4(projectionMatrixLocation, false, ref projectionMatrix);
            GL.UniformMatrix4(modelviewMatrixLocation, false, ref modelviewMatrix);

            GL.UseProgram(0);

            shdProgHnd4Comp = CompileShaders(vertShaderSrc4Comp, fragShaderSrc4Comp);
            GL.UseProgram(shdProgHnd4Comp);
            GL.UseProgram(0);
        }

        private void CreateVBOs()
        {
            GL.GenBuffers(1, out positionVboHandle);
            GL.BindBuffer(BufferTarget.ArrayBuffer, positionVboHandle);
            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer,
                new IntPtr(positionVboData.Length * Vector3.SizeInBytes),
                positionVboData, BufferUsageHint.StaticDraw);

            GL.GenBuffers(1, out normalVboHandle);
            GL.BindBuffer(BufferTarget.ArrayBuffer, normalVboHandle);
            GL.BufferData<Vector3>(BufferTarget.ArrayBuffer,
                new IntPtr(positionVboData.Length * Vector3.SizeInBytes),
                positionVboData, BufferUsageHint.StaticDraw);

            GL.GenBuffers(1, out eboHandle);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eboHandle);
            GL.BufferData(BufferTarget.ElementArrayBuffer,
                new IntPtr(sizeof(uint) * indicesVboData.Length),
                indicesVboData, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }

        private void CreateVBOs4Comp()
        {
            GL.GenBuffers(1, out posVboHnd4Comp);
            GL.BindBuffer(BufferTarget.ArrayBuffer, posVboHnd4Comp);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer,
                new IntPtr(posVboData4Comp.Length * Vector2.SizeInBytes),
                posVboData4Comp, BufferUsageHint.StaticDraw);

            GL.GenBuffers(1, out texCoordVboHnd4Comp);
            GL.BindBuffer(BufferTarget.ArrayBuffer, texCoordVboHnd4Comp);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer,
                new IntPtr(texCoordVboData4Comp.Length * Vector2.SizeInBytes),
                texCoordVboData4Comp, BufferUsageHint.StaticDraw);

            GL.GenBuffers(1, out eboHnd4Comp);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eboHnd4Comp);
            GL.BufferData(BufferTarget.ElementArrayBuffer,
                new IntPtr(indVboData4Comp.Length * sizeof(int)),
                indVboData4Comp, BufferUsageHint.StaticDraw);

            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
        }

        private void CreateVAOs()
        {
            // GL3 allows us to store the vertex layout in a "vertex array object" (VAO).
            // This means we do not have to re-issue VertexAttribPointer calls
            // every time we try to use a different vertex layout - these calls are
            // stored in the VAO so we simply need to bind the correct VAO.
            GL.GenVertexArrays(1, out vaoHandle);
            GL.BindVertexArray(vaoHandle);

            GL.EnableVertexAttribArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, positionVboHandle);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, true, Vector3.SizeInBytes, 0);

            GL.EnableVertexAttribArray(1);
            GL.BindBuffer(BufferTarget.ArrayBuffer, normalVboHandle);
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, true, Vector3.SizeInBytes, 0);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eboHandle);

            GL.BindVertexArray(0);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
        }

        private void CreateVAOs4Comp()
        {
            GL.GenVertexArrays(1, out vaoHnd4Comp);
            GL.BindVertexArray(vaoHnd4Comp);

            GL.EnableVertexAttribArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, posVboHnd4Comp);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, true, 0, 0);

            GL.EnableVertexAttribArray(1);
            GL.BindBuffer(BufferTarget.ArrayBuffer, texCoordVboHnd4Comp);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, true, 0, 0);

            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eboHnd4Comp);

            GL.BindVertexArray(0);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
        }

        private void RenderScene()
        {
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.DepthTest);

            GL.UseProgram(shaderProgramHandle);
            GL.UniformMatrix4(modelviewMatrixLocation, false, ref modelviewMatrix);
            GL.BindVertexArray(vaoHandle);

            GL.DrawElements(BeginMode.Triangles, indicesVboData.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        private void RenderStereoTargets()
        {
            GL.ClearColor(0f, 0f, 0f, 1f);

            // left eye
            GL.Enable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, leftEyeDesc.m_nRenderFramebufferId);
            GL.Viewport(0, 0, renderWidth, renderHeight);
            RenderScene();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.Disable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, leftEyeDesc.m_nRenderFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, leftEyeDesc.m_nResolveFramebufferId);

            GL.BlitFramebuffer(0, 0, renderWidth, renderHeight, 0, 0, renderWidth, renderHeight, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

            // right eye
            GL.Enable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, rightEyeDesc.m_nRenderFramebufferId);
            GL.Viewport(0, 0, renderWidth, renderHeight);
            RenderScene();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);

            GL.Disable(EnableCap.Multisample);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, rightEyeDesc.m_nRenderFramebufferId);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, rightEyeDesc.m_nResolveFramebufferId);

            GL.BlitFramebuffer(0, 0, renderWidth, renderHeight, 0, 0, renderWidth, renderHeight, ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);

            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, 0);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
        }

        private void CreateTestTexture()
        {

        }

        private void RenderCompanionWindow()
        {
            // 渲染伴侣窗口
            GL.Disable(EnableCap.DepthTest);
            GL.Viewport(0, 0, Width, Height);

            GL.BindVertexArray(vaoHnd4Comp);
            GL.UseProgram(shdProgHnd4Comp);

            // left eye
            GL.BindTexture(TextureTarget.Texture2D, leftEyeDesc.m_nResolveFramebufferId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

            GL.DrawElements(BeginMode.Triangles, indVboData4Comp.Length/2, DrawElementsType.UnsignedInt, IntPtr.Zero);

            // right eye
            GL.BindTexture(TextureTarget.Texture2D, rightEyeDesc.m_nResolveFramebufferId);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

            GL.DrawElements(BeginMode.Triangles, indVboData4Comp.Length/2, DrawElementsType.UnsignedInt, new IntPtr(indVboData4Comp.Length));

            GL.BindVertexArray(0);
            GL.UseProgram(0);
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            Matrix4 rotation = Matrix4.CreateRotationY((float)e.Time);
            Matrix4.Mult(ref rotation, ref modelviewMatrix, out modelviewMatrix);
            //GL.UniformMatrix4(modelviewMatrixLocation, false, ref modelviewMatrix);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            RenderStereoTargets();

            RenderCompanionWindow();

            SwapBuffers();
        }

        static void Main(string[] args)
        {
            using (Program prog = new Program())
            {
                prog.Run(30);
            }
        }
    }
}
