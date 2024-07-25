using Aspirate.Shared.Enums;

namespace Aspirate.Tests.ActionsTests.Manifests;

public class GenerateHelmChartManifestsActionTests : BaseActionTests<GenerateHelmChartAction>
{
    [Fact]
    public async Task test()
    {
        // Arrange
        var console = new TestConsole();
        console.Profile.Capabilities.Interactive = true;
        var state = CreateAspirateState(projectPath: DefaultProjectPath, outputFormat: OutputFormat.Helm.Value);
        var transformer = ResourceExpressionProcessor.CreateDefaultExpressionProcessor();
        var serviceProvider = CreateServiceProvider(state, console, new FileSystem());

        var resource = new ContainerResource
        {
            Type = AspireComponentLiterals.Container,
            Name = "rabbitmqcontainer",
            ConnectionString = "amqp://guest:{rabbitmqcontainer-password.value}@{rabbitmqcontainer.bindings.tcp.host}:{rabbitmqcontainer.bindings.tcp.port}",
            Bindings = new()
            {
                {
                    "tcp", new()
                    {
                        Scheme = "tcp", Protocol = "tcp", Transport = "tcp", TargetPort = 5672,
                    }
                },
            },
            Image = "rabbitmq:latest",
            Env = new()
            {
                ["RABBITMQ_DEFAULT_USER"] = "guest",
                ["RABBITMQ_DEFAULT_PASS"] = "{rabbitmqcontainer-password.value}",
                ["TEST_RECURSIVE"] = "{rabbitmqcontainer.connectionString}",
                ["ConnectionStrings__RabbitMQService"] = "{rabbitmqcontainer.connectionString}"
            }
        };

        var inputResource = new ParameterResource { Name = "rabbitmqcontainer-password", Value = "secret_password", Inputs = new() { { "value", new() { Secret = true } }, }, };

        var resources = new Dictionary<string, Resource>
        {
            { resource.Name, resource },
            { inputResource.Name, inputResource },
        };

        // Act
        var secretsMap = transformer.ProcessEvaluations(resources);
        state.LoadedAspireManifestResources = resources;
        state.SecretsMap = secretsMap;
        state.OutputFormat = "helm";
        state.AspireComponentsToProcess = state.LoadedAspireManifestResources.Keys.ToList();
        state.ImagePullPolicy = "Always";
        state.OutputPath = "aspire-output";
        state.IncludeDashboard = false;
        state.WithPrivateRegistry = false;
        state.DisableSecrets = false;

        var sut = GetSystemUnderTest(serviceProvider);

        // Act
        var action = () => sut.ExecuteAsync();

        // Assert

        await action.Should().NotThrowAsync();
    }
}
