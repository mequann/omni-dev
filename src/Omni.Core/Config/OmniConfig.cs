using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Omni.Core.Config;

public class OmniConfig
{
    public string Name { get; set; } = "workspace";
    public Dictionary<string, ServiceDefinition> Services { get; set; } = new();
    public Dictionary<string, TaskDefinition> Tasks { get; set; } = new();

    public static OmniConfig? LoadFromFile(string filePath)
    {
        if (!File.Exists(filePath)) return null;

        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        return deserializer.Deserialize<OmniConfig>(yaml);
    }

    public static string GenerateSampleYaml()
    {
        return @"# Omni Universal Workspace Configuration
name: my-app

services:
  # Any technology, any command:
  api:
    command: dotnet watch run
    dir: ./backend
    color: purple
    port: 5000

  frontend:
    command: npm run dev
    dir: ./frontend
    color: green
    port: 3000

  worker:
    command: cargo run
    dir: ./rust-worker
    color: yellow

tasks:
  test:
    parallel: true
    commands:
      - dotnet test ./backend
      - npm test --prefix ./frontend
      - cargo test --manifest-path ./rust-worker/Cargo.toml

  build:
    commands:
      - dotnet publish ./backend -c Release
      - npm run build --prefix ./frontend
";
    }
}

public class ServiceDefinition
{
    public string Command { get; set; } = string.Empty;
    public string? Dir { get; set; }
    public string? Color { get; set; }
    public int? Port { get; set; }
    public List<string> DependsOn { get; set; } = new();
    public Dictionary<string, string> Env { get; set; } = new();
}

public class TaskDefinition
{
    public bool Parallel { get; set; } = false;
    public List<string> DependsOn { get; set; } = new();
    public List<string> Commands { get; set; } = new();
}

