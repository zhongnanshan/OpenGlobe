using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenGlobe.Core;
using OpenGlobe.Renderer;

namespace OpenGlobe.Scene
{
    public sealed class DoubleViewportQuad : IDisposable
    {
        public DoubleViewportQuad(Context context)
        {
            Verify.ThrowIfNull(context);

            RenderState renderState = new RenderState();
            renderState.FacetCulling.Enabled = false;
            renderState.DepthTest.Enabled = false;

            _drawState = new DrawState();
            _drawState.RenderState = renderState;
            _drawState.ShaderProgram = Device.CreateShaderProgram(
                EmbeddedResources.GetText("OpenGlobe.Scene.Renderables.ViewportQuad.Shaders.DoubleViewportQuadVS.glsl"),
                EmbeddedResources.GetText("OpenGlobe.Scene.Renderables.ViewportQuad.Shaders.DoubleViewportQuadFS.glsl"));

            _geometry = new DoubleViewportQuadGeometry();
        }

        public void Render(Context context, SceneState leftEyeSceneState, SceneState rightEyeSceneState)
        {
            Verify.ThrowIfNull(context);
            Verify.ThrowInvalidOperationIfNull(LeftEyeTexture, "LeftEyeTexture");
            Verify.ThrowInvalidOperationIfNull(RightEyeTexture, "RightEyeTexture");

            // 根据_drawState中的视口变化重新计算视口矩形
            _geometry.Update(context, _drawState.ShaderProgram);

            // 绘制左眼
            context.TextureUnits[0].Texture = LeftEyeTexture;
            context.TextureUnits[0].TextureSampler = Device.TextureSamplers.LinearClamp;
            _drawState.VertexArray = _geometry.VertexArray;

            // 画左眼矩形并绑定左眼纹理
            context.Draw(PrimitiveType.Triangles, 0, 2 * 3, _drawState, leftEyeSceneState);

            // 绘制右眼
            context.TextureUnits[0].Texture = RightEyeTexture;
            context.TextureUnits[0].TextureSampler = Device.TextureSamplers.LinearClamp;
            //_drawState.VertexArray = _geometry.VertexArray;

            // 画右眼矩形并绑定右眼纹理
            context.Draw(PrimitiveType.Triangles, 6, 2 * 3, _drawState, rightEyeSceneState);
        }

        public Texture2D LeftEyeTexture { get; set; }
        public Texture2D RightEyeTexture { get; set; }

        #region IDisposable Members

        public void Dispose()
        {
            _drawState.ShaderProgram.Dispose();
            _geometry.Dispose();
        }

        #endregion

        private readonly DrawState _drawState;
        private readonly DoubleViewportQuadGeometry _geometry;
    }
}
