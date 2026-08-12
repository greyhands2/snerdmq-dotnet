# SnerdMQ .NET SDK (`snerdmq-dotnet`)

The official .NET SDK for SnerdMQ. Execute lightning-fast, persistent background jobs in C#, F#, and VB.NET without setting up Redis or RabbitMQ.

## Features
- **Zero Configuration**: No connection strings, no ports, no firewall rules.
- **Native Task Parallelism**: Leverages C#'s massive `async`/`Task` ThreadPool.
- **ASP.NET Core Friendly**: Never blocks the main event loop.
- **Bulletproof Durability**: Uses OS-level file locking for ACID compliance.

## Installation
*(Coming soon to NuGet)*
```bash
dotnet add package SnerdMQ
```

## Quick Start
```csharp
using SnerdMQ;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Initialize the Queue Orchestrator
        using var queue = new SnerdQueue();

        // 2. Register async job handlers
        queue.RegisterHandler("send_email", async (jsonData) =>
        {
            Console.WriteLine($"Sending email with data: {jsonData}");
            await Task.Delay(1000); // Simulate network request
        });

        // 3. Start listening for jobs in the background ThreadPool
        queue.StartListening();

        // 4. Enqueue a persistent background job!
        queue.Enqueue(
            taskId: "email_123",
            taskType: "send_email",
            jsonData: "{\"user\":\"john.wick@example.com\"}",
            maxRetries: 3,
            retryAfterHours: 0.0
        );

        // Prevent console app from exiting
        await Task.Delay(-1);
    }
}
```

## How it works
This SDK spawns a highly-optimized Rust binary as a child process and communicates with it asynchronously over standard I/O pipes. The Rust engine handles all the complex file-locking, retries, and persistence, while invoking your C# delegates natively!
