#version 330

in vec2 fsTextureCoordinates;

out vec4 fragmentColor;

uniform sampler2D og_texture0;

void main()
{
	fragmentColor = texture(og_texture0, fsTextureCoordinates);
}