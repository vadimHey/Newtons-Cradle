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
    vec3 albedo = texture(texture0, TexCoord).rgb;
    vec3 lightColor = vec3(1.5, 1.4, 1.3);

    vec3 ambient = 0.6 * albedo * lightColor;

    vec3 norm = normalize(Normal);
    vec3 lightDir = normalize(lightPos - FragPos);
    float diff = max(dot(norm, lightDir), 0.0);
    vec3 diffuse = diff * albedo * lightColor;

    vec3 viewDir = normalize(viewPos - FragPos);
    vec3 reflectDir = reflect(-lightDir, norm);
    float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
    vec3 specular = 0.6 * spec * lightColor;

    float distance = length(lightPos - FragPos);
    float attenuation = 1.0 / (1.0 + 0.22 * distance + 0.20 * distance * distance);

    ambient *= attenuation;
    diffuse *= attenuation;
    specular *= attenuation;

    float brightness = 1.5;
    vec3 result = (ambient + diffuse + specular) * brightness;
    FragColor = vec4(result * 1.3, 1.0);   
}