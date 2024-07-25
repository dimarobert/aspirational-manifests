namespace Aspirate.Processors.Transformation;

public sealed class ResourceExpressionProcessor(IJsonExpressionProcessor jsonExpressionProcessor) : IResourceExpressionProcessor
{
    public static IResourceExpressionProcessor CreateDefaultExpressionProcessor() =>
        new ResourceExpressionProcessor(JsonExpressionProcessor.CreateDefaultExpressionProcessor());

    public Dictionary<string, bool> ProcessEvaluations(Dictionary<string, Resource> resources)
    {
        resources.EnsureBindingsHavePorts();

        var jsonDocument = resources.Where(r => r.Value is not UnsupportedResource)
            .ToDictionary(p => p.Key, p => p.Value)
            .TryParseAsJsonNode();

        var rootNode = jsonDocument.Root;

        jsonExpressionProcessor.ResolveJsonExpressions(rootNode, rootNode);

        return HandleSubstitutions(resources, rootNode);
    }

    private static Dictionary<string, bool> HandleSubstitutions(Dictionary<string, Resource> resources, JsonNode rootNode)
    {
        var secretsMap = new Dictionary<string, bool>();
        var replacementMap = rootNode["$replacementMap"].Deserialize<Dictionary<string, string[][]>>();
        foreach (var (key, value) in resources)
        {
            switch (value)
            {
                case IResourceWithConnectionString resourceWithConnectionString when !string.IsNullOrEmpty(resourceWithConnectionString.ConnectionString):
                    resourceWithConnectionString.ConnectionString = rootNode[key]![Literals.ConnectionString]!.ToString();
                    if (replacementMap.TryGetValue($"{key}.{Literals.ConnectionString}", out var csValueReplacementsList))
                    {
                        secretsMap[$"{key}.{Literals.ConnectionString}"] = IsSecret(resources, replacementMap, csValueReplacementsList);
                    }

                    break;
                case ValueResource valueResource:
                    {
                        foreach (var resourceValue in valueResource.Values.ToList())
                        {
                            valueResource.Values[resourceValue.Key] = rootNode[key]![resourceValue.Key]!.ToString();
                            if (replacementMap.TryGetValue($"{key}.{resourceValue.Key}", out var vrValueReplacementsList))
                            {
                                secretsMap[$"{key}.{resourceValue.Key}"] = IsSecret(resources, replacementMap, vrValueReplacementsList);
                            }
                        }

                        break;
                    }
            }

            if (value is IResourceWithEnvironmentalVariables resourceWithEnvVars && resourceWithEnvVars.Env is not null)
            {
                foreach (var envVar in resourceWithEnvVars.Env)
                {
                    resourceWithEnvVars.Env[envVar.Key] = rootNode[key]![Literals.Env]![envVar.Key]!.ToString();
                    if (replacementMap.TryGetValue($"{key}.{Literals.Env}.{envVar.Key}", out var vrValueReplacementsList))
                    {
                        secretsMap[$"{key}.{Literals.Env}.{envVar.Key}"] = IsSecret(resources, replacementMap, vrValueReplacementsList);
                    }
                }
            }
        }
        return secretsMap;
    }

    private static bool IsSecret(Dictionary<string, Resource> resources, Dictionary<string, string[][]> replacementMap, string[][] valueReplacementsList)
    {
        foreach (var replacementPathParts in valueReplacementsList)
        {
            var resourceName = replacementPathParts[0];
            if (resources.TryGetValue(resourceName, out var resource))
            {
                var isSecret = resource switch
                {
                    ParameterResource parameterResource => parameterResource.Inputs.Any(i => i.Value.Secret),
                    // for any other resource type search recursively to see if any of the values composing the current value is secret.
                    _ => replacementMap.TryGetValue(string.Join(".", replacementPathParts), out var subValueReplacementsList) && IsSecret(resources, replacementMap, subValueReplacementsList)
                };
                if (isSecret)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
