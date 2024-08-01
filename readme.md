
---

# SODCustomStateInjectorMiami

CustomStateInjector.Miami (CSI Miami for short) is a plugin designed to allow the injection of custom generation steps during Shadows of Doubt map generation process. It uses attributes for automatic execution in a structured and efficient manner.

## Table of Contents
- [Installation](#installation)
- [Usage](#usage)
    - [Creating Custom States](#creating-custom-states)
    - [Generating Custom States](#generating-custom-states)
- [Modding](#modding)
- [License](#license)

## Installation

1. Download and install BepInEx, if not already installed.
2. Download SOD.Common
2. Place the `SOD.CustomStateInjector.Miami.dll` into the `BepInEx/plugins` folder of your game directory.

## Usage

To use the plugin in your code, you must call it in your Plugin.Load() function like so

```csharp
using SODCustomStateInjectorMiami;
using SODCustomStateInjectorMiami.Attributes;

namespace YourNamespace;
public class Plugin
{

    
    public override void Load()
    {
        CustomStateInjector.InjectStates();
    }
    
}
```


### Creating Custom States

To create a custom state, define a class and annotate it with the `CustomStateAttribute`. The attribute requires two parameters:
- `stepName`: The name of the custom state.
- `afterStepName`: The name of the state after which the custom state should be executed. It can be either a regular step or a custom step

Single Step Example:
```csharp
[CustomState("generateClubs", "generateCompanies")]
public static class CustomStateGenerator
{
    [GenerateState("generateClubs")]
    public static void GenerateClubs()
    {
        Plugin.Log.LogInfo("Generating clubs...");
    }
}
```

Multiple Step Example
```csharp
[CustomState("generateClubs", "generateCompanies")]
[CustomState("generateGuards", "generateClubs")]
[CustomState("generateFaces", "generateBlueprints")]
public static class CustomStateGenerator
{
    [GenerateState("generateClubs")]
    public static void GenerateClubs()
    {
        Plugin.Log.LogInfo("Generating clubs...");
    }
    
    [GenerateState("generateGuards")]
    public static void GenerateGuards()
    {
        Plugin.Log.LogInfo("Generating guards...");
    }
    
        [GenerateState("generateFaces")]
    public static void GenerateFaces()
    {
        Plugin.Log.LogInfo("Generating faces...");
    }
    
}
```
### Generating Custom States

Methods to generate the custom states must be annotated with the `GenerateStateAttribute`, which takes a single parameter:
- `stateName`: The name of the state to be generated.

The plugin automatically registers and validates these methods upon loading.

## Configuration

Custom states can be defined in the configuration file of the plugin. Add your generation steps in the format `stepName:afterStepName`, separated by commas.

Example:
```
GenerationSteps=generateClubs:generateCompanies
```

## Development

### Setting up the Development Environment

1. Clone the repository.
2. Open the solution in your preferred IDE (Visual Studio, Rider, etc.).
3. Add references to BepInEx and other necessary libraries.

### Building the Project

1. Build the solution.
2. The resulting `SODCustomStateInjectorMiami.dll` will be available in the `bin/Debug` or `bin/Release` directory.

### Contributing

1. Fork the repository.
2. Create a new branch (`git checkout -b feature/your-feature-name`).
3. Commit your changes (`git commit -am 'Add some feature'`).
4. Push to the branch (`git push origin feature/your-feature-name`).
5. Create a new Pull Request.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---
