#version 330

layout(location = og_positionVertexLocation) in vec4 position;
layout(location = og_textureCoordinateVertexLocation) in vec2 textureCoordinates;

out vec2 fsTextureCoordinates;

uniform mat4 og_viewportOrthographicMatrix;

void main()
{
	gl_Position = og_viewportOrthographicMatrix * position;
	fsTextureCoordinates = textureCoordinates;
}