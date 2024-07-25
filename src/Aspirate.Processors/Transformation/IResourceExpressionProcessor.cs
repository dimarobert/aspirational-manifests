namespace Aspirate.Processors.Transformation;

public interface IResourceExpressionProcessor
{
    Dictionary<string, bool> ProcessEvaluations(Dictionary<string, Resource> resources);
}
