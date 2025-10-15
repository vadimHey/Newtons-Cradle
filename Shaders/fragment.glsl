#version 330 core
in vec3 FragPos;
in vec3 Normal;
in vec2 TexCoord;

out vec4 FragColor;

uniform sampler2D texture0;
uniform vec3 lightPos;
uniform vec3 viewPos;

void main()
{
    vec3 color = vec3(texture(texture0, TexCoord));
    vec3 ambient = 0.2 * color;
    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * color;
    vec3 viewDir = normalize(viewPos - FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
    vec3 specular = spec * vec3(1.0);
    vec3 result = ambient + diffuse + specular;
    FragColor = vec4(result, 1.0);
}
