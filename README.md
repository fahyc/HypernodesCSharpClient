# HypernodesCSharpClient

# Hyperthetical Client Library

A client library for interacting with the Hyperthetical API, allowing users to communicate with various graphs, manage client functions, and handle user interactions seamlessly.

## Table of Contents

1. [Installation](#installation)
2. [Quick Start](#quick-start)
3. [Usage](#usage)
    - [Client](#client)
    - [User](#user)
    - [RunGraphRequest and RunGraphResponse](#rungraphrequest-and-rungraphresponse)
    - [Other Models](#other-models)
4. [Examples](#examples)
5. [Contributing](#contributing)
6. [License](#license)

## Installation

Before you can use the Hyperthetical library, you need to install it. Add the NuGet package to your project:

```shell
# Use the following command to install the package
dotnet add package HypernodesClient
```

## Quick Start

```csharp
// Initialize the client with your funding key
var client = new Hyperthetical.Client("YOUR_FUNDING_KEY");

// Create a new user
var user = new Hyperthetical.User(client);

// Set the graph for the user
await user.SetGraphAsync("GRAPH_NAME", "GRAPH_USERNAME");

// Chat with the graph
var response = await user.ChatAsync(new List<string> { "Hello, Hyperthetical!" });
Console.WriteLine(response);
```

## Usage

### Client

The `Client` class is the primary point of interaction. It manages the base URL, the funding key, and client functions.

```csharp
var client = new Hyperthetical.Client("YOUR_FUNDING_KEY");
```

You can add custom client functions to the client:

```csharp
client.AddClientFunction("functionName", function);
```

### User

For each interaction, a separate `User` object should be created. This helps in managing session data, the graph a user is communicating with, and other attributes.

```csharp
var user = new Hyperthetical.User(client);
```

The `User` class provides methods to run graphs, chat with them, and fetch available graphs.

### RunGraphRequest and RunGraphResponse

These classes help in sending requests to run specific graphs and process the responses received.

### Other Models

There are other models like `ClientFunction`, `GraphNodeData`, `KeyData`, etc., that help in various functionalities and interactions within the library.

## Examples

Here are some more detailed examples:

*Example 1: Fetching Available Graphs*
```csharp
var availableGraphs = await user.GetAvailableGraphsAsync();
foreach(var graph in availableGraphs)
{
    Console.WriteLine(graph.Name + ": " + graph.Description);
}
```

*Example 2: Custom Client Function*
```csharp
void MyCustomFunction(List<List<string>> args, Hyperthetical.User user)
{
    // Your custom code here
}
client.AddClientFunction("MyFunction", MyCustomFunction);
```

## Contributing

We welcome contributions! Contact me at xo@hyperthetical.dev if you want to ask about a contribution, otherwise just make a pull request.

## License

[MIT License](LICENSE)

---

For detailed documentation, please refer to the official Hyperthetical docs.
